namespace VolvoJ2534.App.Tests;

public sealed class CanDecoderTests
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
}
