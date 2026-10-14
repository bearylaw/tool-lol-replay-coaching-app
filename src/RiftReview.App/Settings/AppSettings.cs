namespace RiftReview.App.Settings;

/// <summary>What the coach configures. Stored as JSON in <c>%APPDATA%\RiftReview\settings.json</c>.</summary>
internal sealed record AppSettings
{
    /// <summary>The schema version written to disk; bump it and add a migration step when the shape changes.</summary>
    public const int CurrentVersion = 2;

    /// <summary>The League client's replay folder.</summary>
    public required string OwnReplaysFolder { get; init; }

    /// <summary>The folder for replays players send.</summary>
    public required string PlayerReplaysFolder { get; init; }

    /// <summary>The League install folder when automatic detection is not wanted; <c>null</c> to detect.</summary>
    public string? LeagueInstallOverride { get; init; }

    /// <summary>Riot IDs (<c>Name#TAG</c>) of the coach's players, highlighted everywhere.</summary>
    public IReadOnlyList<string> Roster { get; init; } = [];

    /// <summary>Size of the whole main window's content, 0.8–1.25.</summary>
    public double InterfaceScale { get; init; } = 1;

    /// <summary>The overlay on League's replay window.</summary>
    public OverlaySettings Overlay { get; init; } = new();

    /// <summary>Whether a Riot ID belongs to the roster, ignoring case.</summary>
    public bool IsOnRoster(string riotId) => Roster.Contains(riotId, StringComparer.OrdinalIgnoreCase);

    /// <summary>The same settings with every number inside its range, for a hand-edited file.</summary>
    public AppSettings Normalized() => this with
    {
        InterfaceScale = Math.Clamp(InterfaceScale, 0.8, 1.25),
        Overlay = Overlay.Normalized(),
    };
}

/// <summary>The overlay drawn over League's replay window, and which of League's own interface it replaces.</summary>
internal sealed record OverlaySettings
{
    /// <summary>Size of the overlay's panels, 0.6–1.6.</summary>
    public double Scale { get; init; } = 1;

    /// <summary>
    /// Rift Review's own scoreboard (its button next to the timeline); off, it is not loaded and the
    /// button is gone, and League's scoreboard (Tab, O) is the only one.
    /// </summary>
    public bool ShowOwnScoreboard { get; init; }

    /// <summary>Hide League's replay controls and timeline; the overlay's bar replaces them.</summary>
    public bool HideLeagueControls { get; init; } = true;

    /// <summary>Hide League's camera and vision panel at the bottom left (it holds the team fog choice).</summary>
    public bool HideLeagueCameraPanel { get; init; }

    /// <summary>Open replays in League's directed camera (game.cfg, asked once with a backup).</summary>
    public bool DirectedCamera { get; init; } = true;

    /// <summary>Pause the replay while the coach writes a bookmark's note.</summary>
    public bool PauseWhileNoting { get; init; }

    /// <summary>Pause the replay when the coach starts drawing.</summary>
    public bool PauseWhenDrawing { get; init; } = true;

    /// <summary>Clear the drawings when the replay jumps (timeline, ±10 s).</summary>
    public bool ClearDrawingsOnJump { get; init; } = true;

    /// <summary>Seconds after which a drawing fades away, 0 for never, up to 60.</summary>
    public double DrawingFadeSeconds { get; init; }

    /// <summary>The same settings with the numbers inside their ranges.</summary>
    public OverlaySettings Normalized() => this with { Scale = Math.Clamp(Scale, 0.6, 1.6), DrawingFadeSeconds = Math.Clamp(DrawingFadeSeconds, 0, 60) };
}
