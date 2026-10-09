namespace VolvoJ2534.App.Tests;

public sealed class CanBusTests
{
    [Fact]
    public unsafe void SendEncodesStandardCanFrameAndWritesThroughAdapter()
    {
        var adapter = new FakeJ2534Adapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        var payload = new byte[] { 0x22, 0xF1, 0x90 };

        Assert.True(bus.Send(
            0x7E0,
            payload,
            extended: false,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None,
            out var error));

        Assert.Equal(string.Empty, error);
        Assert.Equal(1, adapter.WriteCount);
        Assert.Equal(VolvoJ2534.App.J2534Native.ProtocolCan, adapter.LastMessage.ProtocolID);
        Assert.Equal(0u, adapter.LastMessage.TxFlags);
        Assert.Equal(7u, adapter.LastMessage.DataSize);

        var message = adapter.LastMessage;
        fixed (byte* data = message.Data)
        {
            Assert.Equal(new byte[] { 0x00, 0x00, 0x07, 0xE0, 0x22, 0xF1, 0x90 },
                new byte[] { data[0], data[1], data[2], data[3], data[4], data[5], data[6] });
        }
    }

    [Fact]
    public void SendRejectsClassicCanPayloadLongerThanEightBytes()
    {
        var adapter = new FakeJ2534Adapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);

        Assert.Throws<ArgumentOutOfRangeException>(() => bus.Send(
            0x7E0,
            new byte[9],
            extended: false,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None,
            out _));
        Assert.Equal(0, adapter.WriteCount);
    }

    private sealed class FakeJ2534Adapter : VolvoJ2534.App.IJ2534Adapter
    {
        internal int WriteCount { get; private set; }
        internal VolvoJ2534.App.J2534Native.PassthruMsg LastMessage { get; private set; }

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }
        public bool Read(out VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            msg = default;
            error = string.Empty;
            Thread.Sleep((int)Math.Min(timeout, 5));
            return false;
        }

        public bool Write(in VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            LastMessage = msg;
            WriteCount++;
            error = string.Empty;
            return true;
        }

        public void Unload() { }
    }
}
