using System.Diagnostics;
using RiftReview.App.Capture;
using RiftReview.App.Localization;
using RiftReview.App.Replays;
using RiftReview.App.Settings;
using RiftReview.Capture;
using RiftReview.Core.Logging;
using RiftReview.Core.Timelines;
using RiftReview.Launch;

namespace RiftReview.App.Overlay;

/// <summary>
/// Starts a replay in League with Rift Review's overlay on top: switches on the replay API if
/// needed (asked, with a backup), starts the replay through the client, waits for the game window
/// and opens the overlay over it. When the same replay already runs, the overlay attaches to it.
/// One overlay at a time.
/// </summary>
internal sealed class OverlayCoordinator(AppLog log, ReplayGame replayGame, Shell.Dialogs dialogs)
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(4);
    private OverlayWindow? window;

    /// <summary>Makes sure Riot's replay API is on (asked, with a backup); false when it is not.</summary>
    public bool EnsureReplayApi(LeagueInstallation league) => replayGame.EnsureReplayApi(league);

    /// <summary>
    /// The replay open in League right now with the replay API on, and its players when the Live
    /// Client Data API answers; <c>null</c> when none runs.
    /// </summary>
    public async Task<RunningReplay?> FindRunningReplayAsync()
    {
        using var game = new GameApi();
        if (await game.GetPlaybackAsync(CancellationToken.None) is not { Length: > 0 } playback)
        {
            return null;
        }

        try
        {
            var parsed = LiveGameDataParser.ParseAllGameData(await game.GetAllGameDataAsync(CancellationToken.None));
            if (parsed.TryGetValue(out var snapshot, out var failure))
            {
                return new RunningReplay(playback.Length, [.. snapshot.Players.Select(p => p.RiotId).Where(id => id.Length > 0)]);
            }

            log.Warning("overlay.live_players", null, ("detail", failure));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // Without the players the replay is still found when only one library replay has its length.
            log.Warning("overlay.live_players", exception);
        }

        return new RunningReplay(playback.Length, []);
    }

    /// <summary>Starts the replay and opens the overlay; reports progress through <paramref name="status"/>.</summary>
    /// <param name="gameLength">The match's length, to tell whether a replay already running is this one.</param>
    /// <param name="timeline">The replay's timeline, for the scrubber marks and the follow camera; <c>null</c> when not loaded.</param>
    /// <param name="rows">The players in player order.</param>
    /// <param name="itemIcon">Item icons by id.</param>
    /// <param name="settings">How the overlay looks and what of League's interface it hides.</param>
    /// <param name="league">The League install, for the replay API switch.</param>
    /// <param name="startInClient">Starts the replay through the League client; false when it did not.</param>
    /// <param name="status">Shows a line in the main window's status bar.</param>
    /// <param name="bookmarks">The replay's bookmarks; <c>null</c> when the replay cannot be identified.</param>
    /// <param name="itemPrices">Item prices by id, for the scoreboard's item gold.</param>
    public async Task OpenAsync(
        TimeSpan gameLength, MatchTimeline? timeline, IReadOnlyList<PlayerRowViewModel> rows, Func<int, IconSlot> itemIcon, OverlaySettings settings,
        LeagueInstallation league, Func<Task<bool>> startInClient, Action<string> status, BookmarkList? bookmarks = null, IReadOnlyDictionary<int, int>? itemPrices = null)
    {
        ArgumentNullException.ThrowIfNull(startInClient);
        ArgumentNullException.ThrowIfNull(status);
        if (window is not null || !replayGame.EnsureReplayApi(league))
        {
            return;
        }

        // Declining only means League opens in its own last camera; the overlay works either way.
        if (settings.DirectedCamera)
        {
            replayGame.EnsureDirectedCamera(league);
        }

        var game = new GameApi();
        var started = DateTime.Now;
        var attached = await RunningReplayAsync(game, gameLength);
        if (attached is false || (attached is null && !await StartAsync(game, startInClient, status)))
        {
            game.Dispose();
            return;
        }

        var viewModel = new OverlayViewModel(new ReplayRemote(game), game, timeline, rows, itemIcon, settings, log, canEndReplay: attached is null, bookmarks, itemPrices);
        window = new OverlayWindow(viewModel);
        viewModel.Closed += async (_, end) =>
        {
            // async void: a UI event; restoring League's interface only logs failures.
            window?.Close();
            window = null;
            viewModel.Dispose();
            if (end == OverlayEnd.CloseOverlay)
            {
                await viewModel.RestoreLeagueInterfaceAsync();
            }

            if (end == OverlayEnd.EndReplay)
            {
                replayGame.CloseReplayStartedAfter(started);
            }

            game.Dispose();
            log.Info("overlay.closed", ("reason", end));
            status(Loc.Get("Overlay_Closed"));
        };
        window.Show();
        await viewModel.StartAsync();
        log.Info("overlay.opened", ("bookmarks", bookmarks is not null));
        status(Loc.Get("Overlay_Open"));
    }

    // true: this replay already runs (attach); false: another one runs (reported); null: none runs.
    private async Task<bool?> RunningReplayAsync(GameApi game, TimeSpan gameLength)
    {
        if (await game.GetPlaybackAsync(CancellationToken.None) is not { Length: > 0 } running)
        {
            return null;
        }

        if (new RunningReplay(running.Length, []).HasLength(gameLength))
        {
            log.Info("overlay.attached", ("length", running.Length));
            return true;
        }

        dialogs.Error(Loc.Get("Overlay_OtherReplay"));
        return false;
    }

    private static async Task<bool> StartAsync(GameApi game, Func<Task<bool>> startInClient, Action<string> status)
    {
        if (!await startInClient())
        {
            return false;
        }

        status(Loc.Get("Overlay_Waiting"));
        if (await WaitForReplayAsync(game))
        {
            return true;
        }

        status(Loc.Get("Overlay_NoReplay"));
        return false;
    }

    private static async Task<bool> WaitForReplayAsync(GameApi game)
    {
        var waited = Stopwatch.StartNew();
        while (waited.Elapsed < StartTimeout)
        {
            if (await game.GetPlaybackAsync(CancellationToken.None) is { Length: > 0 } && GameWindow.Find() is not null)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        return false;
    }
}
