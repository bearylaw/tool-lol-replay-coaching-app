using RiftReview.Core.Matches;

namespace RiftReview.Core.Tests.Matches;

public sealed class GameVersionTests
{
    [Fact]
    public void ParsesTheFourPartVersionWrittenIntoReplays()
    {
        var version = GameVersion.TryParse("16.20.824.8524");

        Assert.Equal(new GameVersion(16, 20, 824, 8524), version);
        Assert.Equal("16.20", version?.Patch);
        Assert.Equal("16.20.824.8524", version?.ToString());
    }

    [Fact]
    public void FillsMissingPartsWithZero()
    {
        Assert.Equal(new GameVersion(16, 20, 1, 0), GameVersion.TryParse("16.20.1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("16")]
    [InlineData("16.20.824.8524.1")]
    [InlineData("16.x.1")]
    [InlineData("-1.20")]
    [InlineData("16. 20")]
    public void RejectsTextThatIsNotAVersion(string? text)
    {
        Assert.Null(GameVersion.TryParse(text));
    }

    [Fact]
    public void ComparesPatchesWithoutBuildNumbers()
    {
        var replay = new GameVersion(16, 20, 824, 8524);

        Assert.True(replay.IsSamePatch(new GameVersion(16, 20, 900, 1)));
        Assert.False(replay.IsSamePatch(new GameVersion(16, 19, 824, 8524)));
    }
}
