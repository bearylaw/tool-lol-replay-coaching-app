namespace RiftReview.Core.Matches;

/// <summary>The role a player was assigned on Summoner's Rift.</summary>
public enum Position
{
    /// <summary>No role, for example in ARAM or Arena.</summary>
    None,

    /// <summary>Top lane.</summary>
    Top,

    /// <summary>Jungle.</summary>
    Jungle,

    /// <summary>Middle lane.</summary>
    Middle,

    /// <summary>Bottom lane carry.</summary>
    Bottom,

    /// <summary>Support.</summary>
    Utility,
}
