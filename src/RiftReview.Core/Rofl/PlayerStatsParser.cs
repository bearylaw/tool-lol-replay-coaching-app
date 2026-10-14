using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using RiftReview.Core.Matches;
using RiftReview.Core.Results;

namespace RiftReview.Core.Rofl;

/// <summary>
/// Turns the <c>statsJson</c> string of a replay into typed player stats. Riot writes every value
/// as a string and adds or drops stats between patches, so a missing number reads as zero; a
/// value that is present but not a number is an error.
/// </summary>
public static class PlayerStatsParser
{
    private static readonly FrozenDictionary<string, Position> Positions = new Dictionary<string, Position>
    {
        ["TOP"] = Position.Top,
        ["JUNGLE"] = Position.Jungle,
        ["MIDDLE"] = Position.Middle,
        ["BOTTOM"] = Position.Bottom,
        ["UTILITY"] = Position.Utility,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Parses the JSON array of players. An empty array (a remake) is valid.</summary>
    public static Result<IReadOnlyList<PlayerStats>, string> Parse(string statsJson)
    {
        ArgumentNullException.ThrowIfNull(statsJson);
        try
        {
            using var document = JsonDocument.Parse(statsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Result<IReadOnlyList<PlayerStats>, string>.Failure("statsJson is not a JSON array");
            }

            var players = new List<PlayerStats>();
            var index = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var player = ParsePlayer(element);
                if (!player.TryGetValue(out var parsed, out var error))
                {
                    return Result<IReadOnlyList<PlayerStats>, string>.Failure($"player {index}: {error}");
                }

                players.Add(parsed);
                index++;
            }

            return Result<IReadOnlyList<PlayerStats>, string>.Success(players);
        }
        catch (JsonException exception)
        {
            return Result<IReadOnlyList<PlayerStats>, string>.Failure($"statsJson is not valid JSON: {exception.Message}");
        }
    }

    private static Result<PlayerStats, string> ParsePlayer(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return Result<PlayerStats, string>.Failure("entry is not a JSON object");
        }

        var raw = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            raw[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()!
                : property.Value.GetRawText();
        }

        try
        {
            return Result<PlayerStats, string>.Success(Build(new StatSource(raw)));
        }
        catch (FormatException exception)
        {
            return Result<PlayerStats, string>.Failure(exception.Message);
        }
    }

    private static PlayerStats Build(StatSource s) => new()
    {
        GameName = s.Text("RIOT_ID_GAME_NAME") is { Length: > 0 } name ? name : s.Text("NAME"),
        TagLine = s.Text("RIOT_ID_TAG_LINE"),
        Champion = s.Required("SKIN"),
        Team = s.Int("TEAM") switch
        {
            100 => Team.Blue,
            200 => Team.Red,
            _ => Team.Other,
        },
        Position = Positions.GetValueOrDefault(s.Text("TEAM_POSITION"), Position.None),
        Won = string.Equals(s.Text("WIN"), "Win", StringComparison.OrdinalIgnoreCase),
        Level = s.Int("LEVEL"),
        Kills = s.Int("CHAMPIONS_KILLED"),
        Deaths = s.Int("NUM_DEATHS"),
        Assists = s.Int("ASSISTS"),
        MinionsKilled = s.Int("MINIONS_KILLED"),
        NeutralMinionsKilled = s.Int("NEUTRAL_MINIONS_KILLED"),
        GoldEarned = s.Int("GOLD_EARNED"),
        DamageToChampions = s.Long("TOTAL_DAMAGE_DEALT_TO_CHAMPIONS"),
        DamageTaken = s.Long("TOTAL_DAMAGE_TAKEN"),
        DamageToBuildings = s.Long("TOTAL_DAMAGE_DEALT_TO_BUILDINGS"),
        DamageSelfMitigated = s.Long("TOTAL_DAMAGE_SELF_MITIGATED"),
        HealOnTeammates = s.Long("TOTAL_HEAL_ON_TEAMMATES"),
        ShieldOnTeammates = s.Long("TOTAL_DAMAGE_SHIELDED_ON_TEAMMATES"),
        VisionScore = s.Int("VISION_SCORE"),
        WardsPlaced = s.Int("WARD_PLACED"),
        WardsKilled = s.Int("WARD_KILLED"),
        ControlWardsBought = s.Int("VISION_WARDS_BOUGHT_IN_GAME"),
        TurretTakedowns = s.Int("TURRET_TAKEDOWNS"),
        DragonKills = s.Int("DRAGON_KILLS"),
        BaronKills = s.Int("BARON_KILLS"),
        LargestMultiKill = s.Int("LARGEST_MULTI_KILL"),
        LargestKillingSpree = s.Int("LARGEST_KILLING_SPREE"),
        CrowdControlSeconds = s.Int("TIME_CCING_OTHERS"),
        SecondsDead = s.Int("TOTAL_TIME_SPENT_DEAD"),
        Items = s.Ints("ITEM0", "ITEM1", "ITEM2", "ITEM3", "ITEM4", "ITEM5", "ITEM6"),
        SummonerSpells = s.Ints("SUMMONER_SPELL_1", "SUMMONER_SPELL_2"),
        Runes = s.Ints("PERK0", "PERK1", "PERK2", "PERK3", "PERK4", "PERK5"),
        PrimaryRuneStyle = s.Int("PERK_PRIMARY_STYLE"),
        SecondaryRuneStyle = s.Int("PERK_SUB_STYLE"),
        RawStats = s.Raw.ToFrozenDictionary(StringComparer.Ordinal),
    };

    private readonly record struct StatSource(Dictionary<string, string> Raw)
    {
        public string Text(string key) => Raw.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

        public string Required(string key) =>
            Text(key) is { Length: > 0 } value ? value : throw new FormatException($"{key} is missing");

        public long Long(string key)
        {
            var text = Text(key);
            if (text.Length == 0)
            {
                return 0;
            }

            return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new FormatException($"{key} is \"{text}\", expected a whole number");
        }

        public int Int(string key) => (int)Math.Clamp(Long(key), int.MinValue, int.MaxValue);

        public IReadOnlyList<int> Ints(params string[] keys) => [.. keys.Select(Int)];
    }
}
