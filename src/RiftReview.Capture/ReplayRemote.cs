namespace RiftReview.Capture;

/// <summary>Parts of League's own replay interface the overlay can switch off, by replay API render name.</summary>
public enum LeagueInterface
{
    /// <summary>League's play controls and timeline at the bottom (<c>interfaceTimeline</c>).</summary>
    ReplayControls,

    /// <summary>
    /// League's panel at the bottom left with the camera mode and the fog of war team choice (blue,
    /// red, all), which the replay API cannot set (<c>interfaceReplay</c>).
    /// </summary>
    CameraPanel,

    /// <summary>League's scoreboard at the bottom (<c>interfaceScoreboard</c>); the overlay has its own.</summary>
    Scoreboard,

    /// <summary>The chat (<c>interfaceChat</c>).</summary>
    Chat,

    /// <summary>The score at the top (<c>interfaceScore</c>).</summary>
    ScoreDisplay,

    /// <summary>The minimap (<c>interfaceMinimap</c>).</summary>
    Minimap,

    /// <summary>The target frame (<c>interfaceTarget</c>).</summary>
    TargetFrame,

    /// <summary>The objective timers (<c>interfaceNeutralTimers</c>).</summary>
    ObjectiveTimers,

    /// <summary>The team frames at the sides (<c>interfaceFrames</c>).</summary>
    TeamFrames,

    /// <summary>The kill feed and announcements (<c>interfaceKillCallouts</c>, <c>interfaceAnnounce</c>).</summary>
    KillCallouts,
}

/// <summary>
/// Drives a replay running in League through Riot's documented replay API (decision 0006): seek,
/// pause, speed, fog of war, the camera and which of League's own interface shows; it reads which
/// player League has selected. Nothing is typed or clicked into the game.
/// </summary>
public sealed class ReplayRemote(GameApi game)
{
    /// <summary>The playback state, or <c>null</c> while the replay is not running.</summary>
    public Task<Playback?> GetPlaybackAsync(CancellationToken cancellationToken) => game.GetPlaybackAsync(cancellationToken);

    /// <summary>Jumps to a game time in seconds.</summary>
    public Task SeekAsync(double time, CancellationToken cancellationToken) => game.SetPlaybackAsync(Math.Max(0, time), null, null, cancellationToken);

    /// <summary>Pauses or resumes.</summary>
    public Task SetPausedAsync(bool paused, CancellationToken cancellationToken) => game.SetPlaybackAsync(null, paused, null, cancellationToken);

    /// <summary>Sets the playback speed.</summary>
    public Task SetSpeedAsync(double speed, CancellationToken cancellationToken) => game.SetPlaybackAsync(null, null, speed, cancellationToken);

    /// <summary>Shows or hides the fog of war.</summary>
    public Task SetFogOfWarAsync(bool on, CancellationToken cancellationToken) =>
        game.SetRenderAsync(new Dictionary<string, object> { ["fogOfWar"] = on }, cancellationToken);

    /// <summary>The selected player and the camera, or <c>null</c> when the replay API did not answer readably.</summary>
    public async Task<RenderState?> GetRenderStateAsync(CancellationToken cancellationToken) =>
        RenderStateParser.Parse(await game.GetRenderAsync(cancellationToken).ConfigureAwait(false)).TryGetValue(out var state, out _) ? state : null;

    /// <summary>Selects a player as a click on them in League would (their target frame), by Riot ID game name.</summary>
    public Task SelectAsync(string gameName, CancellationToken cancellationToken) =>
        game.SetRenderAsync(new Dictionary<string, object> { ["selectionName"] = gameName }, cancellationToken);

    /// <summary>
    /// Puts the camera where <paramref name="view"/> says. League keeps a set camera only in its free
    /// camera ("fps"); the directed camera would move it straight back.
    /// </summary>
    public Task SetCameraAsync(CameraView view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        return game.SetRenderAsync(
            new Dictionary<string, object>
            {
                ["cameraMode"] = "fps",
                ["cameraAttached"] = false,
                ["cameraPosition"] = new Dictionary<string, double> { ["x"] = view.X, ["y"] = view.Y, ["z"] = view.Z },
                ["cameraRotation"] = new Dictionary<string, double> { ["x"] = view.Pitch, ["y"] = view.Yaw, ["z"] = view.Roll },
                ["fieldOfView"] = view.FieldOfView,
            },
            cancellationToken);
    }

    /// <summary>Shows or hides parts of League's own interface.</summary>
    public Task SetLeagueInterfaceAsync(LeagueInterface part, bool visible, CancellationToken cancellationToken) =>
        game.SetRenderAsync(RenderNames(part).ToDictionary(name => name, _ => (object)visible), cancellationToken);

    /// <summary>The replay API render names behind an interface part.</summary>
    public static IReadOnlyList<string> RenderNames(LeagueInterface part) => part switch
    {
        LeagueInterface.ReplayControls => ["interfaceTimeline"],
        LeagueInterface.CameraPanel => ["interfaceReplay"],
        LeagueInterface.Scoreboard => ["interfaceScoreboard"],
        LeagueInterface.Chat => ["interfaceChat"],
        LeagueInterface.ScoreDisplay => ["interfaceScore"],
        LeagueInterface.Minimap => ["interfaceMinimap"],
        LeagueInterface.TargetFrame => ["interfaceTarget"],
        LeagueInterface.ObjectiveTimers => ["interfaceNeutralTimers"],
        LeagueInterface.TeamFrames => ["interfaceFrames"],
        LeagueInterface.KillCallouts => ["interfaceKillCallouts", "interfaceAnnounce"],
        _ => throw new ArgumentOutOfRangeException(nameof(part), part, null),
    };
}
