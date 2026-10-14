using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Threading;
using RiftReview.App.Mvvm;
using RiftReview.App.Replays;
using RiftReview.App.Settings;
using RiftReview.App.Viewer;
using RiftReview.Capture;
using RiftReview.Core.Logging;
using RiftReview.Core.Matches;
using RiftReview.Core.Timelines;

namespace RiftReview.App.Overlay;

/// <summary>One player on the overlay's scoreboard, from League's live data at the replay's current time.</summary>
internal sealed class OverlayPlayer : ObservableObject
{
    private readonly Func<int, IconSlot> itemIcon;
    private readonly IReadOnlyDictionary<int, int> itemPrices;
    private int level = 1;
    private bool isDead;
    private IReadOnlyList<IconSlot> items = [];
    private string itemKey = string.Empty;
    private (int Kills, int Deaths, int Assists, int CreepScore, double WardScore, int ItemValue, double Minutes) stats;

    public OverlayPlayer(PlayerRowViewModel row, Func<int, IconSlot> itemIcon, IReadOnlyDictionary<int, int>? itemPrices = null)
    {
        Row = row;
        this.itemIcon = itemIcon;
        this.itemPrices = itemPrices ?? new Dictionary<int, int>();
    }

    public PlayerRowViewModel Row { get; }

    public IconSlot Champion => Row.Champion;

    public IReadOnlyList<IconSlot> SummonerSpells => Row.SummonerSpells;

    public string Name => Row.Stats.GameName;

    public bool IsBlue => Row.Stats.Team == Team.Blue;

    public int Level
    {
        get => level;
        private set => SetProperty(ref level, value);
    }

    public bool IsDead
    {
        get => isDead;
        private set => SetProperty(ref isDead, value);
    }

    public IReadOnlyList<IconSlot> Items
    {
        get => items;
        private set => SetProperty(ref items, value);
    }

    public int Kills => stats.Kills;

    /// <summary>Kills / deaths / assists.</summary>
    public string KdaText => $"{stats.Kills} / {stats.Deaths} / {stats.Assists}";

    /// <summary>Creep score and per minute.</summary>
    public string CsText => stats.Minutes < 1
        ? stats.CreepScore.ToString(CultureInfo.CurrentCulture)
        : $"{stats.CreepScore} · {(stats.CreepScore / stats.Minutes).ToString("0.0", CultureInfo.CurrentCulture)}";

    public string WardText => Math.Round(stats.WardScore).ToString(CultureInfo.CurrentCulture);

    /// <summary>The gold the held items cost (Data Dragon prices).</summary>
    public int ItemValue => stats.ItemValue;

    public string ItemValueText => Scoreboard.Gold(stats.ItemValue);

    public void Update(LivePlayer live, double gameTime)
    {
        ArgumentNullException.ThrowIfNull(live);
        var slots = new int[7];
        foreach (var item in live.Items.Where(i => i.Slot is >= 0 and < 7))
        {
            slots[item.Slot] = item.Id;
        }

        var next = (live.Kills, live.Deaths, live.Assists, live.CreepScore, live.WardScore, slots.Sum(id => itemPrices.GetValueOrDefault(id)), gameTime / 60);
        if (next != stats)
        {
            stats = next;
            OnPropertyChanged(nameof(Kills));
            OnPropertyChanged(nameof(KdaText));
            OnPropertyChanged(nameof(CsText));
            OnPropertyChanged(nameof(WardText));
            OnPropertyChanged(nameof(ItemValue));
            OnPropertyChanged(nameof(ItemValueText));
        }

        Level = live.Level;
        IsDead = live.IsDead;
        var key = string.Join(',', slots);
        if (key != itemKey)
        {
            itemKey = key;
            Items = [.. slots.Select(itemIcon)];
        }
    }
}

/// <summary>A note box to open: for a new bookmark at <paramref name="Time"/>, or to edit <paramref name="Existing"/>.</summary>
internal sealed record NoteRequest(double Time, BookmarkItem? Existing);

/// <summary>How the overlay ends: only the overlay, the replay with it, or the game closed by itself.</summary>
internal enum OverlayEnd
{
    CloseOverlay,
    EndReplay,
    GameGone,
}

