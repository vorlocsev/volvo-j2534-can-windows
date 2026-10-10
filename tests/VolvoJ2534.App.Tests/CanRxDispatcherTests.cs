namespace VolvoJ2534.App.Tests;

public sealed class CanRxDispatcherTests
{
    [Fact]
    public void Subscription_DropsOldestFrameWhenCapacityIsReached()
    {
        using var j2534 = new VolvoJ2534.App.J2534Native();
        using var dispatcher = new VolvoJ2534.App.CanRxDispatcher(j2534);
        using var subscription = dispatcher.Subscribe(2);

        subscription.Publish(Frame(0x100));
        subscription.Publish(Frame(0x101));
        subscription.Publish(Frame(0x102));

        Assert.True(subscription.TryRead(TimeSpan.Zero, CancellationToken.None, out var first));
        Assert.Equal((uint)0x101, first.ArbitrationId);

        Assert.True(subscription.TryRead(TimeSpan.Zero, CancellationToken.None, out var second));
        Assert.Equal((uint)0x102, second.ArbitrationId);

        Assert.False(subscription.TryRead(TimeSpan.Zero, CancellationToken.None, out _));
    }

    [Fact]
    public async Task Subscription_ConcurrentPublishAndRead_RemainsConsistentAfterOverflow()
    {
        using var j2534 = new VolvoJ2534.App.J2534Native();
        using var dispatcher = new VolvoJ2534.App.CanRxDispatcher(j2534);
        using var subscription = dispatcher.Subscribe(8);
        var consumed = new System.Collections.Concurrent.ConcurrentBag<uint>();
        var finished = 0;

        var reader = Task.Run(() =>
        {
            while (Volatile.Read(ref finished) == 0)
            {
                if (subscription.TryRead(TimeSpan.FromMilliseconds(1), CancellationToken.None, out var frame))
                    consumed.Add(frame.ArbitrationId);
            }

            while (subscription.TryRead(TimeSpan.Zero, CancellationToken.None, out var frame))
                consumed.Add(frame.ArbitrationId);
        });

        for (uint id = 0; id < 20_000; id++)
            subscription.Publish(Frame(id));

        Volatile.Write(ref finished, 1);
        await reader.WaitAsync(TimeSpan.FromSeconds(5));

        // A fresh full-capacity batch must remain readable in order after
        // concurrent overflow, with no stale permits or invisible queued items.
        for (uint id = 30_000; id < 30_008; id++)
            subscription.Publish(Frame(id));

        for (uint expected = 30_000; expected < 30_008; expected++)
        {
            Assert.True(subscription.TryRead(TimeSpan.Zero, CancellationToken.None, out var frame));
            Assert.Equal(expected, frame.ArbitrationId);
        }

        Assert.False(subscription.TryRead(TimeSpan.Zero, CancellationToken.None, out _));
        Assert.All(consumed, id => Assert.InRange(id, 0u, 19_999u));
    }

