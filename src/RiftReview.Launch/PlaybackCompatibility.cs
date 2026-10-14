using RiftReview.Core.Matches;

namespace RiftReview.Launch;

/// <summary>Whether the installed game can play a replay. A replay plays only on the build that recorded it.</summary>
public enum Compatibility
{
    /// <summary>Same build: the replay plays.</summary>
    Compatible,

    /// <summary>Same patch, other build (a hotfix): it may play, it may not.</summary>
    DifferentBuild,

    /// <summary>Another patch: the game refuses or crashes.</summary>
    DifferentPatch,

    /// <summary>The installed game's version could not be read.</summary>
    Unknown,
}

/// <summary>Compares a replay's build with the installed game's build.</summary>
public static class PlaybackCompatibility
{
    /// <summary>Classifies whether <paramref name="installed"/> can play a replay recorded on <paramref name="replay"/>.</summary>
    public static Compatibility Check(GameVersion replay, GameVersion? installed)
    {
        if (installed is not { } version)
        {
            return Compatibility.Unknown;
        }

        if (version == replay)
        {
            return Compatibility.Compatible;
        }

        return version.IsSamePatch(replay) ? Compatibility.DifferentBuild : Compatibility.DifferentPatch;
    }
}
