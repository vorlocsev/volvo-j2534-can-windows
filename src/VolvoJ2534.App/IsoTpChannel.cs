using System.Diagnostics;

namespace VolvoJ2534.App;

internal sealed class IsoTpChannel : IDisposable
{
    internal sealed record Options(
        uint RequestId,
        uint ResponseId,
        bool CanExtendedId = false,
        byte RxBlockSize = 0,
        byte RxSeparationTime = 0,
        TimeSpan FrameTimeout = default,
        TimeSpan FlowControlTimeout = default,
        TimeSpan ConsecutiveFrameTimeout = default,
        TimeSpan RequestTimeout = default,
        int MaxWaitFlowControls = 8)
    {
        internal TimeSpan EffectiveFrameTimeout =>
            FrameTimeout == default ? TimeSpan.FromMilliseconds(1000) : FrameTimeout;

        internal TimeSpan EffectiveFlowControlTimeout =>
            FlowControlTimeout == default ? TimeSpan.FromMilliseconds(1000) : FlowControlTimeout;

        internal TimeSpan EffectiveConsecutiveFrameTimeout =>
            ConsecutiveFrameTimeout == default ? TimeSpan.FromMilliseconds(1000) : ConsecutiveFrameTimeout;

        internal TimeSpan EffectiveRequestTimeout =>
            RequestTimeout == default ? TimeSpan.FromSeconds(5) : RequestTimeout;

        internal void Validate()
        {
            ValidateId(RequestId, CanExtendedId, nameof(RequestId));
            ValidateId(ResponseId, CanExtendedId, nameof(ResponseId));

            if (MaxWaitFlowControls < 0)
                throw new ArgumentOutOfRangeException(nameof(MaxWaitFlowControls));

            ValidateTimeout(FrameTimeout, nameof(FrameTimeout));
            ValidateTimeout(FlowControlTimeout, nameof(FlowControlTimeout));
            ValidateTimeout(ConsecutiveFrameTimeout, nameof(ConsecutiveFrameTimeout));
            ValidateTimeout(RequestTimeout, nameof(RequestTimeout));
            ValidateSeparationTime(RxSeparationTime);
        }

        private static void ValidateTimeout(TimeSpan value, string name)
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(name, "Timeout must be positive or zero to use the default.");

            // Request deadlines are represented by Stopwatch timestamps. Reject
            // values that cannot safely be converted to timestamp ticks and added
            // to the current timestamp.
            var maxSeconds = (double)(long.MaxValue - Stopwatch.GetTimestamp()) / Stopwatch.Frequency;
            if (value.TotalSeconds > maxSeconds)
                throw new ArgumentOutOfRangeException(name, "Timeout is too large to represent as a Stopwatch deadline.");
        }
    }

    private readonly CanBus _bus;
    private readonly CanRxDispatcher.Subscription _rx;
    private readonly Options _options;
    private readonly CancellationTokenSource _disposeCts = new();
    private int _disposed;

    internal IsoTpChannel(CanBus bus, Options options)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Validate before creating a subscription so invalid options cannot
        // leave a registered receiver behind.
        _options.Validate();
        _rx = _bus.Subscribe();

        // CAN Extended ID (29-bit) is independent from ISO-TP extended
        // addressing (an additional address byte in the CAN data field).
        // This channel currently implements ISO-TP normal addressing only.
    }

    internal byte[] Request(ReadOnlySpan<byte> payload, CancellationToken cancellationToken = default)
        => Request(payload, static _ => false, cancellationToken);

    private readonly object _requestGate = new();

    internal byte[] Request(
        ReadOnlySpan<byte> payload,
        Func<byte[], bool> isInterimResponse,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(isInterimResponse);
        ThrowIfDisposed();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _disposeCts.Token);
        cancellationToken = linkedCancellation.Token;
        if (payload.Length == 0 || payload.Length > IsoTp.MaxPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(payload),
                $"ISO-TP payload must be 1..{IsoTp.MaxPayloadLength} bytes.");

        // The request timeout includes time spent waiting for another transaction
        // to release the channel. A regular lock would make that wait impossible
        // to cancel and would start the timeout only after acquiring the lock.
        var deadline = Stopwatch.GetTimestamp() + ToTimestampTicks(_options.EffectiveRequestTimeout);
        var entered = false;
        try
        {
            while (!entered)
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();
                var remaining = Remaining(deadline);
                if (remaining <= TimeSpan.Zero)
                    throw new TimeoutException("ISO-TP request timeout expired while waiting for the channel.");

                var waitMilliseconds = Math.Max(1, (int)Math.Min(10, Math.Ceiling(remaining.TotalMilliseconds)));
                entered = Monitor.TryEnter(_requestGate, waitMilliseconds);
            }

            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            return RequestLocked(payload, isInterimResponse, deadline, cancellationToken);
        }
        finally
        {
            if (entered)
                Monitor.Exit(_requestGate);
        }
    }

    private byte[] RequestLocked(
        ReadOnlySpan<byte> payload,
        Func<byte[], bool> isInterimResponse,
        long deadline,
        CancellationToken cancellationToken)
    {
        // Drop frames that were buffered before this transaction. This prevents
        // already-queued frames from a timed-out request being consumed as the
        // next response. Frames arriving after the drain still need UDS correlation.
        _rx.DrainPendingFrames();

        var frames = IsoTp.Segment(payload);
        if (frames.Count == 1)
        {
            WriteCan(_options.RequestId, frames[0], deadline, cancellationToken);
            return ReceiveFinalPayload(deadline, cancellationToken, isInterimResponse);
        }

        WriteCan(_options.RequestId, frames[0], deadline, cancellationToken);

        var flowControl = WaitForFlowControl(deadline, cancellationToken);
        var offset = 6;
        byte sequence = 1;
        var sentInBlock = 0;

        while (offset < payload.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var count = Math.Min(7, payload.Length - offset);
            var cf = new byte[8];
            cf[0] = (byte)(0x20 | sequence);
            payload.Slice(offset, count).CopyTo(cf.AsSpan(1));

            WaitSeparation(flowControl.SeparationTime, deadline, cancellationToken);
            WriteCan(_options.RequestId, cf, deadline, cancellationToken);

            offset += count;
            sequence = (byte)((sequence + 1) & 0x0F);
            sentInBlock++;

            if (flowControl.BlockSize != 0 && sentInBlock >= flowControl.BlockSize && offset < payload.Length)
            {
                flowControl = WaitForFlowControl(deadline, cancellationToken);
                sentInBlock = 0;
            }
        }

        return ReceiveFinalPayload(deadline, cancellationToken, isInterimResponse);
    }

    private byte[] ReceiveFinalPayload(
        long deadline,
        CancellationToken cancellationToken,
        Func<byte[], bool> isInterimResponse)
    {
        while (true)
        {
            var response = ReceivePayload(deadline, cancellationToken);
            if (!isInterimResponse(response))
                return response;
        }
    }

    private byte[] ReceivePayload(long deadline, CancellationToken cancellationToken)
    {
        var reassembler = new IsoTpReassembler();

        while (true)
        {
            var frame = ReadMatchingCanFrame(_options.ResponseId, MinDeadline(deadline, _options.EffectiveFrameTimeout), cancellationToken);

            if (!IsoTp.TryDecode(frame, out var iso, out var error))
                throw new InvalidOperationException(error);

            switch (iso.Type)
            {
                case IsoTpFrameType.SingleFrame:
                    return iso.Data;

                case IsoTpFrameType.FirstFrame:
                {
                    if (!reassembler.Push(frame, out var payload, out error))
                        throw new InvalidOperationException(error);

                    if (payload is not null)
                        return payload;

                    SendFlowControl(
                        IsoTpFlowStatus.ContinueToSend,
                        _options.RxBlockSize,
                        _options.RxSeparationTime,
                        deadline,
                        cancellationToken);

                    var receivedInBlock = 0;
                    while (true)
                    {
                        frame = ReadMatchingCanFrame(_options.ResponseId, MinDeadline(deadline, _options.EffectiveConsecutiveFrameTimeout), cancellationToken);

                        if (!IsoTp.TryDecode(frame, out iso, out error))
                            throw new InvalidOperationException(error);

                        if (iso.Type == IsoTpFrameType.FlowControl)
                            throw new InvalidOperationException(
                                "Unexpected ISO-TP Flow Control while receiving a multi-frame response.");

                        if (iso.Type != IsoTpFrameType.ConsecutiveFrame)
                            throw new InvalidOperationException(
                                $"Unexpected ISO-TP {iso.Type} while receiving a multi-frame response.");

                        if (!reassembler.Push(frame, out payload, out error))
                            throw new InvalidOperationException(error);

                        receivedInBlock++;

                        if (payload is not null)
                            return payload;

                        if (_options.RxBlockSize != 0 && receivedInBlock >= _options.RxBlockSize)
                        {
                            SendFlowControl(
                                IsoTpFlowStatus.ContinueToSend,
                                _options.RxBlockSize,
                                _options.RxSeparationTime,
                                deadline,
                                cancellationToken);
                            receivedInBlock = 0;
                        }
                    }
                }

                case IsoTpFrameType.FlowControl:
                    continue;

                case IsoTpFrameType.ConsecutiveFrame:
                    // A CF without a first frame belongs to no active message.
                    // Ignore it: it may be a late fragment from a timed-out exchange.
                    continue;

                default:
                    throw new InvalidOperationException("Unexpected ISO-TP frame type.");
            }
        }
    }

    private IsoTpFrame WaitForFlowControl(long deadline, CancellationToken cancellationToken)
    {
        var waits = 0;

        while (true)
        {
            var frame = ReadMatchingCanFrame(_options.ResponseId, MinDeadline(deadline, _options.EffectiveFlowControlTimeout), cancellationToken);

            if (!IsoTp.TryDecode(frame, out var iso, out var error))
                throw new InvalidOperationException(error);

            if (iso.Type != IsoTpFrameType.FlowControl)
                continue;

            switch (iso.FlowStatus)
            {
                case IsoTpFlowStatus.ContinueToSend:
                    ValidateSeparationTime(iso.SeparationTime);
                    return iso;

                case IsoTpFlowStatus.Wait:
                    if (++waits > _options.MaxWaitFlowControls)
                        throw new TimeoutException("ISO-TP Flow Control WAIT limit exceeded.");
                    continue;

                case IsoTpFlowStatus.Overflow:
                    throw new InvalidOperationException("ISO-TP receiver reported Overflow.");

                default:
                    throw new InvalidOperationException(
                        $"Unsupported ISO-TP Flow Control status: {(byte)iso.FlowStatus:X1}.");
            }
        }
    }

    private void SendFlowControl(
        IsoTpFlowStatus status,
        byte blockSize,
        byte separationTime,
        long deadline,
        CancellationToken cancellationToken)
    {
        var fc = IsoTp.CreateFlowControl((byte)status, blockSize, separationTime);
        WriteCan(_options.RequestId, fc, deadline, cancellationToken);
    }

    private CanFrame ReadMatchingCanFrame(
        uint expectedId,
        long deadline,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = Remaining(deadline);
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException("ISO-TP receive timeout expired.");

            if (!_rx.TryRead(remaining, cancellationToken, out var frame))
                throw new TimeoutException("ISO-TP receive timeout expired.");

            if (frame.ArbitrationId != expectedId ||
                frame.IsExtended != _options.CanExtendedId ||
                frame.IsRemote)
                continue;

            return frame;
        }
    }

    private void WriteCan(
        uint id,
        ReadOnlySpan<byte> data,
        long deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (data.Length > 8)
            throw new ArgumentOutOfRangeException(nameof(data), "Classic CAN payload cannot exceed 8 bytes.");

        var remaining = Remaining(deadline);
        if (remaining <= TimeSpan.Zero)
            throw new TimeoutException("ISO-TP request timeout expired.");

        if (!_bus.Send(
                id,
                data,
                _options.CanExtendedId,
                remaining,
                cancellationToken,
                out var error))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                ? "J2534 CAN write failed."
                : error);
        }
    }

    private static void WaitSeparation(byte separationTime, long deadline, CancellationToken cancellationToken)
    {
        var delay = DecodeSeparationTime(separationTime);
        if (delay <= TimeSpan.Zero)
            return;

        var remaining = Remaining(deadline);
        if (remaining <= TimeSpan.Zero)
            throw new TimeoutException("ISO-TP request timeout expired.");

        if (delay > remaining)
            throw new TimeoutException("ISO-TP STmin exceeds remaining request timeout.");

        if (delay.TotalMilliseconds >= 1)
            Task.Delay(delay, cancellationToken).GetAwaiter().GetResult();
        else
            SpinWaitUntil(delay, cancellationToken);
    }

    private static void SpinWaitUntil(TimeSpan delay, CancellationToken cancellationToken)
    {
        var until = Stopwatch.GetTimestamp() + ToTimestampTicks(delay);
        var spinner = new SpinWait();

        while (Stopwatch.GetTimestamp() < until)
        {
            cancellationToken.ThrowIfCancellationRequested();
            spinner.SpinOnce();
        }
    }

    private static TimeSpan DecodeSeparationTime(byte value)
    {
        ValidateSeparationTime(value);

        if (value <= 0x7F)
            return TimeSpan.FromMilliseconds(value);

        return TimeSpan.FromTicks((value - 0xF0) * 1000);
    }

    internal static void ValidateSeparationTime(byte value)
    {
        if (value is >= 0x80 and <= 0xF0)
            throw new InvalidOperationException($"Reserved ISO-TP STmin value: 0x{value:X2}.");
        if (value >= 0xFA)
            throw new InvalidOperationException($"Reserved ISO-TP STmin value: 0x{value:X2}.");
    }

    private static void ValidateId(uint id, bool extended, string parameterName)
    {
        var max = extended ? 0x1FFFFFFFu : 0x7FFu;
        if (id > max)
            throw new ArgumentOutOfRangeException(parameterName,
                $"CAN ID must be <= 0x{max:X} for {(extended ? "29-bit" : "11-bit")} CAN IDs.");
    }

    private static long MinDeadline(long globalDeadline, TimeSpan stageTimeout)
    {
        var stageTicks = ToTimestampTicks(stageTimeout);
        var candidate = Stopwatch.GetTimestamp() + stageTicks;
        return Math.Min(globalDeadline, candidate);
    }

    private static TimeSpan Remaining(long deadline)
    {
        var ticks = deadline - Stopwatch.GetTimestamp();
        return ticks <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)ticks / Stopwatch.Frequency);
    }

    private static long ToTimestampTicks(TimeSpan value)
        => checked((long)(value.TotalSeconds * Stopwatch.Frequency));

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(IsoTpChannel));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        // Cancel any transaction first so waits for Flow Control or a response
        // unwind promptly. Then wait for the request gate before disposing RX:
        // an in-flight request must not keep sending CAN frames against a
        // disposed channel or race its subscription teardown.
        _disposeCts.Cancel();
        lock (_requestGate)
            _rx.Dispose();
    }
}
