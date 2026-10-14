using RiftReview.Core.Matches;

namespace RiftReview.Launch.Tests;

public sealed class PlaybackCompatibilityTests
{
    private static readonly GameVersion Replay = new(16, 20, 824, 8524);

    [Fact]
    public void SameBuildIsCompatible()
    {
        Assert.Equal(Compatibility.Compatible, PlaybackCompatibility.Check(Replay, new GameVersion(16, 20, 824, 8524)));
    }

    [Fact]
    public void AHotfixBuildOfTheSamePatchIsFlagged()
    {
        Assert.Equal(Compatibility.DifferentBuild, PlaybackCompatibility.Check(Replay, new GameVersion(16, 20, 830, 1)));
    }

    [Fact]
    public void AnotherPatchIsIncompatible()
    {
        Assert.Equal(Compatibility.DifferentPatch, PlaybackCompatibility.Check(Replay, new GameVersion(16, 21, 824, 8524)));
    }

    [Fact]
    public void AnUnreadableInstalledVersionIsUnknown()
    {
        Assert.Equal(Compatibility.Unknown, PlaybackCompatibility.Check(Replay, null));
    }
}
