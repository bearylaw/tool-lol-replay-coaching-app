using RiftReview.Core.Matches;

namespace RiftReview.Core.Tests.Matches;

public sealed class ReplayFileNameTests
{
    [Theory]
    [InlineData("EUW1-7000000004.rofl", "EUW1", 7000000004)]
    [InlineData(@"C:\Replays\Players\KR-7123456789.rofl", "KR", 7123456789)]
    [InlineData("na1-5000000001.rofl", "NA1", 5000000001)]
    public void ReadsPlatformAndGameIdFromClientNames(string name, string platform, long gameId)
    {
        Assert.Equal(new ReplayFileName(platform, gameId), ReplayFileName.TryParse(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("game 3 vs blue.rofl")]
    [InlineData("EUW1-7000000004.rofl.bak")]
    [InlineData("EUW1-.rofl")]
    [InlineData("EUW1-7000000004 (1).rofl")]
    public void IgnoresOtherNames(string? name)
    {
        Assert.Null(ReplayFileName.TryParse(name));
    }
}
