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

    private static VolvoJ2534.App.CanFrame Frame(uint id) =>
        new(id, false, false, new byte[] { 0x00 }, 0, 0);
}
