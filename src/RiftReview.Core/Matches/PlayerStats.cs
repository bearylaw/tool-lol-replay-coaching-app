namespace RiftReview.Core.Matches;

/// <summary>
/// One player's end-of-game summary as stored in a replay. The typed properties cover what the
/// scoreboard shows; <see cref="RawStats"/> keeps every stat Riot wrote, for the full stats table.
/// </summary>
public sealed record PlayerStats
{
    /// <summary>Riot ID name part, for example <c>Faker</c>. Falls back to the summoner name.</summary>
    public required string GameName { get; init; }

    /// <summary>Riot ID tag without the <c>#</c>, empty when the replay has none.</summary>
    public required string TagLine { get; init; }

    /// <summary>Riot's internal champion name, for example <c>MonkeyKing</c> for Wukong.</summary>
    public required string Champion { get; init; }

    /// <summary>The side the player was on.</summary>
    public required Team Team { get; init; }

    /// <summary>The assigned role.</summary>
    public required Position Position { get; init; }

    /// <summary>Whether the player's team won.</summary>
    public required bool Won { get; init; }

    /// <summary>Champion level at the end of the game.</summary>
    public int Level { get; init; }

    /// <summary>Champion kills.</summary>
    public int Kills { get; init; }

    /// <summary>Deaths.</summary>
    public int Deaths { get; init; }

    /// <summary>Kill assists.</summary>
    public int Assists { get; init; }

    /// <summary>Lane minions killed.</summary>
    public int MinionsKilled { get; init; }

    /// <summary>Jungle monsters killed.</summary>
    public int NeutralMinionsKilled { get; init; }

    /// <summary>Total gold earned.</summary>
    public int GoldEarned { get; init; }

    /// <summary>Damage dealt to enemy champions.</summary>
    public long DamageToChampions { get; init; }

    /// <summary>Damage taken from all sources.</summary>
    public long DamageTaken { get; init; }

    /// <summary>Damage dealt to turrets, inhibitors and the nexus.</summary>
    public long DamageToBuildings { get; init; }

    /// <summary>Damage mitigated by own resistances and shields.</summary>
    public long DamageSelfMitigated { get; init; }

    /// <summary>Healing done on teammates.</summary>
    public long HealOnTeammates { get; init; }

    /// <summary>Shields placed on teammates.</summary>
    public long ShieldOnTeammates { get; init; }

    /// <summary>Vision score.</summary>
    public int VisionScore { get; init; }

    /// <summary>Wards placed, including control wards.</summary>
    public int WardsPlaced { get; init; }

    /// <summary>Enemy wards destroyed.</summary>
    public int WardsKilled { get; init; }

    /// <summary>Control wards bought.</summary>
    public int ControlWardsBought { get; init; }

    /// <summary>Turrets the player killed or assisted on.</summary>
    public int TurretTakedowns { get; init; }

    /// <summary>Dragons the player killed (last hit).</summary>
    public int DragonKills { get; init; }

    /// <summary>Barons the player killed (last hit).</summary>
    public int BaronKills { get; init; }

    /// <summary>Largest multi kill: 2 for a double kill up to 5 for a penta kill.</summary>
    public int LargestMultiKill { get; init; }

    /// <summary>Largest killing spree.</summary>
    public int LargestKillingSpree { get; init; }

    /// <summary>Seconds of crowd control applied to enemy champions.</summary>
    public int CrowdControlSeconds { get; init; }

    /// <summary>Seconds spent dead.</summary>
    public int SecondsDead { get; init; }

    /// <summary>Item ids in slots 0 to 6; slot 6 is the trinket. Zero means an empty slot.</summary>
    public IReadOnlyList<int> Items { get; init; } = [];

    /// <summary>The two summoner spell ids (Riot's numeric keys).</summary>
    public IReadOnlyList<int> SummonerSpells { get; init; } = [];

    /// <summary>The six chosen rune ids; the first is the keystone.</summary>
    public IReadOnlyList<int> Runes { get; init; } = [];

    /// <summary>The primary rune tree id, for example 8000 for Precision.</summary>
    public int PrimaryRuneStyle { get; init; }

    /// <summary>The secondary rune tree id.</summary>
    public int SecondaryRuneStyle { get; init; }

    /// <summary>Every stat exactly as Riot wrote it, keyed by Riot's stat name.</summary>
    public IReadOnlyDictionary<string, string> RawStats { get; init; } = new Dictionary<string, string>();

    /// <summary>Lane minions plus jungle monsters.</summary>
    public int CreepScore => MinionsKilled + NeutralMinionsKilled;

    /// <summary>The Riot ID as players write it, <c>Name#TAG</c>, or the name alone without a tag.</summary>
    public string RiotId => TagLine.Length == 0 ? GameName : $"{GameName}#{TagLine}";

    /// <summary>The keystone rune id, or zero when unknown.</summary>
    public int Keystone => Runes.Count > 0 ? Runes[0] : 0;
}
