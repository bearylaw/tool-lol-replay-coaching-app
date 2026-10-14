using RiftReview.App.Settings;

namespace RiftReview.App.Tests;

public sealed class SettingsEditorTests
{
    private static readonly AppSettings Changed = new()
    {
        OwnReplaysFolder = @"C:\Own",
        PlayerReplaysFolder = @"C:\Players",
        Roster = ["A#1"],
        InterfaceScale = 1.15,
        Overlay = new OverlaySettings
        {
            Scale = 1.3,
            HideLeagueControls = false,
            HideLeagueCameraPanel = true,
            DirectedCamera = false,
            PauseWhileNoting = true,
            PauseWhenDrawing = false,
            ClearDrawingsOnJump = false,
            DrawingFadeSeconds = 12,
            ShowOwnTimeline = false,
        },
    };

    [Fact]
    public void SavesEveryValueItWasOpenedWith()
    {
        var saved = new SettingsEditor(Changed).ApplyTo(Changed);

        Assert.Equal(Changed.InterfaceScale, saved.InterfaceScale, 6);
        Assert.Equal(Changed.Overlay, saved.Overlay);
        Assert.Equal(Changed.Roster, saved.Roster);
    }

    [Fact]
    public void ResetRestoresTheDefaultsButKeepsFoldersAndRoster()
    {
        var editor = new SettingsEditor(Changed);

        editor.ResetToDefaults();
        var saved = editor.ApplyTo(Changed);

        Assert.Equal(new OverlaySettings(), saved.Overlay);
        Assert.Equal(1, saved.InterfaceScale);
        Assert.Equal(Changed.PlayerReplaysFolder, saved.PlayerReplaysFolder);
        Assert.Equal(Changed.Roster, saved.Roster);
    }

    [Fact]
    public void ARangeSnapsToItsStepAndLimits()
    {
        var row = new RangeRow("Size", string.Empty, 100, (50, 200, 10), v => v.ToString(System.Globalization.CultureInfo.InvariantCulture));

        row.Value = 133;
        Assert.Equal(130, row.Value);
        row.Value = 999;
        Assert.Equal(200, row.Value);
        Assert.Equal("200", row.Text);
    }

    [Fact]
    public void ARangeIsGreyedOutWhileItsSwitchIsOff()
    {
        var toggle = new ToggleRow("Paths", string.Empty, isOn: true);
        var row = new RangeRow("Length", string.Empty, 8, (2, 30, 1), v => string.Empty, toggle);
        var changes = new List<string?>();
        row.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        toggle.IsOn = false;

        Assert.False(row.IsEnabled);
        Assert.Contains(nameof(RangeRow.IsEnabled), changes);
    }

    [Fact]
    public void AChoiceKeepsExactlyOneChipSelected()
    {
        var row = new ChoiceRow("Speed", string.Empty, [("2×", 2), ("4×", 4), ("8×", 8)], 8);

        row.Options[0].IsSelected = true;
        row.Options[1].IsSelected = false;

        Assert.Equal(2, row.Value);
        Assert.Single(row.Options, o => o.IsSelected);
    }

    [Fact]
    public void AChoiceStartsAtTheNearestOption()
    {
        var row = new ChoiceRow("Step", string.Empty, [("5 s", 5), ("10 s", 10), ("30 s", 30)], 12);

        Assert.Equal(10, row.Value);
    }
}