/// <summary>
/// The overlay on League's replay window: Rift Review's control bar with its own timeline (deaths,
/// turret falls, bookmarks), the scoreboard, drawing and timestamped notes, driving the game through
/// Riot's replay API. Team frames, kill feed, objective timers and the camera stay League's own.
/// </summary>
internal sealed class OverlayViewModel : ObservableObject, IDisposable
{
    private static readonly double[] Speeds = [0.25, 0.5, 1, 2, 4, 8];

    // A jump to a bookmark starts this long before it, to see what led up to it.
    private const double BookmarkLead = 3;

    private readonly ReplayRemote remote;
    private readonly GameApi game;
    private readonly IReadOnlyList<PlayerRowViewModel> rows;
    private readonly AppLog log;
    private readonly DispatcherTimer poll;
    private readonly DispatcherTimer seekDelay;
    private readonly Stopwatch sincePoll = new();
    private Playback? playback;
    private double position;
    private double? pendingSeek;
    private int ticks;
    private bool showBookmarks;
    private bool pausedForNote;

    public OverlayViewModel(ReplayRemote remote, GameApi game, MatchTimeline? timeline, IReadOnlyList<PlayerRowViewModel> rows, Func<int, IconSlot> itemIcon, OverlaySettings settings, AppLog log, bool canEndReplay, BookmarkList? bookmarks = null, IReadOnlyDictionary<int, int>? itemPrices = null)
    {
        Bookmarks = bookmarks;
        CanEndReplay = canEndReplay;
        this.remote = remote;
        this.game = game;
        this.rows = rows;
        this.log = log;
        Settings = settings;
        Players = [.. rows.Select(r => new OverlayPlayer(r, itemIcon, itemPrices))];
        Scoreboard = new Scoreboard(Players, timeline?.TurretFalls ?? []);
        TurretMarks = [.. (timeline?.TurretFalls ?? []).Select(t => new ScrubberMark(t.Time, t.Owner == Team.Blue))];
        Marks = DeathMarksOf(timeline, rows);

        poll = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        poll.Tick += async (_, _) => await PollAsync();
        seekDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        seekDelay.Tick += async (_, _) => await SendSeekAsync();

        PlayPauseCommand = new RelayCommand(async () => await Send(() => remote.SetPausedAsync(!(playback?.Paused ?? false), CancellationToken.None)));
        BackCommand = new RelayCommand(async () => await SeekByAsync(-10));
        ForwardCommand = new RelayCommand(async () => await SeekByAsync(+10));
        SlowerCommand = new RelayCommand(async () => await ChangeSpeedAsync(-1));
        FasterCommand = new RelayCommand(async () => await ChangeSpeedAsync(+1));
        CloseCommand = new RelayCommand(() => Closed?.Invoke(this, OverlayEnd.CloseOverlay));
        EndReplayCommand = new RelayCommand(() => Closed?.Invoke(this, OverlayEnd.EndReplay));
        AddBookmarkCommand = new RelayCommand(async () => await RequestNoteAsync(new NoteRequest(Now, null)), () => Bookmarks is not null);
        ShowBookmarksCommand = new RelayCommand(() => ShowBookmarks = !ShowBookmarks, () => Bookmarks is not null);
        JumpToBookmarkCommand = new RelayCommand(p => JumpTo(p as BookmarkItem));
        EditBookmarkCommand = new RelayCommand(async p => await EditAsync(p as BookmarkItem));
        DeleteBookmarkCommand = new RelayCommand(p => Delete(p as BookmarkItem));
    }

    /// <summary>Raised when a note box should open over the game.</summary>
    public event EventHandler<NoteRequest>? NoteRequested;

    /// <summary>Raised when the overlay should close.</summary>
    public event EventHandler<OverlayEnd>? Closed;

    public OverlaySettings Settings { get; }

    /// <summary>The coach's drawing tools.</summary>
    public DrawingState Drawing { get; } = new();

    /// <summary>Whether Rift Review started this replay, and may therefore end it.</summary>
    public bool CanEndReplay { get; }

    public IReadOnlyList<OverlayPlayer> Players { get; }

    /// <summary>Deaths on the timeline, coloured by the team of the champion who died.</summary>
    public IReadOnlyList<ScrubberMark> Marks { get; }

    /// <summary>Turrets destroyed, coloured by the team that lost them.</summary>
    public IReadOnlyList<ScrubberMark> TurretMarks { get; }

    /// <summary>Both teams' stats and items at the current time (Tab or the scoreboard button).</summary>
    public Scoreboard Scoreboard { get; }

