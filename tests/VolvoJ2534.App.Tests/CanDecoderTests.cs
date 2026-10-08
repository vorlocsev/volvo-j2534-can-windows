namespace VolvoJ2534.App.Tests;

public sealed unsafe class CanDecoderTests
{
    [Fact]
    public void EncodeDecode_StandardCanId()
    {
        var message = VolvoJ2534.App.CanDecoder.Encode(
            0x7E0,
            new byte[] { 0x02, 0x10, 0x03 });

        Assert.True(VolvoJ2534.App.CanDecoder.TryDecode(message, out var frame, out var error), error);
        Assert.Equal((uint)0x7E0, frame.ArbitrationId);
        Assert.False(frame.IsExtended);
        Assert.Equal(new byte[] { 0x02, 0x10, 0x03 }, frame.Data);
    }

    [Fact]
    public void EncodeDecode_ExtendedCanId()
    {
        const uint id = 0x18DAF110;
        var message = VolvoJ2534.App.CanDecoder.Encode(
            id,
            new byte[] { 0x03, 0x22, 0xF1, 0x90 },
            extended: true);

        Assert.True(VolvoJ2534.App.CanDecoder.TryDecode(message, out var frame, out var error), error);
        Assert.Equal(id, frame.ArbitrationId);
        Assert.True(frame.IsExtended);
        Assert.Equal(new byte[] { 0x03, 0x22, 0xF1, 0x90 }, frame.Data);
    }

    [Fact]
    public void Encode_RejectsInvalidStandardId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VolvoJ2534.App.CanDecoder.Encode(0x800, Array.Empty<byte>()));
    }

    [Fact]
    public void Decode_RejectsElevenBitIdWithoutExtendedFlag()
    {
        var message = new VolvoJ2534.App.J2534Native.PassthruMsg
        {
            ProtocolID = VolvoJ2534.App.J2534Native.ProtocolCan,
            DataSize = 4
        };

        message.Data[0] = 0x00;
        message.Data[1] = 0x00;
        message.Data[2] = 0x08;
        message.Data[3] = 0x00;

        Assert.False(
            VolvoJ2534.App.CanDecoder.TryDecode(message, out _, out var error));
        Assert.Contains("CAN_29BIT_ID", error);
    }

    [Fact]
    public void Decode_RejectsOversizedClassicCanMessage()
    {
        var message = new VolvoJ2534.App.J2534Native.PassthruMsg
        {
            ProtocolID = VolvoJ2534.App.J2534Native.ProtocolCan,
            DataSize = 13
        };

        Assert.False(
            VolvoJ2534.App.CanDecoder.TryDecode(message, out _, out var error));
        Assert.Contains("expected 4..12", error);
    }
}
