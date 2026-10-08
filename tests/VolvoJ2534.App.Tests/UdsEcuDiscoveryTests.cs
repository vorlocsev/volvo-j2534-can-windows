namespace VolvoJ2534.App.Tests;

public sealed class UdsEcuDiscoveryTests
{
    [Fact]
    public void CandidateRecord_MapsStandardResponseToRequest()
    {
        var candidate = new VolvoJ2534.App.UdsEcuCandidate(
            0x7E8, 0x7E0, false, 4, 8);

        Assert.Equal((uint)0x7E8, candidate.ResponseId);
        Assert.Equal((uint)0x7E0, candidate.RequestId);
        Assert.False(candidate.IsExtended);
        Assert.Equal(4, candidate.ResponseCount);
        Assert.Equal(8, candidate.MaxDataLength);
    }
    [Fact]
    public void GuessRequestId_MapsStandardResponse()
    {
        Assert.Equal(
            (uint)0x7E3,
            VolvoJ2534.App.UdsEcuDiscovery.GuessRequestId(0x7EB, false));
    }

    [Fact]
    public void GuessRequestId_Maps29BitFixedNormalResponse()
    {
        Assert.Equal(
            (uint)0x18DA10F1,
            VolvoJ2534.App.UdsEcuDiscovery.GuessRequestId(0x18DAF110, true));
    }

    [Fact]
    public void GuessRequestId_DoesNotInventUnknown29BitMapping()
    {
        Assert.Null(
            VolvoJ2534.App.UdsEcuDiscovery.GuessRequestId(0x18DB33F1, true));
    }

    [Fact]
    public void IsLikelyUdsResponse_DetectsPositiveAndNegativeResponses()
    {
        var positive = new VolvoJ2534.App.CanFrame(
            0x700, false, false, new byte[] { 0x03, 0x62, 0xF1, 0x90 }, 0, 0);
        var negative = new VolvoJ2534.App.CanFrame(
            0x701, false, false, new byte[] { 0x03, 0x7F, 0x22, 0x31 }, 0, 0);

        Assert.True(VolvoJ2534.App.UdsEcuDiscovery.IsLikelyUdsResponse(positive, out var positiveSid));
        Assert.Equal((byte)0x62, positiveSid);

        Assert.True(VolvoJ2534.App.UdsEcuDiscovery.IsLikelyUdsResponse(negative, out var negativeSid));
        Assert.Equal((byte)0x7F, negativeSid);
    }


}