    public ICommand PlayPauseCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand ForwardCommand { get; }

    public ICommand SlowerCommand { get; }

    public ICommand FasterCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand EndReplayCommand { get; }

    /// <summary>Opens a note box for a bookmark at the current time (also Ctrl+Alt+B).</summary>
    public ICommand AddBookmarkCommand { get; }

    public ICommand ShowBookmarksCommand { get; }

    public ICommand JumpToBookmarkCommand { get; }

    public ICommand EditBookmarkCommand { get; }

    public ICommand DeleteBookmarkCommand { get; }

    /// <summary>This replay's bookmarks; <c>null</c> when the replay cannot be identified.</summary>
    public BookmarkList? Bookmarks { get; }

    /// <summary>Whether the bookmark list shows.</summary>
    public bool ShowBookmarks
    {
        get => showBookmarks;
        private set => SetProperty(ref showBookmarks, value);
    }

    public double Duration => playback?.Length ?? 0;

    public bool IsPlaying => playback is { Paused: false };

    public string SpeedText => (playback?.Speed ?? 1).ToString("0.##", CultureInfo.CurrentCulture) + "×";

    public string TimeText => $"{MatchFormat.Duration(TimeSpan.FromSeconds(position))} / {MatchFormat.Duration(TimeSpan.FromSeconds(Duration))}";

    /// <summary>The playback position in seconds; setting it (the scrubber) seeks.</summary>
    public double Position
    {
        get => position;
        set
        {
            if (SetProperty(ref position, value))
            {
                pendingSeek = value;
                seekDelay.Stop();
                seekDelay.Start();
                OnPropertyChanged(nameof(TimeText));
            }
        }
    }

    // Game time now, between polls moved on by the playback speed.
    private double Now => playback is null ? position
        : playback.Time + (playback.Paused ? 0 : sincePoll.Elapsed.TotalSeconds * playback.Speed);

    /// <summary>Sets League's replay interface to the coach's defaults, then starts polling.</summary>
    public async Task StartAsync()
    {
        await Send(async () =>
        {
            // The overlay replaces League's scoreboard and time controls; the rest stays League's.
            await remote.SetLeagueInterfaceAsync(LeagueInterface.ReplayControls, !Settings.HideLeagueControls, CancellationToken.None);
            await remote.SetLeagueInterfaceAsync(LeagueInterface.CameraPanel, !Settings.HideLeagueCameraPanel, CancellationToken.None);
            await remote.SetLeagueInterfaceAsync(LeagueInterface.Scoreboard, false, CancellationToken.None);
            await remote.SetLeagueInterfaceAsync(LeagueInterface.Chat, false, CancellationToken.None);
            foreach (var part in (LeagueInterface[])[LeagueInterface.TeamFrames, LeagueInterface.KillCallouts, LeagueInterface.ScoreDisplay, LeagueInterface.Minimap, LeagueInterface.TargetFrame, LeagueInterface.ObjectiveTimers])
            {
                await remote.SetLeagueInterfaceAsync(part, true, CancellationToken.None);
            }
        });
        poll.Start();
    }

    /// <summary>Gives League its own interface back.</summary>
    public async Task RestoreLeagueInterfaceAsync()
    {
        await Send(async () =>
        {
            foreach (var part in Enum.GetValues<LeagueInterface>())
            {
                await remote.SetLeagueInterfaceAsync(part, true, CancellationToken.None);
            }
        });
    }

    /// <summary>Saves what the note box returned: a new bookmark, an edited note, or nothing when cancelled.</summary>
    public async Task FinishNoteAsync(NoteRequest request, string? text)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (text is not null && Bookmarks is not null)
        {
            if (request.Existing is { } item)
            {
                Bookmarks.Edit(item, text);
            }
            else
            {
                Bookmarks.Add(request.Time, text);
            }
        }

