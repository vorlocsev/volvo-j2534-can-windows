[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace VolvoJ2534.App.Tests;

public sealed class IsoTpTests
{
    [Fact]
    public void Segment_SingleFrame_RoundTrips()
    {
        var payload = new byte[] { 0x22, 0xF1, 0x90 };

        var frames = VolvoJ2534.App.IsoTp.Segment(payload);

        Assert.Single(frames);
        Assert.Equal(new byte[] { 0x03, 0x22, 0xF1, 0x90, 0, 0, 0, 0 }, frames[0]);

        var can = new VolvoJ2534.App.CanFrame(0x7E0, false, false, frames[0], 0, 0);
        Assert.True(VolvoJ2534.App.IsoTp.TryDecode(can, out var decoded, out var error), error);
        Assert.Equal(VolvoJ2534.App.IsoTpFrameType.SingleFrame, decoded.Type);
        Assert.Equal(payload, decoded.Data);
    }

    [Fact]
    public void Segment_MultiFrame_Reassembles()
    {
        var payload = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();

        var frames = VolvoJ2534.App.IsoTp.Segment(payload);
        Assert.Equal(3, frames.Count);
        Assert.Equal(0x10, frames[0][0] & 0xF0);
        Assert.Equal(0x21, frames[1][0]);
        Assert.Equal(0x22, frames[2][0]);

        var reassembler = new VolvoJ2534.App.IsoTpReassembler();
        byte[]? result = null;

        foreach (var bytes in frames)
        {
            var can = new VolvoJ2534.App.CanFrame(0x7E8, false, false, bytes, 0, 0);
            Assert.True(reassembler.Push(can, out var completed, out var error), error);
            if (completed is not null)
                result = completed;
        }

        Assert.Equal(payload, result);
        Assert.False(reassembler.InProgress);
    }

    [Fact]
    public void Segment_MaximumPayload_UsesValidFirstFrameLength()
    {
        var payload = Enumerable.Range(0, VolvoJ2534.App.IsoTp.MaxPayloadLength)
            .Select(i => (byte)(i & 0xFF))
            .ToArray();

        var frames = VolvoJ2534.App.IsoTp.Segment(payload);

        Assert.Equal(0x1F, frames[0][0]);
        Assert.Equal(0xFF, frames[0][1]);
        Assert.All(frames, frame => Assert.Equal(8, frame.Length));
    }

    [Fact]
    public void Segment_RejectsPayloadAboveMaximum()
    {
        var payload = new byte[VolvoJ2534.App.IsoTp.MaxPayloadLength + 1];

        Assert.Throws<ArgumentOutOfRangeException>(
            () => VolvoJ2534.App.IsoTp.Segment(payload));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(19)]
    [InlineData(20)]
    public void Reassembler_TrimsPaddingFromFinalConsecutiveFrame(int payloadLength)
    {
        var payload = Enumerable.Range(0, payloadLength).Select(i => (byte)(i + 1)).ToArray();
        var frames = VolvoJ2534.App.IsoTp.Segment(payload);
        var reassembler = new VolvoJ2534.App.IsoTpReassembler();
        byte[]? result = null;

        foreach (var bytes in frames)
        {
            var can = new VolvoJ2534.App.CanFrame(0x7E8, false, false, bytes, 0, 0);
            Assert.True(reassembler.Push(can, out var completed, out var error), error);
            if (completed is not null)
                result = completed;
        }

        Assert.Equal(payload, result);
        Assert.False(reassembler.InProgress);
    }

    [Fact]
    public void Reassembler_RejectsSequenceMismatch()
    {
        var payload = Enumerable.Range(0, 12).Select(i => (byte)i).ToArray();
        var frames = VolvoJ2534.App.IsoTp.Segment(payload);

        var reassembler = new VolvoJ2534.App.IsoTpReassembler();
        var first = new VolvoJ2534.App.CanFrame(0x7E8, false, false, frames[0], 0, 0);
        Assert.True(reassembler.Push(first, out _, out var firstError), firstError);

        var wrong = (byte[])frames[1].Clone();
        wrong[0] = 0x22;
        var second = new VolvoJ2534.App.CanFrame(0x7E8, false, false, wrong, 0, 0);

        Assert.False(reassembler.Push(second, out _, out var error));
        Assert.Contains("sequence mismatch", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(reassembler.InProgress);
    }

    [Fact]
    public void Reassembler_SequenceNumberWrapsAfterFifteen()
    {
        var payload = Enumerable.Range(0, 120).Select(i => (byte)i).ToArray();
        var frames = VolvoJ2534.App.IsoTp.Segment(payload);
        var reassembler = new VolvoJ2534.App.IsoTpReassembler();
        byte[]? result = null;

        foreach (var bytes in frames)
        {
            var can = new VolvoJ2534.App.CanFrame(0x7E8, false, false, bytes, 0, 0);
            Assert.True(reassembler.Push(can, out var completed, out var error), error);
            if (completed is not null)
                result = completed;
        }

        Assert.Equal(0x2F, frames[15][0]);
        Assert.Equal(0x20, frames[16][0]);
        Assert.Equal(payload, result);
        Assert.False(reassembler.InProgress);
    }

    [Fact]
    public void Reassembler_NewFirstFrameRestartsIncompletePayload()
    {
        var firstPayload = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
        var secondPayload = Enumerable.Range(50, 12).Select(i => (byte)i).ToArray();
        var firstFrames = VolvoJ2534.App.IsoTp.Segment(firstPayload);
        var secondFrames = VolvoJ2534.App.IsoTp.Segment(secondPayload);
        var reassembler = new VolvoJ2534.App.IsoTpReassembler();

        var first = new VolvoJ2534.App.CanFrame(0x7E8, false, false, firstFrames[0], 0, 0);
        Assert.True(reassembler.Push(first, out _, out var firstError), firstError);
        Assert.True(reassembler.InProgress);

        byte[]? result = null;
        foreach (var bytes in secondFrames)
        {
            var can = new VolvoJ2534.App.CanFrame(0x7E8, false, false, bytes, 0, 0);
            Assert.True(reassembler.Push(can, out var completed, out var error), error);
            if (completed is not null)
                result = completed;
        }

        Assert.Equal(secondPayload, result);
        Assert.False(reassembler.InProgress);
    }

    [Fact]
    public void ChannelOptions_RejectInvalidStandardCanId()
    {
        var options = new VolvoJ2534.App.IsoTpChannel.Options(0x800, 0x7E8);

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public void ChannelOptions_RejectNegativeTimeout()
    {
        var options = new VolvoJ2534.App.IsoTpChannel.Options(
            0x7E0, 0x7E8, RequestTimeout: TimeSpan.FromMilliseconds(-1));

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public void ChannelOptions_RejectTimeoutTooLargeForStopwatchDeadline()
    {
        var options = new VolvoJ2534.App.IsoTpChannel.Options(
            0x7E0, 0x7E8, RequestTimeout: TimeSpan.MaxValue);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        Assert.Contains("too large", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0x80)]
    [InlineData(0xF0)]
    [InlineData(0xFA)]
    [InlineData(0xFF)]
    public void ChannelOptions_RejectReservedRxSeparationTime(int stmin)
    {
        var options = new VolvoJ2534.App.IsoTpChannel.Options(
            0x7E0, 0x7E8, RxSeparationTime: (byte)stmin);

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void ChannelOptions_AcceptsValid29BitCanIds()
    {
        var options = new VolvoJ2534.App.IsoTpChannel.Options(
            0x18DA10F1, 0x18DAF110, CanExtendedId: true);

        options.Validate();
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x7F)]
    [InlineData(0xF1)]
    [InlineData(0xF9)]
    public void ChannelOptions_AcceptsValidRxSeparationTime(int stmin)
    {
        var options = new VolvoJ2534.App.IsoTpChannel.Options(
            0x7E0, 0x7E8, RxSeparationTime: (byte)stmin);

        options.Validate();
    }

    [Theory]
    [InlineData(0x80)]
    [InlineData(0x81)]
    [InlineData(0xEF)]
    [InlineData(0xF0)]
    [InlineData(0xFA)]
    [InlineData(0xFE)]
    public void ValidateSeparationTime_RejectsReservedValues(int stmin)
    {
        Assert.Throws<InvalidOperationException>(
            () => VolvoJ2534.App.IsoTpChannel.ValidateSeparationTime((byte)stmin));
    }

    [Fact]
    public void TryDecode_RejectsClassicCanPayloadLongerThanEightBytes()
    {
        var can = new VolvoJ2534.App.CanFrame(
            0x7E8, false, false, new byte[9], 0, 0);

        Assert.False(VolvoJ2534.App.IsoTp.TryDecode(can, out _, out var error));
        Assert.Contains("exceeds 8 bytes", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(new byte[] { 0x00 })]
    [InlineData(new byte[] { 0x08, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 })]
    [InlineData(new byte[] { 0x05, 0x11, 0x22 })]
    public void TryDecode_RejectsInvalidSingleFrameLength(byte[] data)
    {
        var can = new VolvoJ2534.App.CanFrame(0x7E8, false, false, data, 0, 0);

        Assert.False(VolvoJ2534.App.IsoTp.TryDecode(can, out _, out var error));
        Assert.Contains("Single Frame length", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(new byte[] { 0x10, 0x08 })]
    [InlineData(new byte[] { 0x10 })]
    public void TryDecode_RejectsFirstFrameWithoutPayload(byte[] data)
    {
        var can = new VolvoJ2534.App.CanFrame(0x7E8, false, false, data, 0, 0);

        Assert.False(VolvoJ2534.App.IsoTp.TryDecode(can, out _, out var error));
        Assert.Contains("First Frame", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDecode_RejectsConsecutiveFrameWithoutPayload()
    {
        var can = new VolvoJ2534.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x21 }, 0, 0);

        Assert.False(VolvoJ2534.App.IsoTp.TryDecode(can, out _, out var error));
        Assert.Contains("Consecutive Frame", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDecode_RejectsFlowControlWithoutAllFields()
    {
        var can = new VolvoJ2534.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x30, 0x00 }, 0, 0);

        Assert.False(VolvoJ2534.App.IsoTp.TryDecode(can, out _, out var error));
        Assert.Contains("Flow Control", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0x33)]
    [InlineData(0x34)]
    [InlineData(0x3F)]
    public void TryDecode_RejectsReservedFlowControlStatus(int pci)
    {
        var can = new VolvoJ2534.App.CanFrame(
            0x7E8, false, false, new byte[] { (byte)pci, 0x00, 0x00 }, 0, 0);

        Assert.False(VolvoJ2534.App.IsoTp.TryDecode(can, out _, out var error));
        Assert.Contains("Flow Status", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reassembler_MalformedFrameResetsIncompletePayload()
    {
        var payload = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
        var frames = VolvoJ2534.App.IsoTp.Segment(payload);
        var reassembler = new VolvoJ2534.App.IsoTpReassembler();

        var first = new VolvoJ2534.App.CanFrame(0x7E8, false, false, frames[0], 0, 0);
        Assert.True(reassembler.Push(first, out _, out var firstError), firstError);
        Assert.True(reassembler.InProgress);

        var malformed = new VolvoJ2534.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x21 }, 0, 0);
        Assert.False(reassembler.Push(malformed, out _, out var error));
        Assert.Contains("Consecutive Frame", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(reassembler.InProgress);

        var continuation = new VolvoJ2534.App.CanFrame(0x7E8, false, false, frames[1], 0, 0);
        Assert.False(reassembler.Push(continuation, out _, out var continuationError));
        Assert.Contains("without First Frame", continuationError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reassembler_UnexpectedFlowControlResetsIncompletePayload()
    {
        var payload = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
        var frames = VolvoJ2534.App.IsoTp.Segment(payload);
        var reassembler = new VolvoJ2534.App.IsoTpReassembler();

        var first = new VolvoJ2534.App.CanFrame(0x7E8, false, false, frames[0], 0, 0);
        Assert.True(reassembler.Push(first, out _, out var firstError), firstError);
        Assert.True(reassembler.InProgress);

        var unexpectedFlowControl = new VolvoJ2534.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x30, 0x00, 0x00 }, 0, 0);
        Assert.False(reassembler.Push(unexpectedFlowControl, out _, out var error));
        Assert.Contains("Flow Control", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(reassembler.InProgress);

        var continuation = new VolvoJ2534.App.CanFrame(0x7E8, false, false, frames[1], 0, 0);
        Assert.False(reassembler.Push(continuation, out _, out var continuationError));
        Assert.Contains("without First Frame", continuationError, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(15)]
    [InlineData(255)]
    public void CreateFlowControl_RejectsReservedStatus(byte status)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => VolvoJ2534.App.IsoTp.CreateFlowControl(status));
    }

    [Fact]
    public void CreateFlowControl_EncodesFields()
    {
        var frame = VolvoJ2534.App.IsoTp.CreateFlowControl(0, 8, 0x0A);

        Assert.Equal(new byte[] { 0x30, 0x08, 0x0A, 0, 0, 0, 0, 0 }, frame);
    }

    [Fact]
    public void IsoTpChannel_CompletesSingleFrameRequestResponse()
    {
        var adapter = new ScenarioJ2534Adapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0, 0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(300),
                RequestTimeout: TimeSpan.FromSeconds(1)));

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x90 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x90, 0x12 }, response);
        Assert.Equal(1, adapter.WriteCount);
    }

    [Fact]
    public void IsoTpChannel_TimesOutWhenNoResponseArrives()
    {
        var adapter = new ScenarioJ2534Adapter { RespondToWrites = false };
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0, 0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(50),
                RequestTimeout: TimeSpan.FromMilliseconds(200)));

        Assert.Throws<TimeoutException>(() =>
            channel.Request(new byte[] { 0x22, 0xF1, 0x90 }));
        Assert.Equal(1, adapter.WriteCount);
    }

    [Fact]
    public async Task IsoTpChannel_CancellationInterruptsWaitingForResponse()
    {
        var adapter = new ScenarioJ2534Adapter { RespondToWrites = false };
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0, 0x7E8,
                FrameTimeout: TimeSpan.FromSeconds(2),
                RequestTimeout: TimeSpan.FromSeconds(5)));
        using var cancellation = new CancellationTokenSource();

        var request = Task.Run(() =>
            channel.Request(new byte[] { 0x22, 0xF1, 0x90 }, cancellation.Token));

        try
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
            while (adapter.WriteCount == 0 && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            Assert.Equal(1, adapter.WriteCount);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                request.WaitAsync(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    [Fact]
    public void IsoTpChannel_SendsFlowControlForEachReceivedBlock()
    {
        var adapter = new MultiFrameResponseScenarioAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0, 0x7E8,
                RxBlockSize: 1,
                FrameTimeout: TimeSpan.FromMilliseconds(300),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(300),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x90 });

        Assert.Equal(Enumerable.Range(0, 20).Select(i => (byte)i), response);
        Assert.Equal(2, adapter.FlowControlWrites.Length);
        Assert.All(adapter.FlowControlWrites, frame =>
        {
            Assert.Equal(0x30, frame.Data[0]);
            Assert.Equal(0x01, frame.Data[1]);
        });
    }

    [Fact]
    public void IsoTpChannel_StopsAfterConfiguredFlowControlWaitLimit()
    {
        var adapter = new MultiFrameScenarioAdapter { WaitFlowControlFrames = 3 };
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0, 0x7E8,
                FlowControlTimeout: TimeSpan.FromMilliseconds(300),
                RequestTimeout: TimeSpan.FromSeconds(2),
                MaxWaitFlowControls: 2));

        var error = Assert.Throws<TimeoutException>(() =>
            channel.Request(Enumerable.Range(0, 20).Select(i => (byte)i).ToArray()));

        Assert.Contains("WAIT limit exceeded", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(adapter.SentFrames);
    }

    [Fact]
    public void IsoTpChannel_RespectsFlowControlSeparationTime()
    {
        var adapter = new MultiFrameScenarioAdapter { SeparationTime = 0x0A };
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0, 0x7E8,
                FlowControlTimeout: TimeSpan.FromMilliseconds(300),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = channel.Request(Enumerable.Range(0, 20).Select(i => (byte)i).ToArray());
        stopwatch.Stop();

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x90 }, response);
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(15),
            $"Expected two 10 ms STmin delays, elapsed {stopwatch.Elapsed}.");
    }

    [Fact]
    public void IsoTpChannel_SendsMultiFrameRequestAfterFlowControl()
    {
        var adapter = new MultiFrameScenarioAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0, 0x7E8,
                FlowControlTimeout: TimeSpan.FromMilliseconds(300),
                FrameTimeout: TimeSpan.FromMilliseconds(300),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        // 27 bytes require one First Frame and three Consecutive Frames.
        // The receiver grants only one CF per Flow Control block.
        var payload = Enumerable.Range(0, 27).Select(i => (byte)i).ToArray();
        var response = channel.Request(payload);

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x90 }, response);
        Assert.Equal(4, adapter.SentFrames.Length);
        Assert.Equal(0x10, adapter.SentFrames[0].Data[0] & 0xF0);
        Assert.Equal(0x21, adapter.SentFrames[1].Data[0]);
        Assert.Equal(0x22, adapter.SentFrames[2].Data[0]);
        Assert.Equal(0x23, adapter.SentFrames[3].Data[0]);
    }

    private sealed class MultiFrameResponseScenarioAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private readonly System.Collections.Concurrent.ConcurrentQueue<VolvoJ2534.App.CanFrame> _flowControlWrites = new();
        private int _flowControlIndex;

        internal VolvoJ2534.App.CanFrame[] FlowControlWrites => _flowControlWrites.ToArray();

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

        public bool Read(out VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            if (_incoming.TryDequeue(out msg))
                return true;

            _incomingReady.WaitOne(TimeSpan.FromMilliseconds(timeout));
            return _incoming.TryDequeue(out msg);
        }

        public bool Write(in VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            if (!VolvoJ2534.App.CanDecoder.TryDecode(msg, out var frame, out error))
                return false;

            if ((frame.Data[0] >> 4) == 3)
            {
                _flowControlWrites.Enqueue(frame);
                var index = Interlocked.Increment(ref _flowControlIndex);
                if (index == 1)
                    Enqueue(0x7E8, new byte[] { 0x21, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C });
                else if (index == 2)
                    Enqueue(0x7E8, new byte[] { 0x22, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13 });
            }
            else
            {
                Enqueue(0x7E8, new byte[] { 0x10, 0x14, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 });
            }

            error = string.Empty;
            return true;
        }

        private void Enqueue(uint id, byte[] data)
        {
            _incoming.Enqueue(VolvoJ2534.App.CanDecoder.Encode(id, data));
            _incomingReady.Set();
        }

        public void Unload() { }
    }

    private sealed class MultiFrameScenarioAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly System.Collections.Concurrent.ConcurrentQueue<VolvoJ2534.App.CanFrame> _sentFrames = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _expectedRequestFrames;

        internal VolvoJ2534.App.CanFrame[] SentFrames => _sentFrames.ToArray();
        internal int WaitFlowControlFrames { get; init; }
        internal byte SeparationTime { get; init; }

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

        public bool Read(out VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            if (_incoming.TryDequeue(out msg))
                return true;

            _incomingReady.WaitOne(TimeSpan.FromMilliseconds(timeout));
            return _incoming.TryDequeue(out msg);
        }

        public bool Write(in VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            if (VolvoJ2534.App.CanDecoder.TryDecode(msg, out var frame, out error))
            {
                _sentFrames.Enqueue(frame);

                if (_sentFrames.Count == 1 && (frame.Data[0] & 0xF0) == 0x10)
                {
                    var payloadLength = ((frame.Data[0] & 0x0F) << 8) | frame.Data[1];
                    _expectedRequestFrames = 1 + Math.Max(0, (payloadLength - 6 + 6) / 7);
                }

                // Block Size=1 means the sender must request another FC
                // after each CF while more payload remains.
                if (_sentFrames.Count == 1 && WaitFlowControlFrames > 0)
                {
                    for (var i = 0; i < WaitFlowControlFrames; i++)
                        Enqueue(0x7E8, new byte[] { 0x31, 0x00, 0x00 });
                    Enqueue(0x7E8, new byte[] { 0x30, 0x00, SeparationTime });
                }
                else if (_sentFrames.Count < _expectedRequestFrames)
                {
                    Enqueue(0x7E8, new byte[] { 0x30, 0x01, SeparationTime });
                }
                else if (_sentFrames.Count == _expectedRequestFrames)
                {
                    Enqueue(0x7E8, new byte[] { 0x03, 0x62, 0xF1, 0x90 });
                }
            }
            return string.IsNullOrEmpty(error);
        }

        private void Enqueue(uint id, byte[] data)
        {
            _incoming.Enqueue(VolvoJ2534.App.CanDecoder.Encode(id, data));
            _incomingReady.Set();
        }

        public void Unload() { }
    }

    private sealed class ScenarioJ2534Adapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _writeCount;

        internal bool RespondToWrites { get; init; } = true;
        internal int WriteCount => Volatile.Read(ref _writeCount);

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

        public bool Read(out VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            if (_incoming.TryDequeue(out msg))
                return true;

            _incomingReady.WaitOne(TimeSpan.FromMilliseconds(timeout));
            return _incoming.TryDequeue(out msg);
        }

        public bool Write(in VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            Interlocked.Increment(ref _writeCount);
            if (RespondToWrites)
            {
                var response = VolvoJ2534.App.CanDecoder.Encode(
                    0x7E8, new byte[] { 0x04, 0x62, 0xF1, 0x90, 0x12 });
                _incoming.Enqueue(response);
                _incomingReady.Set();
            }
            return true;
        }

        public void Unload() { }

        ~ScenarioJ2534Adapter() => _incomingReady.Dispose();
    }

    [Theory]
    [InlineData(new byte[] { 0x10, 0x00, 0xAA })]
    [InlineData(new byte[] { 0x10, 0x07, 0xAA, 0xBB, 0xCC })]
    public void TryDecode_RejectsFirstFrameWithDeclaredLengthBelowMinimum(byte[] data)
    {
        var can = new VolvoJ2534.App.CanFrame(0x7E8, false, false, data, 0, 0);

        Assert.False(VolvoJ2534.App.IsoTp.TryDecode(can, out _, out var error));
        Assert.Contains("payload length", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reassembler_InvalidFirstFrameResetsPreviouslyIncompleteMessage()
    {
        var reassembler = new VolvoJ2534.App.IsoTpReassembler();
        var validFirst = new VolvoJ2534.App.CanFrame(
            0x7E8, false, false,
            new byte[] { 0x10, 0x14, 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03 }, 0, 0);

        Assert.True(reassembler.Push(validFirst, out _, out var validError), validError);
        Assert.True(reassembler.InProgress);

        var malformedFirst = new VolvoJ2534.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x10, 0x07, 0x62, 0xF1, 0x90 }, 0, 0);
        Assert.False(reassembler.Push(malformedFirst, out _, out var error));
        Assert.Contains("payload length", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(reassembler.InProgress);
    }

}