    [Fact]
    public async Task Subscription_ConcurrentReadersDoNotLoseAvailableFrames()
    {
        using var j2534 = new VolvoJ2534.App.J2534Native();
        using var dispatcher = new VolvoJ2534.App.CanRxDispatcher(j2534);
        using var subscription = dispatcher.Subscribe(1_000);

        for (uint id = 0; id < 1_000; id++)
            subscription.Publish(Frame(id));

        var consumed = new System.Collections.Concurrent.ConcurrentBag<uint>();
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (consumed.Count < 1_000)
            {
                if (subscription.TryRead(TimeSpan.FromMilliseconds(100), CancellationToken.None, out var frame))
                    consumed.Add(frame.ArbitrationId);
            }
        })).ToArray();

        await Task.WhenAll(readers).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1_000, consumed.Count);
        Assert.Equal(1_000, consumed.Distinct().Count());
        Assert.False(subscription.TryRead(TimeSpan.Zero, CancellationToken.None, out _));
    }

    [Fact]
    public void Subscription_TryReadHonorsCancellation()
    {
        using var j2534 = new VolvoJ2534.App.J2534Native();
        using var dispatcher = new VolvoJ2534.App.CanRxDispatcher(j2534);
        using var subscription = dispatcher.Subscribe();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            subscription.TryRead(TimeSpan.FromSeconds(1), cancellation.Token, out _));
    }

    [Fact]
    public void Subscription_ReturnsFalseAfterDisposal()
    {
        using var j2534 = new VolvoJ2534.App.J2534Native();
        using var dispatcher = new VolvoJ2534.App.CanRxDispatcher(j2534);
        var subscription = dispatcher.Subscribe();
        subscription.Dispose();

        Assert.False(subscription.TryRead(TimeSpan.Zero, CancellationToken.None, out _));
        subscription.Publish(Frame(0x100));
    }

    [Fact]
    public void Dispatcher_CanStopRestartAndDisposeRepeatedly()
    {
        using var j2534 = new VolvoJ2534.App.J2534Native();
        var dispatcher = new VolvoJ2534.App.CanRxDispatcher(j2534);

        dispatcher.Start();
        dispatcher.Stop();
        dispatcher.Start();
        dispatcher.Stop();
        dispatcher.Dispose();

        Assert.Throws<ObjectDisposedException>(() => dispatcher.Start());
    }

    [Fact]
    public void Dispatcher_RejectsStartAndSubscribeAfterDisposal()
    {
        using var j2534 = new VolvoJ2534.App.J2534Native();
        var dispatcher = new VolvoJ2534.App.CanRxDispatcher(j2534);
        dispatcher.Dispose();

        Assert.Throws<ObjectDisposedException>(() => dispatcher.Start());
        Assert.Throws<ObjectDisposedException>(() => dispatcher.Subscribe());
        dispatcher.Dispose();
    }

    [Fact]
    public void DispatcherDoesNotStartSecondReaderWhilePreviousReadIsStopping()
    {
        using var readEntered = new ManualResetEventSlim();
        using var allowReadToFinish = new ManualResetEventSlim();
        var adapter = new FakeJ2534Adapter
        {
            ReadEntered = readEntered,
            AllowReadToFinish = allowReadToFinish
        };
        using var dispatcher = new VolvoJ2534.App.CanRxDispatcher(adapter);

        dispatcher.Start();
        Assert.True(readEntered.Wait(TimeSpan.FromSeconds(2)), "Native read did not start.");

        dispatcher.Stop(); // bounded wait expires while the fake native read is blocked
        Assert.Throws<InvalidOperationException>(() => dispatcher.Start());
        Assert.Equal(1, adapter.MaxConcurrentReads);

        allowReadToFinish.Set();

        // Wait until the original worker exits before allowing a restart.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                dispatcher.Start();
                dispatcher.Stop();
                return;
            }
            catch (InvalidOperationException)
            {
                Thread.Sleep(10);
            }
        }

        Assert.Fail("The receive worker did not exit after the blocked read was released.");
    }


    [Fact]
    public void DispatcherPublishesFramesFromFakeAdapter()
    {
        var adapter = new FakeJ2534Adapter();
        using var dispatcher = new VolvoJ2534.App.CanRxDispatcher(adapter);
        using var subscription = dispatcher.Subscribe();

        dispatcher.Start();
        adapter.Enqueue(VolvoJ2534.App.CanDecoder.Encode(0x7E8, new byte[] { 0x03, 0x7F, 0x22, 0x31 }));

        Assert.True(subscription.TryRead(TimeSpan.FromSeconds(2), CancellationToken.None, out var frame));
        Assert.Equal((uint)0x7E8, frame.ArbitrationId);
        Assert.Equal(new byte[] { 0x03, 0x7F, 0x22, 0x31 }, frame.Data);
    }

    [Fact]
    public void DispatcherReportsReadErrorAndContinuesReceiving()
    {
        var adapter = new FakeJ2534Adapter();
        using var dispatcher = new VolvoJ2534.App.CanRxDispatcher(adapter);
        using var subscription = dispatcher.Subscribe();
        using var errorSeen = new ManualResetEventSlim();

        dispatcher.ReadError += _ => errorSeen.Set();
        adapter.FailNextRead();
        dispatcher.Start();

        Assert.True(errorSeen.Wait(TimeSpan.FromSeconds(2)));
        adapter.Enqueue(VolvoJ2534.App.CanDecoder.Encode(0x7E8, new byte[] { 0x01, 0x00 }));

        Assert.True(subscription.TryRead(TimeSpan.FromSeconds(2), CancellationToken.None, out var frame));
        Assert.Equal((uint)0x7E8, frame.ArbitrationId);
    }


    [Fact]
    public void DispatcherReportsThrownReadErrorAndContinuesReceivingAfterRecovery()
    {
        var adapter = new FakeJ2534Adapter();
        using var dispatcher = new VolvoJ2534.App.CanRxDispatcher(adapter);
        using var subscription = dispatcher.Subscribe();
        using var errorSeen = new ManualResetEventSlim();

        dispatcher.ReadError += error =>
        {
            if (error.Message.Contains("simulated adapter disconnect", StringComparison.Ordinal))
                errorSeen.Set();
        };

        adapter.ThrowNextRead();
        dispatcher.Start();

        Assert.True(errorSeen.Wait(TimeSpan.FromSeconds(2)),
            "The dispatcher did not report an exception thrown by the adapter.");

        adapter.Enqueue(VolvoJ2534.App.CanDecoder.Encode(0x7E8, new byte[] { 0x01, 0x00 }));
        Assert.True(subscription.TryRead(TimeSpan.FromSeconds(2), CancellationToken.None, out var frame));
        Assert.Equal((uint)0x7E8, frame.ArbitrationId);
    }

    [Fact]
    public async Task DispatcherBacksOffWhenAdapterContinuouslyReturnsReadErrors()
    {
        var adapter = new FakeJ2534Adapter { FailReadsContinuously = true };
        using var dispatcher = new VolvoJ2534.App.CanRxDispatcher(adapter);
        using var firstErrorSeen = new ManualResetEventSlim();
        var errorCount = 0;

        dispatcher.ReadError += _ =>
        {
            Interlocked.Increment(ref errorCount);
            firstErrorSeen.Set();
        };

        dispatcher.Start();
        Assert.True(firstErrorSeen.Wait(TimeSpan.FromSeconds(2)));
        await Task.Delay(350);
        dispatcher.Stop();

        // Immediate-error adapters must not spin at CPU speed. The dispatcher
        // backs off between failures, while still reporting recurring errors.
        Assert.InRange(Volatile.Read(ref errorCount), 2, 10);
    }

    [Fact]
    public async Task DispatcherBacksOffWhenAdapterReturnsEmptyReadsImmediately()
    {
        var adapter = new FakeJ2534Adapter { ReturnEmptyReadsContinuously = true };
        using var dispatcher = new VolvoJ2534.App.CanRxDispatcher(adapter);

        dispatcher.Start();
        await Task.Delay(100);
        dispatcher.Stop();

        // The adapter returns false with no error immediately; this still
        // must not turn the receive loop into a CPU-speed polling loop.
        Assert.InRange(adapter.ReadCount, 1, 100);
    }

    [Fact]
    public void DispatcherCanBeDisposedFromReadErrorCallback()
    {
        var adapter = new FakeJ2534Adapter();
        var dispatcher = new VolvoJ2534.App.CanRxDispatcher(adapter);
        using var callbackReturned = new ManualResetEventSlim();

        dispatcher.ReadError += _ =>
        {
            dispatcher.Dispose();
            callbackReturned.Set();
        };

        adapter.FailNextRead();
        dispatcher.Start();

        Assert.True(callbackReturned.Wait(TimeSpan.FromSeconds(2)),
            "Dispose deadlocked when called from the receive worker callback.");
        dispatcher.Dispose();
    }

    [Fact]
    public void DispatcherIsolatesThrowingReadErrorSubscriber()
    {
        var adapter = new FakeJ2534Adapter();
        using var dispatcher = new VolvoJ2534.App.CanRxDispatcher(adapter);
        using var errorSeen = new ManualResetEventSlim();

        dispatcher.ReadError += _ => throw new InvalidOperationException("simulated UI subscriber failure");
        dispatcher.ReadError += _ => errorSeen.Set();
        adapter.FailNextRead();
        dispatcher.Start();

        Assert.True(errorSeen.Wait(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void DispatcherThrottlesRepeatedDecodeErrors()
    {
        var adapter = new FakeJ2534Adapter();
        using var dispatcher = new VolvoJ2534.App.CanRxDispatcher(adapter);
        using var firstErrorSeen = new ManualResetEventSlim();
        using var secondErrorSeen = new ManualResetEventSlim();
        var errorCount = 0;

        dispatcher.ReadError += error =>
        {
            if (!error.Message.StartsWith("CAN decode error:", StringComparison.Ordinal))
                return;

            var count = Interlocked.Increment(ref errorCount);
            if (count == 1) firstErrorSeen.Set();
            if (count == 2) secondErrorSeen.Set();
        };

        var malformed = VolvoJ2534.App.CanDecoder.Encode(0x7E8, new byte[] { 0x01 });
        malformed.ProtocolID = 0xFFFFFFFF;
        dispatcher.Start();

        for (var i = 0; i < 5; i++)
            adapter.Enqueue(malformed);

        Assert.True(firstErrorSeen.Wait(TimeSpan.FromSeconds(2)));
        Thread.Sleep(100);
        Assert.Equal(1, Volatile.Read(ref errorCount));

        // The dispatcher emits at most one decode diagnostic per second.
        Thread.Sleep(1_000);
        adapter.Enqueue(malformed);
        Assert.True(secondErrorSeen.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(2, Volatile.Read(ref errorCount));
    }

    [Fact]
    public async Task DispatcherDisposeWaitsForAnInFlightAdapterRead()
    {
        using var readEntered = new ManualResetEventSlim();
        using var allowReadToFinish = new ManualResetEventSlim();
        var adapter = new BlockingReadAdapter(readEntered, allowReadToFinish);
        var dispatcher = new VolvoJ2534.App.CanRxDispatcher(adapter);

        dispatcher.Start();
        Assert.True(readEntered.Wait(TimeSpan.FromSeconds(2)), "Adapter read did not start.");

        var disposeTask = Task.Run(dispatcher.Dispose);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1_200));
            Assert.False(disposeTask.IsCompleted);
        }
        finally
        {
            allowReadToFinish.Set();
        }

        await disposeTask.WaitAsync(TimeSpan.FromSeconds(2));
        dispatcher.Dispose();
        Assert.Equal(1, adapter.ReadCount);
    }

    private sealed class FakeJ2534Adapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _frames = new();
        private int _failNextRead;
        private int _throwNextRead;
        private int _activeReads;
        private int _maxConcurrentReads;
        private int _readCount;
        internal int ReadCount => Volatile.Read(ref _readCount);

        internal ManualResetEventSlim? ReadEntered { get; init; }
        internal ManualResetEventSlim? AllowReadToFinish { get; init; }
        internal bool FailReadsContinuously { get; init; }
        internal bool ReturnEmptyReadsContinuously { get; init; }
        internal int MaxConcurrentReads => Volatile.Read(ref _maxConcurrentReads);

        internal void Enqueue(VolvoJ2534.App.J2534Native.PassthruMsg frame) => _frames.Enqueue(frame);
        internal void FailNextRead() => Interlocked.Exchange(ref _failNextRead, 1);
        internal void ThrowNextRead() => Interlocked.Exchange(ref _throwNextRead, 1);

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }
        public bool Write(in VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            return true;
        }
        public void Unload() { }

        public bool Read(out VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            Interlocked.Increment(ref _readCount);
            var active = Interlocked.Increment(ref _activeReads);
            UpdateMaxConcurrentReads(active);
            try
            {
                ReadEntered?.Set();
                AllowReadToFinish?.Wait();
                if (Interlocked.Exchange(ref _throwNextRead, 0) != 0)
                    throw new InvalidOperationException("simulated adapter disconnect");

                if (FailReadsContinuously)
                {
                    msg = default;
                    error = "simulated persistent adapter read error";
                    return false;
                }

                if (ReturnEmptyReadsContinuously)
                {
                    msg = default;
                    error = string.Empty;
                    return false;
                }

                if (Interlocked.Exchange(ref _failNextRead, 0) != 0)
                {
                    msg = default;
                    error = "simulated adapter read error";
                    return false;
                }

                if (_frames.TryDequeue(out msg))
            {
                error = string.Empty;
                return true;
            }

            Thread.Sleep((int)Math.Min(timeout, 10));
            msg = default;
            error = string.Empty;
            return false;
            }
            finally
            {
                Interlocked.Decrement(ref _activeReads);
            }
        }

        private void UpdateMaxConcurrentReads(int active)
        {
            var current = Volatile.Read(ref _maxConcurrentReads);
            while (active > current)
            {
                var observed = Interlocked.CompareExchange(ref _maxConcurrentReads, active, current);
                if (observed == current)
                    return;
                current = observed;
            }
        }
    }

    private sealed class BlockingReadAdapter(
        ManualResetEventSlim readEntered,
        ManualResetEventSlim allowReadToFinish) : VolvoJ2534.App.IJ2534Adapter
    {
        private int _readCount;
        internal int ReadCount => Volatile.Read(ref _readCount);

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }
        public bool Write(in VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            return true;
        }
        public void Unload() { }

        public bool Read(out VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            Interlocked.Increment(ref _readCount);
            readEntered.Set();
            allowReadToFinish.Wait();
            msg = default;
            error = string.Empty;
            return false;
        }
    }

    private static VolvoJ2534.App.CanFrame Frame(uint id) =>
        new(id, false, false, new byte[] { 0x00 }, 0, 0);
}
