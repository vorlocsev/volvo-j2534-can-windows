namespace VolvoJ2534.App.Tests;

public sealed class VirtualJ2534AdapterTests
{
    [Fact]
    public void VirtualAdapter_RunsUdsSingleFrameRequestThroughRealCanAndIsoTpStack()
    {
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            // Emulate a read-only ECU response to ReadDataByIdentifier (DID F190).
            if (request.ArbitrationId == 0x7E0 &&
                request.Data.Length >= 4 &&
                request.Data[1] == 0x22 &&
                request.Data[2] == 0xF1 &&
                request.Data[3] == 0x90)
            {
                return new[]
                {
                    new VolvoJ2534.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x03, 0x62, 0xF1, 0x90 },
                        0, 0)
                };
            }

            return Array.Empty<VolvoJ2534.App.CanFrame>();
        });

        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();
        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0, 0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x90 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x90 }, response);
        Assert.Equal(1, adapter.TransmittedFrames);
    }
}
