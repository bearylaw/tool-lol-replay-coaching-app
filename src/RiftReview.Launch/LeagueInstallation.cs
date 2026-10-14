using RiftReview.Core.Matches;

namespace RiftReview.Launch;

/// <summary>An installed League of Legends game, as far as playing replays needs it.</summary>
public sealed record LeagueInstallation
{
    /// <summary>The install root, for example <c>C:\Riot Games\League of Legends</c>, without a trailing separator.</summary>
    public required string InstallDirectory { get; init; }

    /// <summary>Full path of <c>Game\League of Legends.exe</c>.</summary>
    public required string GameExecutable { get; init; }

    /// <summary>The game build, or <c>null</c> when the executable carries no readable version.</summary>
    public required GameVersion? Version { get; init; }

    /// <summary>The client language, for example <c>en_GB</c>.</summary>
    public required string Locale { get; init; }

    /// <summary>The client's region, for example <c>EUW</c>, or <c>null</c> when unknown.</summary>
    public required string? Region { get; init; }

    /// <summary>The working directory the game expects: the <c>Game</c> folder.</summary>
    public string GameDirectory => Path.GetDirectoryName(GameExecutable)!;
}
