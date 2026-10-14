using System.Net.Http.Headers;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using RiftReview.App.Capture;
using RiftReview.App.Localization;
using RiftReview.App.Replays;
using RiftReview.App.Settings;
using RiftReview.App.Shell;
using RiftReview.App.Timelines;
using RiftReview.Core.Logging;
using RiftReview.GameData;
using RiftReview.Launch;
using RiftReview.Library;

namespace RiftReview.App;

/// <summary>
/// Composition root: creates the log, settings, services and the main window, and catches every
/// unhandled error at the edge so it is logged and shown instead of closing the app silently.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "WPF owns the lifetime; OnExit disposes the fields.")]
internal sealed partial class App : Application
{
    private AppLog? log;
    private HttpClient? http;
    private MainViewModel? viewModel;

    /// <summary>The version from Directory.Build.props, plus the commit when built from git.</summary>
    public static string Version =>
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    /// <summary>
    /// <c>%LOCALAPPDATA%\RiftReview</c>: logs, the icon cache and timelines. With <c>RIFTREVIEW_HOME</c>
    /// set, everything lives in that folder instead (a portable copy, or a demo apart from your data).
    /// </summary>
    public static string LocalDataFolder => Home is { } home
        ? Path.Combine(home, "local")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RiftReview");

    /// <summary><c>%APPDATA%\RiftReview</c>: settings, notes and bookmarks; <c>RIFTREVIEW_HOME</c> when set.</summary>
    public static string RoamingDataFolder => Home ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RiftReview");

    private static string? Home => Environment.GetEnvironmentVariable("RIFTREVIEW_HOME") is { Length: > 0 } home ? Path.GetFullPath(home) : null;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // async void: the WPF startup event; failures reach the handlers registered below.
        base.OnStartup(e);
        if (e.Args is ["--version"])
        {
            NativeMethods.AttachConsole(-1);
            Console.WriteLine($"Rift Review {Version}");
            Shutdown(0);
            return;
        }

        var clock = TimeProvider.System;
        var roamingFolder = RoamingDataFolder;
        log = AppLog.ToDailyFile(Path.Combine(LocalDataFolder, "logs"), clock);
        log.Info("app.started", ("version", Version), ("os", Environment.OSVersion));
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => log.Error("app.crashed", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => log.Error("task.unobserved", args.Exception);

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var settingsStore = new SettingsStore(Path.Combine(roamingFolder, "settings.json"), log);
        var settings = settingsStore.Load(new AppSettings
        {
            OwnReplaysFolder = DefaultFolders.OwnReplays(documents),
            PlayerReplaysFolder = DefaultFolders.PlayerReplays(AppContext.BaseDirectory, documents),
        });
        // Saving the first-run defaults keeps the player folder fixed even if the exe moves later.
        settingsStore.Save(settings);

        http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RiftReview", Version.Split('+')[0]));
        var icons = new IconProvider(new GameDataService(http, Path.Combine(LocalDataFolder, "ddragon"), log), log);

        var timelines = new TimelineCache(Path.Combine(LocalDataFolder, "timelines"), log);
        MainWindow? window = null;
        var dialogs = new Dialogs(() => window);
        var replayGame = new ReplayGame(dialogs, log, Path.Combine(LocalDataFolder, "backup"));
        viewModel = new MainViewModel(
            settings,
            settingsStore,
            new NotesStore(Path.Combine(roamingFolder, "notes.json"), log),
            new BookmarkStore(Path.Combine(roamingFolder, "bookmarks.json"), log),
            icons,
            dialogs,
            log,
            InstallSearchPaths.ForThisMachine(),
            timelines,
            new Overlay.OverlayCoordinator(log, replayGame, dialogs))
        {
            EditSettings = (current, installation) =>
            {
                var editor = new SettingsWindow(current, installation, dialogs) { Owner = window };
                return editor.ShowDialog() == true ? editor.Result : null;
            },
        };

        window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
        await viewModel.StartAsync(e.Args.FirstOrDefault(a => a.EndsWith(".rofl", StringComparison.OrdinalIgnoreCase)));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        viewModel?.Dispose();
        http?.Dispose();
        log?.Info("app.exited", ("code", e.ApplicationExitCode));
        log?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        log?.Error("ui.unhandled", e.Exception);
        MessageBox.Show(
            Loc.Format("Crash_Message", Path.Combine(LocalDataFolder, "logs")),
            Loc.Get("App_Title"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        // The error is logged and shown; keep the window open rather than lose the coach's work.
        e.Handled = true;
    }
}
