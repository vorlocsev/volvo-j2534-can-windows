using System.Diagnostics;

namespace VolvoJ2534.App;

internal sealed class IsoTpChannel : IDisposable
{
    internal sealed record Options(
        uint RequestId,
        uint ResponseId,
        bool ExtendedAddressing = false,
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
    }

    private readonly CanBus _bus;
    private readonly CanRxDispatcher.Subscription _rx;
    private readonly Options _options;

    internal IsoTpChannel(CanBus bus, Options options)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _rx = _bus.Subscribe();

        ValidateId(_options.RequestId, _options.ExtendedAddressing, nameof(options.RequestId));
        ValidateId(_options.ResponseId, _options.ExtendedAddressing, nameof(options.ResponseId));

        if (_options.MaxWaitFlowControls < 0)
            throw new ArgumentOutOfRangeException(nameof(options.MaxWaitFlowControls));
    }

    internal byte[] Request(ReadOnlySpan<byte> payload, CancellationToken cancellationToken = default)
    {
        if (payload.Length == 0 || payload.Length > IsoTp.MaxPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(payload),
                $"ISO-TP payload must be 1..{IsoTp.MaxPayloadLength} bytes.");

        var deadline = Stopwatch.GetTimestamp() + ToTimestampTicks(_options.EffectiveRequestTimeout);
        cancellationToken.ThrowIfCancellationRequested();

        var frames = IsoTp.Segment(payload);
        if (frames.Count == 1)
        {
            WriteCan(_options.RequestId, frames[0], deadline, cancellationToken);
            return ReceivePayload(deadline, cancellationToken);
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

        return ReceivePayload(deadline, cancellationToken);
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

                        if (iso.Type != IsoTpFrameType.ConsecutiveFrame)
                            continue;

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
                    // A Flow Control belonging to another exchange is not an application payload.
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
                frame.IsExtended != _options.ExtendedAddressing ||
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
                _options.ExtendedAddressing,
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

    private static void ValidateSeparationTime(byte value)
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
                $"CAN ID must be <= 0x{max:X} for {(extended ? "29-bit" : "11-bit")} addressing.");
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
    public void Dispose() => _rx.Dispose();
}
