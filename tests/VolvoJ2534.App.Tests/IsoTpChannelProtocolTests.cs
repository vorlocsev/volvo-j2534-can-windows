using System.Collections.Concurrent;

namespace VolvoJ2534.App.Tests;

public sealed class IsoTpChannelProtocolTests
{
    [Fact]
    public void Request_RejectsFlowControlDuringMultiFrameResponse()
    {
        var adapter = new UnexpectedFlowControlResponseAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(300),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(300),
                RequestTimeout: TimeSpan.FromSeconds(1)));

        var error = Assert.Throws<InvalidOperationException>(() =>
            channel.Request(new byte[] { 0x22, 0xF1, 0x90 }));

        Assert.Contains("Unexpected ISO-TP Flow Control", error.Message, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void UdsClient_IgnoresResponseForDifferentDidAndAcceptsMatchingResponse()
    {
        var adapter = new UnrelatedThenMatchingDidAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(300),
                RequestTimeout: TimeSpan.FromSeconds(1)));
        using var client = new VolvoJ2534.App.UdsClient(channel);

        var data = client.ReadDataByIdentifier(0xF190);

        Assert.Equal(new byte[] { 0xF1, 0x90 }, data);
    }

    [Fact]
    public void UdsClient_RejectsTruncatedReadDataByIdentifierResponse()
    {
        var adapter = new UnrelatedThenMatchingDidAdapter(new byte[] { 0x02, 0x62, 0xF1 });
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(300),
                RequestTimeout: TimeSpan.FromSeconds(1)));
        using var client = new VolvoJ2534.App.UdsClient(channel);

        var error = Assert.Throws<InvalidOperationException>(() => client.ReadDataByIdentifier(0xF190));

        Assert.Contains("did not contain requested DID", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Request_CancellationWhileWaitingForChannelGateIsHonored()
    {
        using var writeEntered = new ManualResetEventSlim();
        using var allowWriteToFinish = new ManualResetEventSlim();
        var adapter = new BlockingWriteAdapter(writeEntered, allowWriteToFinish);
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(100),
                RequestTimeout: TimeSpan.FromMilliseconds(500)));

        var firstRequest = Task.Run(() =>
            Assert.Throws<TimeoutException>(() => channel.Request(new byte[] { 0x22, 0xF1, 0x90 })));

        try
        {
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(2)), "First request did not enter the adapter write.");

            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Assert.ThrowsAny<OperationCanceledException>(() =>
                channel.Request(new byte[] { 0x22, 0xF1, 0x91 }, cancellation.Token));
            Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(300),
                "Cancellation while waiting for the channel gate took too long.");
        }
        finally
        {
            // Never leave the first request blocked if an assertion fails.
            allowWriteToFinish.Set();
        }

        await firstRequest.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private sealed class BlockingWriteAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ManualResetEventSlim _writeEntered;
        private readonly ManualResetEventSlim _allowWriteToFinish;

        internal BlockingWriteAdapter(ManualResetEventSlim writeEntered, ManualResetEventSlim allowWriteToFinish)
        {
            _writeEntered = writeEntered;
            _allowWriteToFinish = allowWriteToFinish;
        }

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

        public bool Read(out VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            msg = default;
            Thread.Sleep((int)Math.Min(timeout, 5));
            return false;
        }

        public bool Write(in VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            _writeEntered.Set();
            _allowWriteToFinish.Wait();
            return true;
        }

        public void Unload() { }
    }

    private sealed class UnrelatedThenMatchingDidAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _responded;
        private readonly byte[] _matchingResponse;

        internal UnrelatedThenMatchingDidAdapter(byte[]? matchingResponse = null)
            => _matchingResponse = matchingResponse ?? new byte[] { 0x03, 0x62, 0xF1, 0x90 };

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
            if (Interlocked.Exchange(ref _responded, 1) == 0)
            {
                // An unrelated DID arrives first; the matching response follows.
                Enqueue(new byte[] { 0x03, 0x62, 0xF1, 0x91 });
                Enqueue(_matchingResponse);
            }

            return true;
        }

        private void Enqueue(byte[] data)
        {
            _incoming.Enqueue(VolvoJ2534.App.CanDecoder.Encode(0x7E8, data));
            _incomingReady.Set();
        }

        public void Unload() { }
    }

    private sealed class UnexpectedFlowControlResponseAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);

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
            if (!VolvoJ2534.App.CanDecoder.TryDecode(msg, out var frame, out error))
                return false;

            if ((frame.Data[0] >> 4) == 3)
            {
                Enqueue(0x7E8, new byte[] { 0x30, 0x00, 0x00 });
                Enqueue(0x7E8, new byte[] { 0x21, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B });
            }
            else
            {
                Enqueue(0x7E8, new byte[] { 0x10, 0x0C, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 });
            }

            return true;
        }

        private void Enqueue(uint id, byte[] data)
        {
            _incoming.Enqueue(VolvoJ2534.App.CanDecoder.Encode(id, data));
            _incomingReady.Set();
        }

        public void Unload() { }
    }
}
