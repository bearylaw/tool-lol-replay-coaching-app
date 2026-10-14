namespace RiftReview.Capture.Tests;

public sealed class ReplayRemoteTests
{
    // Checked on a running 16.20 replay: interfaceReplay is the bottom-left panel with the camera
    // mode and the fog of war team choice, interfaceTimeline the play controls and timeline.
    [Fact]
    public void TheTimelineAndTheCameraPanelAreSeparateParts()
    {
        Assert.Equal(["interfaceTimeline"], ReplayRemote.RenderNames(LeagueInterface.ReplayControls));
        Assert.Equal(["interfaceReplay"], ReplayRemote.RenderNames(LeagueInterface.CameraPanel));
    }

    [Fact]
    public void EveryPartHasARenderName()
    {
        Assert.All(Enum.GetValues<LeagueInterface>(), part => Assert.NotEmpty(ReplayRemote.RenderNames(part)));
    }
}
