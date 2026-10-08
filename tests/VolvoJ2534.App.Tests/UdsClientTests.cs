namespace VolvoJ2534.App.Tests;

public sealed class UdsClientTests
{
    [Fact]
    public void ParsePositiveResponse_ReturnsPayload()
    {
        var payload = VolvoJ2534.App.UdsClient.ParsePositiveResponse(
            0x22,
            new byte[] { 0x62, 0xF1, 0x90, 0x31, 0x32, 0x33 });

        Assert.Equal(new byte[] { 0xF1, 0x90, 0x31, 0x32, 0x33 }, payload);
    }

    [Fact]
    public void ParsePositiveResponse_ThrowsNrc()
    {
        var ex = Assert.Throws<VolvoJ2534.App.UdsNegativeResponseException>(() =>
            VolvoJ2534.App.UdsClient.ParsePositiveResponse(
                0x22,
                new byte[] { 0x7F, 0x22, 0x31 }));

        Assert.Equal((byte)0x22, ex.RequestedService);
        Assert.Equal((byte)0x31, ex.NegativeResponseCode);
    }

    [Fact]
    public void ParsePositiveResponse_RejectsWrongPositiveSid()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            VolvoJ2534.App.UdsClient.ParsePositiveResponse(
                0x22,
                new byte[] { 0x59, 0x02 }));

        Assert.Contains("expected 0x62", ex.Message);
    }

    [Fact]
    public void ParseDtcResponse_ParsesRecords()
    {
        var records = VolvoJ2534.App.UdsClient.ParseDtcResponse(
            new byte[]
            {
                0x02, 0xFF,
                0x12, 0x34, 0x56, 0x2A,
                0xAB, 0xCD, 0xEF, 0x08
            });

        Assert.Equal(2, records.Count);
        Assert.Equal((uint)0x123456, records[0].Code);
        Assert.Equal((byte)0x2A, records[0].Status);
        Assert.Equal((uint)0xABCDEF, records[1].Code);
        Assert.Equal((byte)0x08, records[1].Status);
    }

    [Fact]
    public void ParseDtcResponse_RejectsMalformedRecordLength()
    {
        Assert.Throws<InvalidOperationException>(() =>
            VolvoJ2534.App.UdsClient.ParseDtcResponse(
                new byte[] { 0x02, 0xFF, 0x12, 0x34 }));
    }
}
