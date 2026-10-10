using System.Collections.Concurrent;

namespace VolvoJ2534.App.Tests;

public sealed class IsoTpChannelProtocolTests
{
    [Fact]
    public void Request_AfterDisposeThrowsWithoutSendingCanFrame()
    {
        var adapter = new LateConsecutiveFrameAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(0x7E0, 0x7E8));

        channel.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            channel.Request(new byte[] { 0x22, 0xF1, 0x90 }));
        Assert.Equal(0, adapter.RequestCount);
    }

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
    public void Request_LateConsecutiveFrameAfterTimeoutDoesNotPoisonNextTransaction()
    {
        var adapter = new LateConsecutiveFrameAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(200),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(100),
                RequestTimeout: TimeSpan.FromMilliseconds(700)));

        Assert.Throws<TimeoutException>(() =>
            channel.Request(new byte[] { 0x22, 0xF1, 0x90 }));

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x91 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91 }, response);
        Assert.Equal(2, adapter.RequestCount);
    }

    [Fact]
    public void Request_CancellationDuringMultiFrameResponseAllowsNextTransaction()
    {
        var adapter = new CanceledMultiFrameResponseAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                ConsecutiveFrameTimeout: TimeSpan.FromSeconds(1),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150)))
        {
            Assert.ThrowsAny<OperationCanceledException>(() =>
                channel.Request(new byte[] { 0x22, 0xF1, 0x90 }, cancellation.Token));
        }

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x91 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91 }, response);
        Assert.Equal(2, adapter.RequestCount);
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
    public void UdsClient_IgnoresResponsePendingAndAcceptsFinalResponse()
    {
        var adapter = new PendingThenFinalResponseAdapter();
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
        Assert.Equal(1, adapter.WriteCount);
    }

    [Fact]
    public void UdsClient_PendingThenNegativeResponseReportsFinalNrc()
    {
        var adapter = new PendingThenNegativeResponseAdapter();
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

        var error = Assert.Throws<VolvoJ2534.App.UdsNegativeResponseException>(
            () => client.ReadDataByIdentifier(0xF190));

        Assert.Equal((byte)0x22, error.RequestedService);
        Assert.Equal((byte)0x31, error.NegativeResponseCode);
        Assert.Equal(1, adapter.WriteCount);
    }

    [Fact]
    public void UdsClient_RepeatedResponsePendingDoesNotExtendOverallRequestTimeout()
    {
        var adapter = new RepeatedPendingResponseAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromMilliseconds(220)));
        using var client = new VolvoJ2534.App.UdsClient(channel);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Throws<TimeoutException>(() => client.ReadDataByIdentifier(0xF190));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            "Repeated response-pending frames extended the request beyond its overall deadline.");
        Assert.Equal(1, adapter.WriteCount);
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
    public void Request_PreCanceledTokenDoesNotWriteCanFrame()
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
                RequestTimeout: TimeSpan.FromSeconds(1)));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            channel.Request(new byte[] { 0x22, 0xF1, 0x90 }, cancellation.Token));
        Assert.Equal(0, adapter.WriteCount);
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

    [Fact]
    public async Task Request_TimeoutWhileWaitingForChannelGateDoesNotSendAnotherRequest()
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
                RequestTimeout: TimeSpan.FromMilliseconds(150)));

        var firstRequest = Task.Run(() =>
            Assert.Throws<TimeoutException>(() => channel.Request(new byte[] { 0x22, 0xF1, 0x90 })));

        try
        {
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(2)), "First request did not enter the adapter write.");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var error = Assert.Throws<TimeoutException>(() =>
                channel.Request(new byte[] { 0x22, 0xF1, 0x91 }));
            Assert.Contains("waiting for the channel", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(500),
                "Channel-gate timeout took longer than expected.");
            Assert.Equal(1, adapter.WriteCount);
        }
        finally
        {
            // The fake native write deliberately ignores its timeout; always unblock it.
            allowWriteToFinish.Set();
        }

        await firstRequest.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, adapter.WriteCount);
    }

    private sealed class LateConsecutiveFrameAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _requestCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);

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

            // The tester's Flow Control is a PCI type 3. The fake ECU intentionally
            // never sends a CF for the first response, forcing a consecutive-frame timeout.
            if ((frame.Data[0] >> 4) == 3)
                return true;

            if ((frame.Data[0] >> 4) == 0 && Interlocked.Increment(ref _requestCount) == 1)
            {
                Enqueue(new byte[] { 0x10, 0x08, 0x62, 0xF1, 0x90, 0x41, 0x42, 0x43 });
            }
            else if ((frame.Data[0] >> 4) == 0)
            {
                // A late CF from the previous exchange arrives before the valid SF.
                Enqueue(new byte[] { 0x21, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A });
                Enqueue(new byte[] { 0x03, 0x62, 0xF1, 0x91 });
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

    private sealed class CanceledMultiFrameResponseAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _requestCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);

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

            // Flow Control is not a new diagnostic request.
            if ((frame.Data[0] >> 4) == 3)
                return true;

            if ((frame.Data[0] >> 4) == 0)
            {
                var requestNumber = Interlocked.Increment(ref _requestCount);
                if (requestNumber == 1)
                    Enqueue(new byte[] { 0x10, 0x08, 0x62, 0xF1, 0x90, 0x41, 0x42, 0x43 });
                else
                    Enqueue(new byte[] { 0x03, 0x62, 0xF1, 0x91 });
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

    private sealed class PendingThenNegativeResponseAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _responded;
        private int _writeCount;

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
            if (Interlocked.Exchange(ref _responded, 1) == 0)
            {
                Enqueue(new byte[] { 0x03, 0x7F, 0x22, 0x78 });
                Enqueue(new byte[] { 0x03, 0x7F, 0x22, 0x31 });
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

    private sealed class RepeatedPendingResponseAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _responded;
        private int _writeCount;

        internal int WriteCount => Volatile.Read(ref _writeCount);

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

        public bool Read(out VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            Thread.Sleep((int)Math.Min(timeout, 50));
            return _incoming.TryDequeue(out msg);
        }

        public bool Write(in VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            Interlocked.Increment(ref _writeCount);
            if (Interlocked.Exchange(ref _responded, 1) == 0)
            {
                for (var i = 0; i < 12; i++)
                    _incoming.Enqueue(VolvoJ2534.App.CanDecoder.Encode(
                        0x7E8, new byte[] { 0x03, 0x7F, 0x22, 0x78 }));
                _incomingReady.Set();
            }

            return true;
        }

        public void Unload() { }
    }

    private sealed class PendingThenFinalResponseAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _responded;
        private int _writeCount;

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
            if (Interlocked.Exchange(ref _responded, 1) == 0)
            {
                Enqueue(new byte[] { 0x03, 0x7F, 0x22, 0x78 });
                Enqueue(new byte[] { 0x03, 0x62, 0xF1, 0x90 });
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

    private sealed class BlockingWriteAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ManualResetEventSlim _writeEntered;
        private readonly ManualResetEventSlim _allowWriteToFinish;
        private int _writeCount;

        internal int WriteCount => Volatile.Read(ref _writeCount);

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
            Interlocked.Increment(ref _writeCount);
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


    [Fact]
    public void Request_ReassemblesMultiFrameResponseAcrossSequenceNumberRollover()
    {
        var adapter = new SequenceNumberRolloverAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(3)));

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x90 });

        Assert.Equal(adapter.ExpectedPayload, response);
        Assert.Equal(16, adapter.ConsecutiveFramesSent);
    }

    private sealed class SequenceNumberRolloverAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _consecutiveFramesSent;

        internal byte[] ExpectedPayload { get; } = CreatePayload();
        internal int ConsecutiveFramesSent => Volatile.Read(ref _consecutiveFramesSent);

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
                // A 118-byte payload requires 16 CFs, so the PCI sequence rolls
                // over from 0x2F to 0x20 on the final consecutive frame.
                for (var frameIndex = 0; frameIndex < 16; frameIndex++)
                {
                    var cf = new byte[8];
                    cf[0] = (byte)(0x20 | ((frameIndex + 1) & 0x0F));
                    var offset = 6 + frameIndex * 7;
                    ExpectedPayload.AsSpan(offset, 7).CopyTo(cf.AsSpan(1));
                    Enqueue(cf);
                    Interlocked.Increment(ref _consecutiveFramesSent);
                }
            }
            else if ((frame.Data[0] >> 4) == 0)
            {
                var firstFrame = new byte[8];
                firstFrame[0] = 0x10;
                firstFrame[1] = (byte)ExpectedPayload.Length;
                ExpectedPayload.AsSpan(0, 6).CopyTo(firstFrame.AsSpan(2));
                Enqueue(firstFrame);
            }

            return true;
        }

        private static byte[] CreatePayload()
        {
            var payload = new byte[118];
            payload[0] = 0x62;
            payload[1] = 0xF1;
            payload[2] = 0x90;
            for (var i = 3; i < payload.Length; i++)
                payload[i] = (byte)(i & 0xFF);
            return payload;
        }

        private void Enqueue(byte[] data)
        {
            _incoming.Enqueue(VolvoJ2534.App.CanDecoder.Encode(0x7E8, data));
            _incomingReady.Set();
        }

        public void Unload() { }
    }


    [Fact]
    public void Request_RejectsSkippedConsecutiveFrameSequenceNumber()
    {
        AssertBadConsecutiveFrameSequence(duplicate: false);
    }

    [Fact]
    public void Request_RejectsDuplicateConsecutiveFrameSequenceNumber()
    {
        AssertBadConsecutiveFrameSequence(duplicate: true);
    }

    private static void AssertBadConsecutiveFrameSequence(bool duplicate)
    {
        var adapter = new InvalidConsecutiveSequenceAdapter(duplicate);
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var error = Assert.Throws<InvalidOperationException>(() =>
            channel.Request(new byte[] { 0x22, 0xF1, 0x90 }));

        Assert.Contains("sequence mismatch", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class InvalidConsecutiveSequenceAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private readonly bool _duplicate;

        internal InvalidConsecutiveSequenceAdapter(bool duplicate) => _duplicate = duplicate;

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
                if (_duplicate)
                {
                    Enqueue(new byte[] { 0x21, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16 });
                    Enqueue(new byte[] { 0x21, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D });
                }
                else
                {
                    // The receiver expects sequence 1; sequence 2 skips a CF.
                    Enqueue(new byte[] { 0x22, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16 });
                }
            }
            else if ((frame.Data[0] >> 4) == 0)
            {
                // Declared length 20 requires more than one consecutive frame.
                Enqueue(new byte[] { 0x10, 0x14, 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03 });
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



    [Fact]
    public void Request_TimeoutBetweenConsecutiveFramesAllowsNextTransaction()
    {
        var adapter = new ConsecutiveFrameTimeoutAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(300),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(120),
                RequestTimeout: TimeSpan.FromMilliseconds(900)));

        Assert.Throws<TimeoutException>(() =>
            channel.Request(new byte[] { 0x22, 0xF1, 0x90 }));

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x91 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91 }, response);
        Assert.Equal(2, adapter.RequestCount);
    }

    private sealed class ConsecutiveFrameTimeoutAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _requestCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);

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
                // Send one valid CF, then stall so the inter-CF timer expires.
                Enqueue(new byte[] { 0x21, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47 });
            }
            else if ((frame.Data[0] >> 4) == 0)
            {
                if (Interlocked.Increment(ref _requestCount) == 1)
                    Enqueue(new byte[] { 0x10, 0x14, 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03 });
                else
                {
                    // The old exchange's next CF arrives late before the new SF.
                    Enqueue(new byte[] { 0x22, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E });
                    Enqueue(new byte[] { 0x03, 0x62, 0xF1, 0x91 });
                }
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


    [Fact]
    public void Request_ContinuesAfterContinueToSendFlowControl()
    {
        var adapter = new OutboundFlowControlAdapter(waitBeforeContinue: false, overflow: false);
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FlowControlTimeout: TimeSpan.FromMilliseconds(300),
                RequestTimeout: TimeSpan.FromSeconds(1)));

        var response = channel.Request(new byte[] { 0x2E, 0xF1, 0x90, 0x01, 0x02, 0x03, 0x04, 0x05 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x90 }, response);
        Assert.Equal(1, adapter.ConsecutiveFramesSent);
    }

    [Fact]
    public void Request_RetriesFlowControlAfterWaitStatus()
    {
        var adapter = new OutboundFlowControlAdapter(waitBeforeContinue: true, overflow: false);
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FlowControlTimeout: TimeSpan.FromMilliseconds(300),
                RequestTimeout: TimeSpan.FromSeconds(1)));

        var response = channel.Request(new byte[] { 0x2E, 0xF1, 0x90, 0x01, 0x02, 0x03, 0x04, 0x05 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x90 }, response);
        Assert.Equal(1, adapter.ConsecutiveFramesSent);
        Assert.Equal(2, adapter.FlowControlFramesSent);
    }

    [Fact]
    public void Request_ThrowsWhenReceiverReportsFlowControlOverflow()
    {
        var adapter = new OutboundFlowControlAdapter(waitBeforeContinue: false, overflow: true);
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FlowControlTimeout: TimeSpan.FromMilliseconds(300),
                RequestTimeout: TimeSpan.FromSeconds(1)));

        var error = Assert.Throws<InvalidOperationException>(() =>
            channel.Request(new byte[] { 0x2E, 0xF1, 0x90, 0x01, 0x02, 0x03, 0x04, 0x05 }));

        Assert.Contains("Overflow", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, adapter.ConsecutiveFramesSent);
    }

    private sealed class OutboundFlowControlAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private readonly bool _waitBeforeContinue;
        private readonly bool _overflow;
        private int _flowControlFramesSent;
        private int _consecutiveFramesSent;

        internal OutboundFlowControlAdapter(bool waitBeforeContinue, bool overflow)
        {
            _waitBeforeContinue = waitBeforeContinue;
            _overflow = overflow;
        }

        internal int FlowControlFramesSent => Volatile.Read(ref _flowControlFramesSent);
        internal int ConsecutiveFramesSent => Volatile.Read(ref _consecutiveFramesSent);

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

            var pciType = frame.Data[0] >> 4;
            if (pciType == 1)
            {
                if (_overflow)
                {
                    Enqueue(new byte[] { 0x32, 0x00, 0x00 });
                    Interlocked.Increment(ref _flowControlFramesSent);
                }
                else
                {
                    if (_waitBeforeContinue)
                    {
                        Enqueue(new byte[] { 0x31, 0x00, 0x00 });
                        Interlocked.Increment(ref _flowControlFramesSent);
                    }

                    Enqueue(new byte[] { 0x30, 0x00, 0x00 });
                    Interlocked.Increment(ref _flowControlFramesSent);
                }
            }
            else if (pciType == 2)
            {
                Interlocked.Increment(ref _consecutiveFramesSent);
                Enqueue(new byte[] { 0x03, 0x62, 0xF1, 0x90 });
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


    [Fact]
    public void Request_RejectsFlowControlWaitBeyondConfiguredLimit()
    {
        var adapter = new FlowControlBoundaryAdapter(FlowControlMode.TooManyWaits);
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                MaxWaitFlowControls: 1,
                FlowControlTimeout: TimeSpan.FromMilliseconds(250),
                RequestTimeout: TimeSpan.FromSeconds(1)));

        var error = Assert.Throws<TimeoutException>(() =>
            channel.Request(new byte[] { 0x2E, 0xF1, 0x90, 1, 2, 3, 4, 5 }));

        Assert.Contains("WAIT limit", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, adapter.ConsecutiveFramesSent);
    }

    [Fact]
    public void Request_TimesOutWhenFlowControlNeverArrives()
    {
        var adapter = new FlowControlBoundaryAdapter(FlowControlMode.NoResponse);
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FlowControlTimeout: TimeSpan.FromMilliseconds(100),
                RequestTimeout: TimeSpan.FromMilliseconds(500)));

        Assert.Throws<TimeoutException>(() =>
            channel.Request(new byte[] { 0x2E, 0xF1, 0x90, 1, 2, 3, 4, 5 }));

        Assert.Equal(0, adapter.ConsecutiveFramesSent);
    }

    [Fact]
    public void Request_RejectsReservedSeparationTimeInContinueToSend()
    {
        var adapter = new FlowControlBoundaryAdapter(FlowControlMode.ReservedStmin);
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FlowControlTimeout: TimeSpan.FromMilliseconds(250),
                RequestTimeout: TimeSpan.FromSeconds(1)));

        var error = Assert.Throws<InvalidOperationException>(() =>
            channel.Request(new byte[] { 0x2E, 0xF1, 0x90, 1, 2, 3, 4, 5 }));

        Assert.Contains("Reserved ISO-TP STmin", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, adapter.ConsecutiveFramesSent);
    }

    private enum FlowControlMode
    {
        TooManyWaits,
        NoResponse,
        ReservedStmin
    }

    private sealed class FlowControlBoundaryAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private readonly FlowControlMode _mode;
        private int _consecutiveFramesSent;

        internal FlowControlBoundaryAdapter(FlowControlMode mode) => _mode = mode;
        internal int ConsecutiveFramesSent => Volatile.Read(ref _consecutiveFramesSent);

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

            var frameType = frame.Data[0] >> 4;
            if (frameType == 2)
            {
                Interlocked.Increment(ref _consecutiveFramesSent);
                return true;
            }

            if (frameType != 1)
                return true;

            switch (_mode)
            {
                case FlowControlMode.TooManyWaits:
                    Enqueue(new byte[] { 0x31, 0x00, 0x00 });
                    Enqueue(new byte[] { 0x31, 0x00, 0x00 });
                    break;
                case FlowControlMode.ReservedStmin:
                    Enqueue(new byte[] { 0x30, 0x00, 0x80 });
                    break;
                case FlowControlMode.NoResponse:
                    break;
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


    [Fact]
    public void UdsClient_LateNegativeResponseForDifferentServiceDoesNotPoisonNextRequest()
    {
        var adapter = new LateNegativeResponseAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(100),
                RequestTimeout: TimeSpan.FromMilliseconds(300)));
        using var client = new VolvoJ2534.App.UdsClient(channel);

        Assert.Throws<TimeoutException>(() => client.ReadDtcByStatusMask());

        var response = client.ReadDataByIdentifier(0xF191);

        Assert.Equal(new byte[] { 0xF1, 0x91, 0x42 }, response);
        Assert.Equal(2, adapter.RequestCount);
    }

    [Fact]
    public void UdsClient_LateResponseForTimedOutDidDoesNotPoisonNextRequest()
    {
        var adapter = new LateDidResponseAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(100),
                RequestTimeout: TimeSpan.FromMilliseconds(300)));
        using var client = new VolvoJ2534.App.UdsClient(channel);

        Assert.Throws<TimeoutException>(() => client.ReadDataByIdentifier(0xF190));

        var response = client.ReadDataByIdentifier(0xF191);

        Assert.Equal(new byte[] { 0xF1, 0x91, 0x42 }, response);
        Assert.Equal(2, adapter.RequestCount);
    }

    private sealed class LateNegativeResponseAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _requestCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);

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

            if ((frame.Data[0] >> 4) != 0)
                return true;

            if (Interlocked.Increment(ref _requestCount) == 2)
            {
                // A delayed negative response for service 0x19 has no relation
                // to the active 0x22 request and must be ignored.
                Enqueue(new byte[] { 0x03, 0x7F, 0x19, 0x31 });
                Enqueue(new byte[] { 0x04, 0x62, 0xF1, 0x91, 0x42 });
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

    private sealed class LateDidResponseAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _requestCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);

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

            if ((frame.Data[0] >> 4) != 0)
                return true;

            if (Interlocked.Increment(ref _requestCount) == 2)
            {
                // A delayed response to the previous DID arrives before the
                // valid response to this request. UDS correlation must discard it.
                Enqueue(new byte[] { 0x04, 0x62, 0xF1, 0x90, 0x11 });
                Enqueue(new byte[] { 0x04, 0x62, 0xF1, 0x91, 0x42 });
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

}
