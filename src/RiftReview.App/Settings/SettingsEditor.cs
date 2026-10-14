using System.Globalization;
using RiftReview.App.Localization;
using RiftReview.App.Mvvm;

namespace RiftReview.App.Settings;

/// <summary>
/// The display and overlay settings as rows for the settings window. Folders and the roster are
/// edited by the window itself; <see cref="ApplyTo"/> merges both.
/// </summary>
internal sealed class SettingsEditor : ObservableObject
{
    private Rows rows;

    public SettingsEditor(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        rows = new Rows(settings.Normalized());
    }

    public IReadOnlyList<SettingRow> GeneralRows => rows.General;

    public IReadOnlyList<SettingRow> OverlayRows => rows.Overlay;

    /// <summary>Puts every row back to its default; folders and roster are not rows and stay.</summary>
    public void ResetToDefaults()
    {
        rows = new Rows(new AppSettings { OwnReplaysFolder = string.Empty, PlayerReplaysFolder = string.Empty });
        OnPropertyChanged(nameof(GeneralRows));
        OnPropertyChanged(nameof(OverlayRows));
    }

    /// <summary><paramref name="settings"/> with the rows' values.</summary>
    public AppSettings ApplyTo(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return rows.ApplyTo(settings).Normalized();
    }

    private static string Percent(double value) => Loc.Format("Settings_Percent", value.ToString("0", CultureInfo.CurrentCulture));

    private static string Seconds(double value) => Loc.Format("Settings_Seconds", value.ToString("0", CultureInfo.CurrentCulture));

    // Built fresh from settings, so a reset is a new instance and nothing is copied field by field.
    private sealed class Rows
    {
        private readonly RangeRow interfaceScale;
        private readonly RangeRow overlayScale;
        private readonly ToggleRow hideLeagueControls;
        private readonly ToggleRow hideLeagueCameraPanel;
        private readonly ToggleRow directedCamera;
        private readonly ToggleRow pauseWhileNoting;
        private readonly ToggleRow pauseWhenDrawing;
        private readonly ToggleRow clearDrawingsOnJump;
        private readonly RangeRow drawingFade;
        private readonly ToggleRow showOwnScoreboard;

        public Rows(AppSettings settings)
        {
            interfaceScale = new RangeRow(Loc.Get("Settings_InterfaceScale"), Loc.Get("Settings_InterfaceScaleHint"), settings.InterfaceScale * 100, (80, 125, 5), Percent);
            General = [new HeadingRow(Loc.Get("Settings_Display")), interfaceScale];

            var overlay = settings.Overlay;
            overlayScale = new RangeRow(Loc.Get("Settings_OverlayScale"), Loc.Get("Settings_OverlayScaleHint"), overlay.Scale * 100, (60, 160, 10), Percent);
            showOwnScoreboard = new ToggleRow(Loc.Get("Settings_ShowOwnScoreboard"), Loc.Get("Settings_ShowOwnScoreboardHint"), overlay.ShowOwnScoreboard);
            hideLeagueControls = new ToggleRow(Loc.Get("Settings_HideLeagueControls"), Loc.Get("Settings_HideLeagueControlsHint"), overlay.HideLeagueControls);
            hideLeagueCameraPanel = new ToggleRow(Loc.Get("Settings_HideLeagueCameraPanel"), Loc.Get("Settings_HideLeagueCameraPanelHint"), overlay.HideLeagueCameraPanel);
            directedCamera = new ToggleRow(Loc.Get("Settings_DirectedCamera"), Loc.Get("Settings_DirectedCameraHint"), overlay.DirectedCamera);
            pauseWhileNoting = new ToggleRow(Loc.Get("Settings_PauseWhileNoting"), Loc.Get("Settings_PauseWhileNotingHint"), overlay.PauseWhileNoting);
            pauseWhenDrawing = new ToggleRow(Loc.Get("Settings_PauseWhenDrawing"), Loc.Get("Settings_PauseWhenDrawingHint"), overlay.PauseWhenDrawing);
            clearDrawingsOnJump = new ToggleRow(Loc.Get("Settings_ClearDrawingsOnJump"), Loc.Get("Settings_ClearDrawingsOnJumpHint"), overlay.ClearDrawingsOnJump);
            drawingFade = new RangeRow(Loc.Get("Settings_DrawingFade"), Loc.Get("Settings_DrawingFadeHint"), overlay.DrawingFadeSeconds, (0, 60, 1),
                v => v == 0 ? Loc.Get("Settings_Never") : Seconds(v));
            Overlay =
            [
                new HeadingRow(Loc.Get("Settings_OverlayPanels")), overlayScale, showOwnScoreboard,
                new HeadingRow(Loc.Get("Settings_Drawing")), pauseWhenDrawing, clearDrawingsOnJump, drawingFade,
                new HeadingRow(Loc.Get("Settings_Bookmarks")), pauseWhileNoting,
                new HeadingRow(Loc.Get("Settings_OverlayLeague")), directedCamera, hideLeagueControls, hideLeagueCameraPanel,
            ];
        }

        public IReadOnlyList<SettingRow> General { get; }

        public IReadOnlyList<SettingRow> Overlay { get; }

        public AppSettings ApplyTo(AppSettings settings) => settings with
        {
            InterfaceScale = interfaceScale.Value / 100,
            Overlay = new OverlaySettings
            {
                Scale = overlayScale.Value / 100,
                DirectedCamera = directedCamera.IsOn,
                PauseWhenDrawing = pauseWhenDrawing.IsOn,
                PauseWhileNoting = pauseWhileNoting.IsOn,
                ClearDrawingsOnJump = clearDrawingsOnJump.IsOn,
                DrawingFadeSeconds = drawingFade.Value,
                HideLeagueControls = hideLeagueControls.IsOn,
                HideLeagueCameraPanel = hideLeagueCameraPanel.IsOn,
                ShowOwnScoreboard = showOwnScoreboard.IsOn,
            },
        };
    }
}
