using System.Globalization;
using RiftReview.App.Mvvm;
using RiftReview.Core.Matches;
using RiftReview.GameData;

namespace RiftReview.App.Replays;

/// <summary>One player's line on the scoreboard.</summary>
internal sealed class PlayerRowViewModel : ObservableObject
{
    private bool isOnRoster;

    public PlayerRowViewModel(PlayerStats stats, TimeSpan gameLength, long maxDamage, GameCatalog? catalog, IconProvider icons, bool isOnRoster)
    {
        ArgumentNullException.ThrowIfNull(stats);
        Stats = stats;
        this.isOnRoster = isOnRoster;

        var champion = catalog?.FindChampion(stats.Champion);
        Champion = new IconSlot(champion?.Name ?? stats.Champion);
        Champion.Load(icons, catalog is null || champion is null ? null : DataDragonParser.ChampionIconPath(catalog.Version, champion));

        SummonerSpells = [.. stats.SummonerSpells.Select(id => Slot(catalog?.SummonerSpells, id, icons))];
        Keystone = Slot(catalog?.Runes, stats.Keystone, icons);
        SecondaryTree = Slot(catalog?.Runes, stats.SecondaryRuneStyle, icons);
        Items = [.. stats.Items.Select(id => ItemSlot(catalog, id, icons))];

        KdaRatio = MatchFormat.KdaRatio(stats.Kills, stats.Deaths, stats.Assists);
        CsPerMinute = MatchFormat.PerMinute(stats.CreepScore, gameLength);
        Gold = MatchFormat.Thousands(stats.GoldEarned);
        Damage = stats.DamageToChampions.ToString("N0", CultureInfo.CurrentCulture);
        DamageShare = maxDamage <= 0 ? 0 : (double)stats.DamageToChampions / maxDamage;
        Role = MatchFormat.Role(stats.Position);
    }

    public PlayerStats Stats { get; }

    public IconSlot Champion { get; }

    public IReadOnlyList<IconSlot> SummonerSpells { get; }

    public IconSlot Keystone { get; }

    public IconSlot SecondaryTree { get; }

    public IReadOnlyList<IconSlot> Items { get; }

    public string Role { get; }

    public string Kda => $"{Stats.Kills} / {Stats.Deaths} / {Stats.Assists}";

    public string KdaRatio { get; }

    public string CsPerMinute { get; }

    public string Gold { get; }

    public string Damage { get; }

    /// <summary>Damage to champions relative to the highest in the game, 0 to 1, for the bar.</summary>
    public double DamageShare { get; }

    public bool IsBlue => Stats.Team == Team.Blue;

    public bool IsOnRoster
    {
        get => isOnRoster;
        set => SetProperty(ref isOnRoster, value);
    }

    private static IconSlot Slot(IReadOnlyDictionary<int, GameObjectInfo>? lookup, int id, IconProvider icons)
    {
        var info = lookup?.GetValueOrDefault(id);
        var slot = new IconSlot(info?.Name ?? id.ToString(CultureInfo.InvariantCulture), isEmpty: id == 0);
        slot.Load(icons, info?.IconPath);
        return slot;
    }

    private static IconSlot ItemSlot(GameCatalog? catalog, int id, IconProvider icons)
    {
        var name = catalog?.Items.GetValueOrDefault(id)?.Name ?? id.ToString(CultureInfo.InvariantCulture);
        var slot = new IconSlot(name, isEmpty: id == 0);
        slot.Load(icons, catalog is null || id == 0 ? null : DataDragonParser.ItemIconPath(catalog.Version, id));
        return slot;
    }
}
