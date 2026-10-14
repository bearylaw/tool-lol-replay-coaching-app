namespace RiftReview.Core.Matches;

/// <summary>The side a player is on. The numbers are Riot's team ids.</summary>
public enum Team
{
    /// <summary>A team id other than 100 or 200, for example in Arena.</summary>
    Other = 0,

    /// <summary>Blue side, team id 100, bottom-left base.</summary>
    Blue = 100,

    /// <summary>Red side, team id 200, top-right base.</summary>
    Red = 200,
}
