using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using RiftReview.App.Localization;

namespace RiftReview.App.Shell;

/// <summary>Message boxes, file pickers and Explorer, owned by the main window.</summary>
internal sealed class Dialogs(Func<Window?> owner)
{
    /// <summary>Lets the coach pick replay files; empty when cancelled.</summary>
    public IReadOnlyList<string> PickReplayFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.Get("Dialog_AddReplaysTitle"),
            Filter = Loc.Get("Dialog_ReplayFilter") + "|*.rofl",
            Multiselect = true,
        };
        return dialog.ShowDialog(owner()) == true ? dialog.FileNames : [];
    }

    /// <summary>Asks where to save a review file; <c>null</c> when cancelled.</summary>
    public string? PickReviewToSave(string fileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = Loc.Get("Dialog_ShareReviewTitle"),
            Filter = Loc.Get("Dialog_ReviewFilter") + "|*" + Settings.ReviewShare.Extension,
            FileName = fileName,
            DefaultExt = Settings.ReviewShare.Extension,
        };
        return dialog.ShowDialog(owner()) == true ? dialog.FileName : null;
    }

    /// <summary>Lets the coach pick review files; empty when cancelled.</summary>
    public IReadOnlyList<string> PickReviewsToImport()
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.Get("Dialog_ImportReviewTitle"),
            Filter = Loc.Get("Dialog_ReviewFilter") + "|*" + Settings.ReviewShare.Extension,
            Multiselect = true,
        };
        return dialog.ShowDialog(owner()) == true ? dialog.FileNames : [];
    }

    /// <summary>Lets the coach pick a folder; <c>null</c> when cancelled.</summary>
    public string? PickFolder(string title, string? initialFolder)
    {
        var dialog = new OpenFolderDialog { Title = title, InitialDirectory = initialFolder ?? string.Empty };
        return dialog.ShowDialog(owner()) == true ? dialog.FolderName : null;
    }

    /// <summary>Asks a yes/no question; true for yes.</summary>
    public bool Confirm(string message) =>
        Show(message, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void Inform(string message) => Show(message, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Error(string message) => Show(message, MessageBoxButton.OK, MessageBoxImage.Error);

    /// <summary>Opens Explorer at a folder, creating it first so a fresh player folder can be opened.</summary>
    public static void OpenFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        using var _ = Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { folder }, UseShellExecute = false });
    }

    /// <summary>Opens Explorer with a file selected.</summary>
    public static void ShowFile(string file)
    {
        using var _ = Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = false });
    }

    private MessageBoxResult Show(string message, MessageBoxButton buttons, MessageBoxImage image)
    {
        var window = owner();
        return window is null
            ? MessageBox.Show(message, Loc.Get("App_Title"), buttons, image)
            : MessageBox.Show(window, message, Loc.Get("App_Title"), buttons, image);
    }
}
