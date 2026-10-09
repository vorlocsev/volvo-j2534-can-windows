namespace VolvoJ2534.App.Tests;

public sealed class IsoTpTests
{
    [Fact]
    public void Segment_SingleFrame_RoundTrips()
    {
        var payload = new byte[] { 0x22, 0xF1, 0x90 };

        var frames = VolvoJ2534.App.IsoTp.Segment(payload);

        Assert.Single(frames);
        Assert.Equal(new byte[] { 0x03, 0x22, 0xF1, 0x90, 0, 0, 0, 0 }, frames[0]);

        var can = new VolvoJ2534.App.CanFrame(0x7E0, false, false, frames[0], 0, 0);
        Assert.True(VolvoJ2534.App.IsoTp.TryDecode(can, out var decoded, out var error), error);
        Assert.Equal(VolvoJ2534.App.IsoTpFrameType.SingleFrame, decoded.Type);
        Assert.Equal(payload, decoded.Data);
    }

    [Fact]
    public void Segment_MultiFrame_Reassembles()
    {
        var payload = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();

        var frames = VolvoJ2534.App.IsoTp.Segment(payload);
        Assert.Equal(3, frames.Count);
        Assert.Equal(0x10, frames[0][0] & 0xF0);
        Assert.Equal(0x21, frames[1][0]);
        Assert.Equal(0x22, frames[2][0]);

        var reassembler = new VolvoJ2534.App.IsoTpReassembler();
        byte[]? result = null;

        foreach (var bytes in frames)
        {
            var can = new VolvoJ2534.App.CanFrame(0x7E8, false, false, bytes, 0, 0);
            Assert.True(reassembler.Push(can, out var completed, out var error), error);
            if (completed is not null)
                result = completed;
        }

        Assert.Equal(payload, result);
        Assert.False(reassembler.InProgress);
    }

    [Fact]
    public void Reassembler_RejectsSequenceMismatch()
    {
        var payload = Enumerable.Range(0, 12).Select(i => (byte)i).ToArray();
        var frames = VolvoJ2534.App.IsoTp.Segment(payload);

        var reassembler = new VolvoJ2534.App.IsoTpReassembler();
        var first = new VolvoJ2534.App.CanFrame(0x7E8, false, false, frames[0], 0, 0);
        Assert.True(reassembler.Push(first, out _, out var firstError), firstError);

        var wrong = (byte[])frames[1].Clone();
        wrong[0] = 0x22;
        var second = new VolvoJ2534.App.CanFrame(0x7E8, false, false, wrong, 0, 0);

        Assert.False(reassembler.Push(second, out _, out var error));
        Assert.Contains("sequence mismatch", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(reassembler.InProgress);
    }

    [Fact]
    public void ChannelOptions_RejectInvalidStandardCanId()
    {
        var options = new VolvoJ2534.App.IsoTpChannel.Options(0x800, 0x7E8);

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public void ChannelOptions_RejectNegativeTimeout()
    {
        var options = new VolvoJ2534.App.IsoTpChannel.Options(
            0x7E0, 0x7E8, RequestTimeout: TimeSpan.FromMilliseconds(-1));

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(0x80)]
    [InlineData(0xF0)]
    [InlineData(0xFA)]
    [InlineData(0xFF)]
    public void ChannelOptions_RejectReservedRxSeparationTime(int stmin)
    {
        var options = new VolvoJ2534.App.IsoTpChannel.Options(
            0x7E0, 0x7E8, RxSeparationTime: (byte)stmin);

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void ChannelOptions_AcceptsValid29BitCanIds()
    {
        var options = new VolvoJ2534.App.IsoTpChannel.Options(
            0x18DA10F1, 0x18DAF110, CanExtendedId: true);

        options.Validate();
    }

    [Fact]
    public void CreateFlowControl_EncodesFields()
    {
        var frame = VolvoJ2534.App.IsoTp.CreateFlowControl(0, 8, 0x0A);

        Assert.Equal(new byte[] { 0x30, 0x08, 0x0A, 0, 0, 0, 0, 0 }, frame);
    }
}
