namespace RiftReview.Launch.Tests;

public sealed class GameConfigTests
{
    [Fact]
    public void AddsTheSettingAsFirstLineOfGeneralKeepingWindowsLineEndings()
    {
        var config = "[FloatingText]\r\nX=1\r\n[General]\r\nEnableScreenShake=0\r\n[HUD]\r\nY=2\r\n";

        var changed = GameConfig.WithReplayApi(config);

        Assert.Equal("[FloatingText]\r\nX=1\r\n[General]\r\nEnableReplayApi=1\r\nEnableScreenShake=0\r\n[HUD]\r\nY=2\r\n", changed);
        Assert.True(GameConfig.IsReplayApiEnabled(changed));
    }

    [Fact]
    public void LeavesAConfigThatAlreadyHasItUnchanged()
    {
        var config = "[General]\nEnableReplayApi=1\nWidth=3440\n";

        Assert.Same(config, GameConfig.WithReplayApi(config));
    }

    [Fact]
    public void ReplacesASwitchedOffSetting()
    {
        var changed = GameConfig.WithReplayApi("[General]\nEnableReplayApi=0\nWidth=3440\n");

        Assert.Equal("[General]\nEnableReplayApi=1\nWidth=3440\n", changed);
    }

    [Fact]
    public void AddsTheSectionWhenTheFileHasNone()
    {
        Assert.Equal("[HUD]\nY=2\n[General]\nEnableReplayApi=1\n", GameConfig.WithReplayApi("[HUD]\nY=2\n"));
    }

    [Fact]
    public void SwitchesOnTheDirectedCameraInTheReplaySectionOnly()
    {
        const string Config = "[General]\r\nEnableDirectedCamera=0\r\n[Replay]\r\nShowReplayScoreboard=0\r\nEnableDirectedCamera=0\r\n\r\n[Mobile]\r\nX=1\r\n";

        var changed = GameConfig.WithDirectedCamera(Config);

        Assert.Equal("[General]\r\nEnableDirectedCamera=0\r\n[Replay]\r\nShowReplayScoreboard=0\r\nEnableDirectedCamera=1\r\n\r\n[Mobile]\r\nX=1\r\n", changed);
        Assert.True(GameConfig.IsDirectedCameraEnabled(changed));
        Assert.False(GameConfig.IsDirectedCameraEnabled(Config));
        Assert.Same(changed, GameConfig.WithDirectedCamera(changed));
    }

    [Fact]
    public void AddsTheDirectedCameraToAReplaySectionWithoutIt()
    {
        Assert.Equal("[Replay]\nEnableDirectedCamera=1\nA=1\n", GameConfig.WithDirectedCamera("[Replay]\nA=1\n"));
        Assert.Equal("[General]\nA=1\n[Replay]\nEnableDirectedCamera=1\n", GameConfig.WithDirectedCamera("[General]\nA=1\n"));
    }

    [Fact]
    public void DoesNotMistakeOtherSettingsForIt()
    {
        Assert.False(GameConfig.IsReplayApiEnabled("[General]\nEnableReplayApiX=1\n"));
    }
}
