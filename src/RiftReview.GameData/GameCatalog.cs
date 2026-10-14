using System.Collections.Frozen;

namespace RiftReview.GameData;

/// <summary>A champion: Data Dragon id (also its icon file name) and display name.</summary>
public sealed record ChampionInfo(string Id, string Name);

/// <summary>An item, summoner spell or rune: display name and the icon's path relative to the CDN.</summary>
public sealed record GameObjectInfo(int Id, string Name, string IconPath);

/// <summary>Names and icon paths for one Data Dragon version.</summary>
public sealed record GameCatalog
{
    /// <summary>The Data Dragon version, for example <c>16.20.1</c>.</summary>
    public required string Version { get; init; }

    /// <summary>Champions by Data Dragon id, case-insensitive (replays write <c>FiddleSticks</c>, Data Dragon <c>Fiddlesticks</c>).</summary>
    public required FrozenDictionary<string, ChampionInfo> Champions { get; init; }

    /// <summary>Items by id.</summary>
    public required FrozenDictionary<int, GameObjectInfo> Items { get; init; }

    /// <summary>Summoner spells by their numeric key, as replays store them.</summary>
    public required FrozenDictionary<int, GameObjectInfo> SummonerSpells { get; init; }

    /// <summary>Runes and rune trees by id.</summary>
    public required FrozenDictionary<int, GameObjectInfo> Runes { get; init; }

    /// <summary>Each item's total price in gold, by item id; empty when not parsed.</summary>
    public IReadOnlyDictionary<int, int> ItemPrices { get; init; } = new Dictionary<int, int>();

    /// <summary>The champion for a replay's internal champion name, or <c>null</c> when unknown.</summary>
    public ChampionInfo? FindChampion(string internalName) => Champions.GetValueOrDefault(internalName);
}
