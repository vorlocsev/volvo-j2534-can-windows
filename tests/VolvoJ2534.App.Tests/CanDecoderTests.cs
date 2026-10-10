namespace VolvoJ2534.App.Tests;

public sealed unsafe class CanDecoderTests
{
    [Theory]
    [InlineData("7E0", "7E0", true)]
    [InlineData("0x7e0", "7E0", true)]
    [InlineData("000", "0", true)]
    [InlineData("0", "7E0", false)]
    [InlineData("", "7E0", true)]
    [InlineData("not-hex", "7E0", false)]
    [InlineData("18DAF110", "18DAF110", true)]
    [InlineData("18DAF110", "18DAF111", false)]
    public void MatchesCanIdFilter_HandlesHexAndZeroCorrectly(string query, string frameId, bool expected)
    {
        Assert.Equal(expected, VolvoJ2534.App.MainWindow.MatchesCanIdFilter(query, frameId));
    }

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

        // Encode produces a transmit message; emulate the J2534 receive metadata.
        message.RxStatus = VolvoJ2534.App.CanDecoder.Can29BitId;
        Assert.True(VolvoJ2534.App.CanDecoder.TryDecode(message, out var frame, out var error), error);
        Assert.Equal(id, frame.ArbitrationId);
        Assert.True(frame.IsExtended);
        Assert.Equal(new byte[] { 0x03, 0x22, 0xF1, 0x90 }, frame.Data);
    }

    [Fact]
    public void EncodeDecode_ExtendedCanIdPreservesWidthForLowNumericId()
    {
        // A 29-bit CAN identifier can be numerically below 0x800.
        const uint id = 0x123;
        var message = VolvoJ2534.App.CanDecoder.Encode(
            id,
            new byte[] { 0x01, 0x22 },
            extended: true);

        // Encode produces a transmit message; emulate the J2534 receive metadata.
        message.RxStatus = VolvoJ2534.App.CanDecoder.Can29BitId;
        Assert.True(VolvoJ2534.App.CanDecoder.TryDecode(message, out var frame, out var error), error);
        Assert.Equal(id, frame.ArbitrationId);
        Assert.True(frame.IsExtended);
        Assert.Equal(new byte[] { 0x01, 0x22 }, frame.Data);
    }

    [Fact]
    public void Encode_RejectsInvalidStandardId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VolvoJ2534.App.CanDecoder.Encode(0x800, Array.Empty<byte>()));
    }

    [Fact]
    public void Decode_IgnoresTransmitFlagsWhenDeterminingReceivedIdWidth()
    {
        var message = VolvoJ2534.App.CanDecoder.Encode(
            0x7E8,
            new byte[] { 0x03, 0x7F, 0x22, 0x31 });

        // TxFlags are not the source of identifier-width metadata for RX frames.
        message.TxFlags = VolvoJ2534.App.CanDecoder.Can29BitId;

        Assert.True(VolvoJ2534.App.CanDecoder.TryDecode(message, out var frame, out var error), error);
        Assert.Equal((uint)0x7E8, frame.ArbitrationId);
        Assert.False(frame.IsExtended);
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
