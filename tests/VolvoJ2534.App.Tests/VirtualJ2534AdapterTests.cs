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

    [Fact]
    public void VirtualAdapter_IgnoresFramesFromOtherEcuWhileWaitingForResponse()
    {
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 ||
                request.Data.Length < 4 ||
                request.Data[1] != 0x22 ||
                request.Data[2] != 0xF1 ||
                request.Data[3] != 0x90)
                return Array.Empty<VolvoJ2534.App.CanFrame>();

            // Another ECU responds first; only the configured response ID is valid.
            return new[]
            {
                new VolvoJ2534.App.CanFrame(
                    0x7E9, false, false,
                    new byte[] { 0x03, 0x62, 0xF1, 0x90 },
                    0, 0),
                new VolvoJ2534.App.CanFrame(
                    0x7E8, false, false,
                    new byte[] { 0x03, 0x62, 0xF1, 0x90 },
                    0, 0)
            };
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

    [Fact]
    public void VirtualAdapter_ReassemblesMultiFrameUdsResponseAndSendsFlowControl()
    {
        var flowControlFrames = 0;
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 || request.Data.Length == 0)
                return Array.Empty<VolvoJ2534.App.CanFrame>();

            // ECU starts a 10-byte ISO-TP response. The tester must acknowledge
            // the First Frame with Flow Control before the ECU sends its CF.
            if (request.Data[0] == 0x03 &&
                request.Data.Length >= 4 &&
                request.Data[1] == 0x22 &&
                request.Data[2] == 0xF1 &&
                request.Data[3] == 0x90)
            {
                return new[]
                {
                    new VolvoJ2534.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x10, 0x0A, 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03 },
                        0, 0)
                };
            }

            if ((request.Data[0] >> 4) == 0x3)
            {
                Interlocked.Increment(ref flowControlFrames);
                return new[]
                {
                    new VolvoJ2534.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x21, 0x04, 0x05, 0x06, 0x07, 0x00, 0x00, 0x00 },
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
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x90 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 }, response);
        Assert.Equal(1, Volatile.Read(ref flowControlFrames));
        Assert.Equal(2, adapter.TransmittedFrames);
    }

    [Fact]
    public void VirtualAdapter_RejectsOutOfOrderConsecutiveFrame()
    {
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 || request.Data.Length == 0)
                return Array.Empty<VolvoJ2534.App.CanFrame>();

            if (request.Data[0] == 0x03 && request.Data.Length >= 4 &&
                request.Data[1] == 0x22 && request.Data[2] == 0xF1 && request.Data[3] == 0x90)
            {
                return new[]
                {
                    new VolvoJ2534.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x10, 0x0A, 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03 },
                        0, 0)
                };
            }

            if ((request.Data[0] >> 4) == 0x3)
            {
                // The expected CF sequence number is 1; 2 must be rejected.
                return new[]
                {
                    new VolvoJ2534.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x22, 0x04, 0x05, 0x06, 0x07, 0x00, 0x00, 0x00 },
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
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var error = Assert.Throws<InvalidOperationException>(() =>
            channel.Request(new byte[] { 0x22, 0xF1, 0x90 }));

        Assert.Contains("sequence mismatch", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, adapter.TransmittedFrames);
    }

    [Fact]
    public void VirtualAdapter_ReportsTimeoutWhenEcuDoesNotRespond()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<VolvoJ2534.App.CanFrame>());
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();
        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0, 0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(80),
                RequestTimeout: TimeSpan.FromMilliseconds(250)));

        Assert.Throws<TimeoutException>(() =>
            channel.Request(new byte[] { 0x22, 0xF1, 0x90 }));
        Assert.Equal(1, adapter.TransmittedFrames);
    }
}
