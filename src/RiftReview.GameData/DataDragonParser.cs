using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using RiftReview.Core.Matches;

namespace RiftReview.GameData;

/// <summary>
/// Parses Data Dragon's JSON files. Every id and icon path that later becomes part of a URL or a
/// cache file name is checked here, so nothing from the network can point outside the CDN or cache.
/// </summary>
public static partial class DataDragonParser
{
    /// <summary>Parses <c>api/versions.json</c>, newest first. Malformed entries are dropped.</summary>
    public static IReadOnlyList<string> ParseVersions(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .Where(v => VersionPattern().IsMatch(v))];
    }

    /// <summary>
    /// Picks the Data Dragon version for a replay: the newest one of the replay's patch, else the
    /// newest one overall. <c>null</c> when the list is empty.
    /// </summary>
    public static string? PickVersion(IReadOnlyList<string> versions, GameVersion replay)
    {
        ArgumentNullException.ThrowIfNull(versions);
        var prefix = string.Create(CultureInfo.InvariantCulture, $"{replay.Major}.{replay.Minor}.");
        return versions.FirstOrDefault(v => v.StartsWith(prefix, StringComparison.Ordinal)) ?? (versions.Count > 0 ? versions[0] : null);
    }

    /// <summary>Builds the catalog from the four data files of one version.</summary>
    public static GameCatalog ParseCatalog(string version, string championJson, string itemJson, string summonerJson, string runesJson) => new()
    {
        Version = version,
        Champions = ParseChampions(championJson),
        Items = ParseItems(itemJson),
        SummonerSpells = ParseSummonerSpells(summonerJson, version),
        Runes = ParseRunes(runesJson),
        ItemPrices = ParseItemPrices(itemJson),
    };

    /// <summary>Each item's total price (<c>gold.total</c>).</summary>
    public static IReadOnlyDictionary<int, int> ParseItemPrices(string json)
    {
        using var document = JsonDocument.Parse(json);
        var prices = new Dictionary<int, int>();
        foreach (var entry in document.RootElement.GetProperty("data").EnumerateObject())
        {
            if (int.TryParse(entry.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && entry.Value.TryGetProperty("gold", out var gold) && gold.TryGetProperty("total", out var total) && total.TryGetInt32(out var price))
            {
                prices[id] = price;
            }
        }

        return prices;
    }

    private static FrozenDictionary<string, ChampionInfo> ParseChampions(string json)
    {
        using var document = JsonDocument.Parse(json);
        var champions = new Dictionary<string, ChampionInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in document.RootElement.GetProperty("data").EnumerateObject())
        {
            var id = entry.Value.GetProperty("id").GetString();
            var name = entry.Value.GetProperty("name").GetString();
            if (id is not null && name is not null && SafeName().IsMatch(id))
            {
                champions[id] = new ChampionInfo(id, name);
            }
        }

        return champions.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static FrozenDictionary<int, GameObjectInfo> ParseItems(string json)
    {
        using var document = JsonDocument.Parse(json);
        var items = new Dictionary<int, GameObjectInfo>();
        foreach (var entry in document.RootElement.GetProperty("data").EnumerateObject())
        {
            if (int.TryParse(entry.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && entry.Value.GetProperty("name").GetString() is { } name)
            {
                items[id] = new GameObjectInfo(id, name, string.Empty);
            }
        }

        return items.ToFrozenDictionary();
    }

    private static FrozenDictionary<int, GameObjectInfo> ParseSummonerSpells(string json, string version)
    {
        using var document = JsonDocument.Parse(json);
        var spells = new Dictionary<int, GameObjectInfo>();
        foreach (var entry in document.RootElement.GetProperty("data").EnumerateObject())
        {
            var spell = entry.Value;
            if (int.TryParse(spell.GetProperty("key").GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var key)
                && spell.GetProperty("id").GetString() is { } id
                && SafeName().IsMatch(id)
                && spell.GetProperty("name").GetString() is { } name)
            {
                spells[key] = new GameObjectInfo(key, name, $"cdn/{version}/img/spell/{id}.png");
            }
        }

        return spells.ToFrozenDictionary();
    }

    private static FrozenDictionary<int, GameObjectInfo> ParseRunes(string json)
    {
        using var document = JsonDocument.Parse(json);
        var runes = new Dictionary<int, GameObjectInfo>();
        foreach (var tree in document.RootElement.EnumerateArray())
        {
            Add(runes, tree);
            foreach (var slot in tree.GetProperty("slots").EnumerateArray())
            {
                foreach (var rune in slot.GetProperty("runes").EnumerateArray())
                {
                    Add(runes, rune);
                }
            }
        }

        return runes.ToFrozenDictionary();

        static void Add(Dictionary<int, GameObjectInfo> into, JsonElement rune)
        {
            var icon = rune.GetProperty("icon").GetString();
            if (rune.GetProperty("id").TryGetInt32(out var id)
                && rune.GetProperty("name").GetString() is { } name
                && icon is not null
                && SafeIconPath().IsMatch(icon))
            {
                into[id] = new GameObjectInfo(id, name, $"cdn/img/{icon}");
            }
        }
    }

    /// <summary>The CDN path of an item icon.</summary>
    public static string ItemIconPath(string version, int itemId) =>
        string.Create(CultureInfo.InvariantCulture, $"cdn/{version}/img/item/{itemId}.png");

    /// <summary>The CDN path of a champion's square icon.</summary>
    public static string ChampionIconPath(string version, ChampionInfo champion)
    {
        ArgumentNullException.ThrowIfNull(champion);
        return $"cdn/{version}/img/champion/{champion.Id}.png";
    }

    [GeneratedRegex(@"^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,4}$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"^[A-Za-z0-9_]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeName();

    // Relative path of letters, digits, '_', '-', '.' segments ending in .png; no "..", no leading slash.
    [GeneratedRegex(@"^(?!.*\.\.)[A-Za-z0-9_\-]+(/[A-Za-z0-9_\-.]+)*\.png$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIconPath();
}