        if (pausedForNote)
        {
            pausedForNote = false;
            await Send(() => remote.SetPausedAsync(false, CancellationToken.None));
        }
    }

    /// <summary>The coach started drawing: pauses the replay when set.</summary>
    public async Task DrawingStartedAsync()
    {
        if (Settings.PauseWhenDrawing && IsPlaying)
        {
            await Send(() => remote.SetPausedAsync(true, CancellationToken.None));
        }
    }

    public void Dispose()
    {
        poll.Stop();
        seekDelay.Stop();
    }

    private static IReadOnlyList<ScrubberMark> DeathMarksOf(MatchTimeline? timeline, IReadOnlyList<PlayerRowViewModel> rows) => timeline is null
        ? []
        : [.. timeline.Events.Where(e => e.Kind == TimelineEventKind.Death && e.Champion < rows.Count)
            .Select(e => new ScrubberMark(e.Time, rows[e.Champion].Stats.Team == Team.Blue))];

    private static LivePlayer? LiveOf(LiveSnapshot snapshot, PlayerRowViewModel row) =>
        snapshot.Players.FirstOrDefault(p => string.Equals(p.RiotId, row.Stats.RiotId, StringComparison.OrdinalIgnoreCase))
        ?? snapshot.Players.FirstOrDefault(p => string.Equals(p.Champion, row.Stats.Champion, StringComparison.OrdinalIgnoreCase));

    private void JumpTo(BookmarkItem? item)
    {
        if (item is not null)
        {
            Position = Math.Max(0, item.Time - BookmarkLead);
        }
    }

    private async Task EditAsync(BookmarkItem? item)
    {
        if (item is not null)
        {
            await RequestNoteAsync(new NoteRequest(item.Time, item));
        }
    }

    private void Delete(BookmarkItem? item)
    {
        if (item is not null)
        {
            Bookmarks?.Remove(item);
        }
    }

    private async Task RequestNoteAsync(NoteRequest request)
    {
        if (Bookmarks is null)
        {
            return;
        }

        if (request.Existing is null && Settings.PauseWhileNoting && IsPlaying)
        {
            pausedForNote = true;
            await Send(() => remote.SetPausedAsync(true, CancellationToken.None));
        }

        NoteRequested?.Invoke(this, request);
    }

    private async Task PollAsync()
    {
        ticks++;
        var current = await game.GetPlaybackAsync(CancellationToken.None);
        if (current is null)
        {
            if (playback is not null)
            {
                Closed?.Invoke(this, OverlayEnd.GameGone);
            }

            return;
        }

        playback = current;
        sincePoll.Restart();
        if (pendingSeek is null)
        {
            position = current.Time;
            OnPropertyChanged(nameof(Position));
        }

        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(SpeedText));
        OnPropertyChanged(nameof(TimeText));
        if (ticks % 4 == 1)
        {
            await UpdatePlayersAsync();
        }
    }

    private async Task UpdatePlayersAsync()
    {
        try
        {
            if (LiveGameDataParser.ParseAllGameData(await game.GetAllGameDataAsync(CancellationToken.None)).TryGetValue(out var snapshot, out _))
            {
                foreach (var player in Players)
                {
                    if (LiveOf(snapshot, player.Row) is { } live)
                    {
                        player.Update(live, snapshot.GameTime);
                    }
                }

                Scoreboard.Update(snapshot.GameTime);
            }
        }
        catch (HttpRequestException exception)
        {
            log.Warning("overlay.live_data", exception);
        }
    }

    private async Task SendSeekAsync()
    {
        seekDelay.Stop();
        if (pendingSeek is { } target)
        {
            await Send(() => remote.SeekAsync(target, CancellationToken.None));
            pendingSeek = null;
            Jumped();
        }
    }

    private async Task SeekByAsync(double seconds)
    {
        await Send(() => remote.SeekAsync(Now + seconds, CancellationToken.None));
        Jumped();
    }

    // A jump shows another moment: drawings about the old one go, when set.
    private void Jumped()
    {
        if (Settings.ClearDrawingsOnJump)
        {
            Drawing.Clear();
        }
    }

    private async Task ChangeSpeedAsync(int step)
    {
        var index = Array.FindIndex(Speeds, s => s >= (playback?.Speed ?? 1) - 0.01);
        var next = Speeds[Math.Clamp((index < 0 ? Speeds.Length - 1 : index) + step, 0, Speeds.Length - 1)];
        await Send(() => remote.SetSpeedAsync(next, CancellationToken.None));
    }

    // Commands talk to the game; when it is gone the poll ends the overlay, so failures are only logged.
    private async Task Send(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (HttpRequestException exception)
        {
            log.Warning("overlay.replay_api", exception);
        }
        catch (TaskCanceledException exception)
        {
            log.Warning("overlay.replay_api_timeout", exception);
        }
    }
}
