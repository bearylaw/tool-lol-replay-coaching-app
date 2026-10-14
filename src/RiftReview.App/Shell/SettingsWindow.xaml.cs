using System.Windows;
using RiftReview.App.Localization;
using RiftReview.App.Settings;
using RiftReview.Launch;

namespace RiftReview.App.Shell;

/// <summary>Edits folders, the League install override, the roster, and the display, viewer and capture rows.</summary>
internal sealed partial class SettingsWindow : Window
{
    private readonly AppSettings original;
    private readonly Dialogs dialogs;
    private readonly SettingsEditor editor;

    public SettingsWindow(AppSettings settings, LeagueInstallation? installation, Dialogs dialogs)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NativeMethods.UseDarkTitleBar(this);
        original = settings;
        this.dialogs = dialogs;
        editor = new SettingsEditor(settings);
        DataContext = editor;
        OwnFolder.Text = settings.OwnReplaysFolder;
        PlayerFolder.Text = settings.PlayerReplaysFolder;
        LeagueFolder.Text = settings.LeagueInstallOverride ?? string.Empty;
        LeagueHint.Text = installation is null
            ? Loc.Get("Settings_LeagueHintNotFound")
            : Loc.Format("Settings_LeagueHintFound", installation.InstallDirectory);
        Roster.Text = string.Join(Environment.NewLine, settings.Roster);
    }

    /// <summary>The edited settings after Save, else <c>null</c>.</summary>
    public AppSettings? Result { get; private set; }

    private void OnBrowseOwn(object sender, RoutedEventArgs e) => Browse(OwnFolder, Loc.Get("Settings_OwnFolder"));

    private void OnBrowsePlayers(object sender, RoutedEventArgs e) => Browse(PlayerFolder, Loc.Get("Settings_PlayerFolder"));

    private void OnBrowseLeague(object sender, RoutedEventArgs e) => Browse(LeagueFolder, Loc.Get("Settings_LeagueFolder"));

    private void Browse(System.Windows.Controls.TextBox target, string title)
    {
        if (dialogs.PickFolder(title, target.Text) is { } folder)
        {
            target.Text = folder;
        }
    }

    private void OnReset(object sender, RoutedEventArgs e) => editor.ResetToDefaults();

    private void OnSave(object sender, RoutedEventArgs e)
    {
        Result = editor.ApplyTo(original with
        {
            OwnReplaysFolder = Trimmed(OwnFolder.Text) ?? original.OwnReplaysFolder,
            PlayerReplaysFolder = Trimmed(PlayerFolder.Text) ?? original.PlayerReplaysFolder,
            LeagueInstallOverride = Trimmed(LeagueFolder.Text),
            Roster = [.. Roster.Text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)],
        });
        DialogResult = true;
    }

    private static string? Trimmed(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
