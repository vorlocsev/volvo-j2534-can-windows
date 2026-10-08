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
}
