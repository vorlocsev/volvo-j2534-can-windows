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
    public void Dispatcher_RejectsStartAndSubscribeAfterDisposal()
    {
        using var j2534 = new VolvoJ2534.App.J2534Native();
        var dispatcher = new VolvoJ2534.App.CanRxDispatcher(j2534);
        dispatcher.Dispose();

        Assert.Throws<ObjectDisposedException>(() => dispatcher.Start());
        Assert.Throws<ObjectDisposedException>(() => dispatcher.Subscribe());
        dispatcher.Dispose();
    }

    private static VolvoJ2534.App.CanFrame Frame(uint id) =>
        new(id, false, false, new byte[] { 0x00 }, 0, 0);
}
