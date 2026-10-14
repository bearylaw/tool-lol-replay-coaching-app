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

    // A running replay is this match when the lengths agree this closely (the replay runs a few
    // seconds past the game's end).
    private const double SameReplaySeconds = 20;
    private OverlayWindow? window;

    /// <summary>Starts the replay and opens the overlay; reports progress through <paramref name="status"/>.</summary>
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
        MatchTimeline? timeline, IReadOnlyList<PlayerRowViewModel> rows, Func<int, IconSlot> itemIcon, OverlaySettings settings,
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
        var attached = await RunningReplayAsync(game, timeline);
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
    private async Task<bool?> RunningReplayAsync(GameApi game, MatchTimeline? timeline)
    {
        if (await game.GetPlaybackAsync(CancellationToken.None) is not { Length: > 0 } running)
        {
            return null;
        }

        if (timeline is not null && Math.Abs(running.Length - timeline.GameLength.TotalSeconds) <= SameReplaySeconds)
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
