namespace VolvoJ2534.App.Tests;

public sealed class UdsClientTests
{

    [Fact]
    public void ParseVinData_ExtractsVinAfterDid()
    {
        var data = System.Text.Encoding.ASCII.GetBytes("F190YV1ABCDEF12345");
        // The DID is binary in a UDS response, followed by the 17 ASCII VIN characters.
        data[0] = 0xF1;
        data[1] = 0x90;

        Assert.Equal("YV1ABCDEF12345", VolvoJ2534.App.UdsClient.ParseVinData(data));
    }

    [Theory]
    [InlineData(new byte[] { 0x31, 0x32, 0x33 })]
    [InlineData(new byte[] { 0xF1, 0x90 })]
    [InlineData(new byte[] { 0xF1, 0x90, 0x59, 0x56, 0x31 })]
    public void ParseVinData_RejectsMissingDidOrWrongLength(byte[] data)
    {
        Assert.Throws<InvalidOperationException>(
            () => VolvoJ2534.App.UdsClient.ParseVinData(data));
    }

    [Fact]
    public void ParseVinData_RejectsNonAsciiVinCharacters()
    {
        var data = new byte[19];
        data[0] = 0xF1;
        data[1] = 0x90;
        Array.Fill(data, (byte)'A', 2, 17);
        data[8] = 0x20;

        Assert.Throws<InvalidOperationException>(
            () => VolvoJ2534.App.UdsClient.ParseVinData(data));
    }

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

    [Fact]
    public void ParsePositiveResponse_RejectsEmptyResponse()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            VolvoJ2534.App.UdsClient.ParsePositiveResponse(0x22, Array.Empty<byte>()));

        Assert.Contains("empty response", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParsePositiveResponse_RejectsNegativeResponseForDifferentService()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            VolvoJ2534.App.UdsClient.ParsePositiveResponse(
                0x22,
                new byte[] { 0x7F, 0x19, 0x31 }));

        Assert.Contains("refers to service", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParsePositiveResponse_RejectsTruncatedNegativeResponse()
    {
        Assert.Throws<InvalidOperationException>(() =>
            VolvoJ2534.App.UdsClient.ParsePositiveResponse(
                0x22,
                new byte[] { 0x7F, 0x22 }));
    }

    [Fact]
    public void ParseDtcResponse_AcceptsNoDtcRecords()
    {
        var records = VolvoJ2534.App.UdsClient.ParseDtcResponse(
            new byte[] { 0x02, 0xFF });

        Assert.Empty(records);
    }

    [Fact]
    public void ParseDtcResponse_RejectsUnexpectedSubfunction()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            VolvoJ2534.App.UdsClient.ParseDtcResponse(
                new byte[] { 0x01, 0xFF }));

        Assert.Contains("expected 0x02", ex.Message);
    }

    [Fact]
    public void ParseDtcResponse_RejectsResponseShorterThanHeader()
    {
        Assert.Throws<InvalidOperationException>(() =>
            VolvoJ2534.App.UdsClient.ParseDtcResponse(new byte[] { 0x02 }));
    }
}
