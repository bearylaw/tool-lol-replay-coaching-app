using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using RiftReview.App.Localization;
using RiftReview.App.Mvvm;
using RiftReview.App.Replays;
using RiftReview.App.Settings;
using RiftReview.App.Timelines;
using RiftReview.Core.Logging;
using RiftReview.Launch;
using RiftReview.Library;

namespace RiftReview.App.Shell;

/// <summary>Which replays the list shows.</summary>
internal enum LibraryFilter
{
    All,
    Own,
    Players,
    Roster,
}

/// <summary>The main window: the library list, the selected replay, and the actions on it.</summary>
internal sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly SettingsStore settingsStore;
    private readonly NotesStore notes;
    private readonly BookmarkStore bookmarks;
    private IReadOnlyList<BookmarkLine> selectedBookmarks = [];
    private readonly IconProvider icons;
    private readonly Dialogs dialogs;
    private readonly AppLog log;
    private readonly InstallSearchPaths installPaths;
    private readonly ClientReplayWatcher replayWatcher;
    private readonly TimelineCache timelines;
    private readonly Overlay.OverlayCoordinator overlay;
    private readonly DispatcherTimer rescanTimer;
    private readonly List<FileSystemWatcher> watchers = [];
    private readonly List<ReplayEntry> openedOutsideLibrary = [];

    private AppSettings settings;
    private LeagueInstallation? installation;
    private string installError = string.Empty;
    private List<ReplayItemViewModel> allItems = [];
    private CancellationTokenSource? scan;
    private ReplayItemViewModel? selected;
    private ReplayDetailViewModel? detail;
    private LibraryFilter filter = LibraryFilter.All;
    private string searchText = string.Empty;
    private bool isScanning;
    private string statusText = string.Empty;
    private string noteText = string.Empty;
    private bool isLaunching;

    public MainViewModel(AppSettings settings, SettingsStore settingsStore, NotesStore notes, BookmarkStore bookmarks, IconProvider icons, Dialogs dialogs, AppLog log, InstallSearchPaths installPaths, TimelineCache timelines, Overlay.OverlayCoordinator overlay)
    {
        this.settings = settings;
        this.settingsStore = settingsStore;
        this.notes = notes;
        this.bookmarks = bookmarks;
        bookmarks.Changed += (_, key) => OnBookmarksChanged(key);
        DeleteBookmarkCommand = new RelayCommand(p =>
        {
            if (p is BookmarkLine line && Selected is not null)
            {
                bookmarks.Remove(Selected.NoteKey, line.Bookmark.Id);
            }
        });
        this.icons = icons;
        this.dialogs = dialogs;
        this.log = log;
        this.installPaths = installPaths;
        this.timelines = timelines;
        this.overlay = overlay;
        replayWatcher = new ClientReplayWatcher(TimeProvider.System);

        rescanTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        rescanTimer.Tick += async (_, _) =>
        {
            rescanTimer.Stop();
            await RefreshAsync();
        };

        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        AddReplaysCommand = new RelayCommand(async () => await ImportAsync(dialogs.PickReplayFiles()));
        WatchCommand = new RelayCommand(async () => await WatchAsync(), () => Detail is not null && !IsLaunching);
        WatchWithOverlayCommand = new RelayCommand(async () => await WatchWithOverlayAsync(), () => Detail is not null && !IsLaunching);
        ShowInExplorerCommand = new RelayCommand(() => Dialogs.ShowFile(Selected!.Entry.FilePath), () => Selected is not null);
        OpenPlayerFolderCommand = new RelayCommand(() => Dialogs.OpenFolder(this.settings.PlayerReplaysFolder));
        OpenOwnFolderCommand = new RelayCommand(() => Dialogs.OpenFolder(this.settings.OwnReplaysFolder));
        OpenSettingsCommand = new RelayCommand(async () => await EditSettingsAsync());
        ToggleRosterCommand = new RelayCommand(p => ToggleRoster((PlayerRowViewModel)p!), p => p is PlayerRowViewModel);
        SetFilterCommand = new RelayCommand(p => Filter = Enum.Parse<LibraryFilter>((string)p!));
    }

    /// <summary>Asks the window to open the settings dialog; returns the edited settings or <c>null</c>.</summary>
    public Func<AppSettings, LeagueInstallation?, AppSettings?>? EditSettings { get; set; }

    /// <summary>Size of the main window's content, from the settings.</summary>
    public double InterfaceScale => settings.InterfaceScale;

    public ObservableCollection<ReplayItemViewModel> Items { get; } = [];

    public ICommand RefreshCommand { get; }

    public ICommand AddReplaysCommand { get; }

    public ICommand WatchCommand { get; }

    /// <summary>Starts the replay in League with Rift Review's overlay on top.</summary>
    public ICommand WatchWithOverlayCommand { get; }

    public ICommand ShowInExplorerCommand { get; }

    public ICommand OpenPlayerFolderCommand { get; }

    public ICommand OpenOwnFolderCommand { get; }

    public ICommand OpenSettingsCommand { get; }

    public ICommand ToggleRosterCommand { get; }

    public ICommand SetFilterCommand { get; }

    public ReplayItemViewModel? Selected
    {
        get => selected;
        set
        {
            if (SetProperty(ref selected, value))
            {
                ShowDetail(value);
            }
        }
    }

    public ReplayDetailViewModel? Detail
    {
        get => detail;
        private set
        {
            var previous = detail;
            if (SetProperty(ref detail, value))
            {
                previous?.Dispose();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public LibraryFilter Filter
    {
        get => filter;
        set
        {
            if (SetProperty(ref filter, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    /// <summary>True while a replay is being handed to the League client.</summary>
    public bool IsLaunching
    {
        get => isLaunching;
        private set
        {
            if (SetProperty(ref isLaunching, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsScanning
    {
        get => isScanning;
        private set => SetProperty(ref isScanning, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    /// <summary>The selected replay's bookmarks, all of them, in time order.</summary>
    public IReadOnlyList<BookmarkLine> SelectedBookmarks
    {
        get => selectedBookmarks;
        private set
        {
            if (SetProperty(ref selectedBookmarks, value))
            {
                OnPropertyChanged(nameof(HasSelectedBookmarks));
            }
        }
    }

    public bool HasSelectedBookmarks => SelectedBookmarks.Count > 0;

    public System.Windows.Input.ICommand DeleteBookmarkCommand { get; }

    // A bookmark was made, edited or deleted (in the overlay or here): refresh the cards and the list.
    private void OnBookmarksChanged(string key)
    {
        var saved = bookmarks.Get(key);
        foreach (var item in allItems.Where(i => string.Equals(i.NoteKey, key, StringComparison.OrdinalIgnoreCase)))
        {
            item.SetBookmarks(saved);
        }

        if (Selected is not null && string.Equals(Selected.NoteKey, key, StringComparison.OrdinalIgnoreCase))
        {
            SelectedBookmarks = [.. saved.Select(BookmarkLine.From)];
        }
    }

    /// <summary>The coach's note on the selected replay; saved when the text box loses focus.</summary>
    public string NoteText
    {
        get => noteText;
        set
        {
            if (SetProperty(ref noteText, value) && Selected is not null)
            {
                notes.Set(Selected.NoteKey, value);
            }
        }
    }

    public string InstallText => installation is null
        ? Loc.Get("Install_NotFound")
        : Loc.Format("Install_Found", installation.Version?.ToString() ?? "?", installation.InstallDirectory);

    public bool IsInstallFound => installation is not null;

    public int CountAll => allItems.Count;

    public int CountOwn => allItems.Count(i => i.Entry.Source == ReplaySource.Own);

    public int CountPlayers => allItems.Count(i => i.Entry.Source == ReplaySource.Players);

    public int CountRoster => allItems.Count(i => i.RosterSummary is not null);

    public bool IsLibraryEmpty => allItems.Count == 0 && !IsScanning;

    public string EmptyLibraryText => Loc.Format("Library_Empty", settings.OwnReplaysFolder, settings.PlayerReplaysFolder);

    /// <summary>Locates League, starts watching the folders, and scans. Opens <paramref name="replayToOpen"/> if given.</summary>
    public async Task StartAsync(string? replayToOpen)
    {
        LocateInstall();
        WatchFolders();
        if (replayToOpen is not null && File.Exists(replayToOpen))
        {
            await OpenFileAsync(replayToOpen);
        }
        else
        {
            await RefreshAsync();
        }
    }

    /// <summary>Shows a replay that may be outside the library, for example when opened from Explorer.</summary>
    public async Task OpenFileAsync(string path)
    {
        var full = Path.GetFullPath(path);
        var inLibrary = Folders().Any(f => full.StartsWith(Path.GetFullPath(f.Path) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        if (!inLibrary && openedOutsideLibrary.TrueForAll(e => !string.Equals(e.FilePath, full, StringComparison.OrdinalIgnoreCase)))
        {
            openedOutsideLibrary.Add(ReplayLibrary.ReadEntry(full, new LibraryFolder(Path.GetDirectoryName(full)!, ReplaySource.Players)));
        }

        await RefreshAsync(selectPath: full);
    }

    /// <summary>Copies dropped or picked files into the player folder, then rescans.</summary>
    public async Task ImportAsync(IReadOnlyList<string> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        IReadOnlyList<ImportOutcome> outcomes;
        try
        {
            outcomes = await Task.Run(() => ReplayImporter.Import(files, settings.PlayerReplaysFolder));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            log.Error("import.folder_unusable", exception, ("folder", settings.PlayerReplaysFolder));
            dialogs.Error(Loc.Format("Import_FolderUnusable", settings.PlayerReplaysFolder, exception.Message));
            return;
        }

        foreach (var failure in outcomes.Where(o => o.Result == ImportResult.Failed))
        {
            log.Warning("import.failed", null, ("path", failure.SourcePath), ("detail", failure.Detail));
        }

        var imported = outcomes.Count(o => o.Result is ImportResult.Imported or ImportResult.ImportedRenamed);
        log.Info("import.finished", ("files", files.Count), ("imported", imported));
        StatusText = Loc.Format(
            "Import_Summary",
            imported,
            outcomes.Count(o => o.Result == ImportResult.AlreadyPresent),
            outcomes.Count(o => o.Result == ImportResult.NotAReplay),
            outcomes.Count(o => o.Result == ImportResult.Failed));

        await RefreshAsync(selectPath: outcomes.FirstOrDefault(o => o.TargetPath is not null)?.TargetPath);
    }

    /// <summary>Rescans both folders off the UI thread; a newer scan cancels an older one.</summary>
    public async Task RefreshAsync(string? selectPath = null)
    {
        selectPath ??= Selected?.Entry.FilePath;
        if (scan is not null)
        {
            await scan.CancelAsync();
            scan.Dispose();
        }

        scan = new CancellationTokenSource();
        var token = scan.Token;
        IsScanning = true;
        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<ReplayEntry> entries;
        try
        {
            var folders = Folders();
            entries = await Task.Run(() => ReplayLibrary.Scan(folders, token), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsScanning = false;
            }
        }

        var all = entries.Concat(openedOutsideLibrary.Where(o => entries.All(e => !string.Equals(e.FilePath, o.FilePath, StringComparison.OrdinalIgnoreCase))));
        allItems = [.. all.Select(e => new ReplayItemViewModel(e)).OrderByDescending(i => i.Entry.ModifiedUtc)];
        foreach (var item in allItems)
        {
            item.SetBookmarks(bookmarks.Get(item.NoteKey));
            item.UpdateRoster(settings.IsOnRoster);
            item.Compatibility = item.Metadata is null ? Compatibility.Unknown : PlaybackCompatibility.Check(item.Metadata.Version, installation?.Version);
        }

        log.Info("scan.finished", ("replays", allItems.Count), ("unreadable", allItems.Count(i => !i.IsReadable)), ("ms", stopwatch.ElapsedMilliseconds));
        ApplyFilter();
        Selected = Items.FirstOrDefault(i => string.Equals(i.Entry.FilePath, selectPath, StringComparison.OrdinalIgnoreCase)) ?? Items.FirstOrDefault();
        await LoadChampionStripsAsync(allItems);
    }

    public void Dispose()
    {
        foreach (var watcher in watchers)
        {
            watcher.Dispose();
        }

        scan?.Dispose();
    }

    private IReadOnlyList<LibraryFolder> Folders() =>
    [
        new LibraryFolder(settings.OwnReplaysFolder, ReplaySource.Own),
        new LibraryFolder(settings.PlayerReplaysFolder, ReplaySource.Players),
    ];

    private async Task LoadChampionStripsAsync(IReadOnlyList<ReplayItemViewModel> items)
    {
        foreach (var patch in items.Where(i => i.Metadata is not null).GroupBy(i => i.Metadata!.Version))
        {
            var catalog = await icons.GetCatalogAsync(patch.Key);
            foreach (var item in patch)
            {
                item.LoadChampions(catalog, icons, settings.IsOnRoster);
            }
        }

        RaiseCounts();
    }

    private void ApplyFilter()
    {
        var visible = allItems.Where(i => filter switch
        {
            LibraryFilter.Own => i.Entry.Source == ReplaySource.Own,
            LibraryFilter.Players => i.Entry.Source == ReplaySource.Players,
            LibraryFilter.Roster => i.RosterSummary is not null,
            _ => true,
        } && ReplaySearch.Matches(i.Entry, searchText));

        var keep = Selected;
        Items.Clear();
        foreach (var item in visible)
        {
            Items.Add(item);
        }

        if (keep is not null && Items.Contains(keep))
        {
            Selected = keep;
        }

        RaiseCounts();
    }

    private void RaiseCounts()
    {
        OnPropertyChanged(nameof(CountAll));
        OnPropertyChanged(nameof(CountOwn));
        OnPropertyChanged(nameof(CountPlayers));
        OnPropertyChanged(nameof(CountRoster));
        OnPropertyChanged(nameof(IsLibraryEmpty));
        OnPropertyChanged(nameof(EmptyLibraryText));
    }

    private async void ShowDetail(ReplayItemViewModel? item)
    {
        // async void: a UI event boundary; everything awaited here handles its own failures.
        noteText = item is null ? string.Empty : notes.Get(item.NoteKey);
        OnPropertyChanged(nameof(NoteText));
        SelectedBookmarks = item is null ? [] : [.. bookmarks.Get(item.NoteKey).Select(BookmarkLine.From)];
        if (item?.Metadata is null)
        {
            Detail = null;
            return;
        }

        var catalog = await icons.GetCatalogAsync(item.Metadata.Version);
        if (!ReferenceEquals(Selected, item))
        {
            return;
        }

        var detail = new ReplayDetailViewModel(item, item.Metadata, catalog, icons, settings.IsOnRoster);
        Detail = detail;
        var timeline = await timelines.GetAsync(item.Entry.FilePath, item.Metadata, CancellationToken.None);
        if (timeline.TryGetValue(out var loaded, out var failure))
        {
            detail.ShowTimeline(loaded, icons, catalog);
        }
        else
        {
            detail.Timeline.Fail(failure);
        }
    }

    private async Task WatchAsync() => await StartInClientAsync();

    private async Task WatchWithOverlayAsync()
    {
        if (Detail is not { } detail)
        {
            return;
        }

        if (installation is null && !LocateInstall())
        {
            dialogs.Error(Loc.Format("Launch_NoInstall", installError));
            return;
        }

        var catalog = await icons.GetCatalogAsync(detail.Metadata.Version);
        var marks = Selected is { } replay ? new Overlay.BookmarkList(bookmarks, replay.NoteKey) : null;
        await overlay.OpenAsync(detail.LoadedTimeline, detail.AllPlayers, detail.ItemIcon, settings.Overlay, installation!, StartInClientAsync, text => StatusText = text, marks, catalog?.ItemPrices);
    }

    /// <summary>Hands the selected replay to the League client (the Watch flow); true when it started.</summary>
    private async Task<bool> StartInClientAsync()
    {
        if (Selected is not { Metadata: { } metadata } item || IsLaunching)
        {
            return false;
        }

        if (installation is null && !LocateInstall())
        {
            dialogs.Error(Loc.Format("Launch_NoInstall", installError));
            return false;
        }

        var league = installation!;
        var compatibility = PlaybackCompatibility.Check(metadata.Version, league.Version);
        if (compatibility != Compatibility.Compatible
            && !dialogs.Confirm(Loc.Format($"Launch_Confirm_{compatibility}", metadata.Version, league.Version?.ToString() ?? "?")))
        {
            return false;
        }

        IsLaunching = true;
        try
        {
            var lockfile = ClientReplayWatcher.FindRunningClient(league) ?? await StartClientAsync(league);
            if (lockfile is null)
            {
                return false;
            }

            StatusText = Loc.Format("Launch_Preparing", item.Title);
            using var client = new LeagueClientApi(lockfile);
            var path = item.Entry.FilePath;
            var result = await replayWatcher.WatchAsync(client, path, metadata.Version, league.Region, CancellationToken.None);
            if (result.TryGetValue(out var clientCopy, out var failure))
            {
                log.Info("watch.started", ("file", Path.GetFileName(path)), ("clientFile", Path.GetFileName(clientCopy)), ("compatibility", compatibility));
                StatusText = Loc.Format("Launch_Started", item.Title);
                return true;
            }

            log.Error("watch.failed", null, ("file", Path.GetFileName(path)), ("reason", failure.Reason), ("detail", failure.Detail));
            StatusText = string.Empty;
            dialogs.Error(Loc.Format($"Watch_Failed_{failure.Reason}", failure.Detail));
            return false;
        }
        finally
        {
            IsLaunching = false;
        }
    }

    /// <summary>Offers to start the League client and waits until someone is logged in; <c>null</c> when that does not happen.</summary>
    private async Task<ClientLockfile?> StartClientAsync(LeagueInstallation league)
    {
        if (!dialogs.Confirm(Loc.Get("Launch_StartClient")))
        {
            return null;
        }

        var riotClient = RiotClientStarter.FindRiotClient(installPaths, league);
        var error = riotClient is null ? "RiotClientInstalls.json names no Riot Client" : RiotClientStarter.Start(riotClient);
        if (error is not null)
        {
            log.Error("client.start_failed", null, ("detail", error));
            dialogs.Error(Loc.Format("Launch_ClientStartFailed", error));
            return null;
        }

        log.Info("client.started");
        StatusText = Loc.Get("Launch_WaitingForClient");
        var lockfile = await replayWatcher.WaitForReadyClientAsync(league, TimeSpan.FromMinutes(5), CancellationToken.None);
        if (lockfile is null)
        {
            StatusText = string.Empty;
            dialogs.Error(Loc.Get("Launch_ClientTimeout"));
        }

        return lockfile;
    }

    private bool LocateInstall()
    {
        var result = LeagueInstallLocator.Locate(installPaths, settings.LeagueInstallOverride);
        installation = result.TryGetValue(out var found, out var error) ? found : null;
        installError = error ?? string.Empty;
        log.Info("install.located", ("found", installation is not null), ("version", installation?.Version), ("detail", error));
        OnPropertyChanged(nameof(InstallText));
        OnPropertyChanged(nameof(IsInstallFound));
        return installation is not null;
    }

    private void WatchFolders()
    {
        foreach (var watcher in watchers)
        {
            watcher.Dispose();
        }

        watchers.Clear();
        foreach (var folder in Folders().Where(f => Directory.Exists(f.Path)))
        {
            var watcher = new FileSystemWatcher(folder.Path, "*.rofl") { IncludeSubdirectories = true };
            watcher.Created += OnFolderChanged;
            watcher.Deleted += OnFolderChanged;
            watcher.Renamed += OnFolderChanged;
            watcher.Changed += OnFolderChanged;
            watcher.EnableRaisingEvents = true;
            watchers.Add(watcher);
        }
    }

    // Raised on a thread-pool thread; the client writes a replay in many steps, so rescan once
    // the folder has been quiet for two seconds.
    private void OnFolderChanged(object sender, FileSystemEventArgs e) =>
        rescanTimer.Dispatcher.BeginInvoke(() =>
        {
            rescanTimer.Stop();
            rescanTimer.Start();
        });

    private async Task EditSettingsAsync()
    {
        if (EditSettings?.Invoke(settings, installation) is not { } edited)
        {
            return;
        }

        var previous = settings;
        settings = edited.Normalized();
        settingsStore.Save(settings);
        log.Info("settings.saved", ("roster", settings.Roster.Count));
        OnPropertyChanged(nameof(InterfaceScale));
        if (previous.OwnReplaysFolder == settings.OwnReplaysFolder
            && previous.PlayerReplaysFolder == settings.PlayerReplaysFolder
            && previous.LeagueInstallOverride == settings.LeagueInstallOverride
            && previous.Roster.SequenceEqual(settings.Roster))
        {
            return;
        }

        LocateInstall();
        WatchFolders();
        await RefreshAsync();
    }

    private void ToggleRoster(PlayerRowViewModel player)
    {
        var riotId = player.Stats.RiotId;
        settings = settings with
        {
            Roster = settings.IsOnRoster(riotId)
                ? [.. settings.Roster.Where(r => !string.Equals(r, riotId, StringComparison.OrdinalIgnoreCase))]
                : [.. settings.Roster, riotId],
        };
        settingsStore.Save(settings);
        player.IsOnRoster = settings.IsOnRoster(riotId);
        foreach (var item in allItems)
        {
            item.UpdateRoster(settings.IsOnRoster);
        }

        if (Detail is not null)
        {
            foreach (var row in Detail.AllPlayers)
            {
                row.IsOnRoster = settings.IsOnRoster(row.Stats.RiotId);
            }
        }

        RaiseCounts();
    }
}
