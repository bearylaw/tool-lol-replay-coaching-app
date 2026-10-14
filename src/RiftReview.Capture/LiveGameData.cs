using System.Globalization;
using System.Text.Json;
using RiftReview.Core.Results;

namespace RiftReview.Capture;

/// <summary>An item a player holds.</summary>
public sealed record LiveItem(int Id, int Slot);

/// <summary>
/// One player as the game's Live Client Data API reports them at a moment: Riot ID (<c>Name#Tag</c>),
/// champion key as the replay names it (<c>MonkeyKing</c> for Wukong), side (ORDER is blue), level, life state with seconds until respawn, score, items and
/// the summoner spells' display names (they change when Smite or Teleport is upgraded).
/// </summary>
public sealed record LivePlayer(
    string RiotId,
    string Champion,
    bool IsBlue,
    int Level,
    bool IsDead,
    double RespawnSeconds,
    int Kills,
    int Deaths,
    int Assists,
    int CreepScore,
    double WardScore,
    IReadOnlyList<LiveItem> Items,
    IReadOnlyList<string> SummonerSpellNames);

/// <summary>Everything about the players at one game time.</summary>
public sealed record LiveSnapshot(double GameTime, IReadOnlyList<LivePlayer> Players);

/// <summary>
/// An entry of the game's event feed (kills, objectives, structures, multikills). <c>Name</c> is
/// Riot's event name such as <c>ChampionKill</c>; <c>Killer</c> and <c>Victim</c> are Riot ID game
/// names, champion or unit names, <c>null</c> when absent; <c>Detail</c> holds the dragon type,
/// turret or inhibitor name, multikill streak or acing team.
/// </summary>
public sealed record LiveEvent(int Id, string Name, double Time, string? Killer, string? Victim, IReadOnlyList<string> Assisters, string? Detail);

/// <summary>Parses the Live Client Data API's JSON (<c>/liveclientdata/allgamedata</c>, <c>/eventdata</c>).</summary>
public static class LiveGameDataParser
{
    /// <summary>Parses <c>allgamedata</c> into a snapshot.</summary>
    public static Result<LiveSnapshot, string> ParseAllGameData(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var time = root.TryGetProperty("gameData", out var game) && game.TryGetProperty("gameTime", out var t) && t.TryGetDouble(out var seconds)
                ? seconds
                : double.NaN;
            if (double.IsNaN(time) || !root.TryGetProperty("allPlayers", out var players) || players.ValueKind != JsonValueKind.Array)
            {
                return Result<LiveSnapshot, string>.Failure("allgamedata has no gameData.gameTime or allPlayers");
            }

            return Result<LiveSnapshot, string>.Success(new LiveSnapshot(time, [.. players.EnumerateArray().Select(Player)]));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return Result<LiveSnapshot, string>.Failure($"allgamedata is not readable: {exception.Message}");
        }
    }

    /// <summary>Parses <c>eventdata</c> into the event list.</summary>
    public static Result<IReadOnlyList<LiveEvent>, string> ParseEvents(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("Events", out var events) || events.ValueKind != JsonValueKind.Array)
            {
                return Result<IReadOnlyList<LiveEvent>, string>.Failure("eventdata has no Events array");
            }

            return Result<IReadOnlyList<LiveEvent>, string>.Success([.. events.EnumerateArray().Select(Event)]);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return Result<IReadOnlyList<LiveEvent>, string>.Failure($"eventdata is not readable: {exception.Message}");
        }
    }

    private static LivePlayer Player(JsonElement p)
    {
        var scores = p.TryGetProperty("scores", out var s) ? s : default;
        return new LivePlayer(
            Text(p, "riotId") ?? Text(p, "summonerName") ?? string.Empty,
            ChampionKey(p),
            string.Equals(Text(p, "team"), "ORDER", StringComparison.OrdinalIgnoreCase),
            Int(p, "level"),
            p.TryGetProperty("isDead", out var dead) && dead.ValueKind == JsonValueKind.True,
            Double(p, "respawnTimer"),
            Int(scores, "kills"),
            Int(scores, "deaths"),
            Int(scores, "assists"),
            Int(scores, "creepScore"),
            Double(scores, "wardScore"),
            p.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
                ? [.. items.EnumerateArray().Select(i => new LiveItem(Int(i, "itemID"), Int(i, "slot")))]
                : [],
            SpellNames(p));
    }

    private const string RawChampionPrefix = "game_character_displayname_";

    // championName is the localized display name ("Wukong"); the replay names champions by their
    // internal key ("MonkeyKing"), which rawChampionName carries after its prefix.
    private static string ChampionKey(JsonElement p)
    {
        var raw = Text(p, "rawChampionName");
        return raw is not null && raw.StartsWith(RawChampionPrefix, StringComparison.Ordinal) && raw.Length > RawChampionPrefix.Length
            ? raw[RawChampionPrefix.Length..]
            : Text(p, "championName") ?? string.Empty;
    }

    private static readonly string[] SpellKeys = ["summonerSpellOne", "summonerSpellTwo"];

    private static List<string> SpellNames(JsonElement p)
    {
        if (!p.TryGetProperty("summonerSpells", out var spells) || spells.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return [.. SpellKeys
            .Select(k => spells.TryGetProperty(k, out var spell) ? Text(spell, "displayName") ?? string.Empty : string.Empty)];
    }

    private static LiveEvent Event(JsonElement e) => new(
        Int(e, "EventID"),
        Text(e, "EventName") ?? string.Empty,
        Double(e, "EventTime"),
        Text(e, "KillerName") ?? Text(e, "Acer"),
        Text(e, "VictimName"),
        e.TryGetProperty("Assisters", out var a) && a.ValueKind == JsonValueKind.Array
            ? [.. a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!)]
            : [],
        Text(e, "DragonType") ?? Text(e, "TurretKilled") ?? Text(e, "InhibKilled") ?? Text(e, "AcingTeam")
            ?? (e.TryGetProperty("KillStreak", out var streak) ? streak.ToString() : null));

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int Int(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? (int)Math.Round(value.GetDouble())
            : 0;

    private static double Double(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var text) && text.ValueKind == JsonValueKind.String
                && double.TryParse(text.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
}
