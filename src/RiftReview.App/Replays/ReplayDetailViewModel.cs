using System.Globalization;
using RiftReview.App.Localization;
using RiftReview.App.Mvvm;
using RiftReview.App.Timelines;
using RiftReview.Core.Matches;
using RiftReview.GameData;

namespace RiftReview.App.Replays;

/// <summary>Everything the detail pane shows for one readable replay.</summary>
internal sealed class ReplayDetailViewModel : ObservableObject, IDisposable
{
    // Account identifiers, mission counters and seasonal event counters (keys starting with a
    // year, such as 2026_S1A1_...) say nothing about the game.
    private static readonly string[] HiddenStatPrefixes = ["PUUID", "SUMMONER_ID", "Missions_", "PlayerBehavior_"];

    private string statFilter = string.Empty;
    private Core.Timelines.MatchTimeline? loadedTimeline;

    public ReplayDetailViewModel(ReplayItemViewModel item, ReplayMetadata metadata, GameCatalog? catalog, IconProvider icons, Func<string, bool> isOnRoster)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(isOnRoster);

        Item = item;
        Metadata = metadata;
        var maxDamage = metadata.Players.Count == 0 ? 0 : metadata.Players.Max(p => p.DamageToChampions);
        var rows = metadata.Players
            .Select(p => new PlayerRowViewModel(p, metadata.GameLength, maxDamage, catalog, icons, isOnRoster(p.RiotId)))
            .ToList();
        AllPlayers = rows;
        Blue = new TeamViewModel(Team.Blue, [.. rows.Where(r => r.Stats.Team == Team.Blue)]);
        Red = new TeamViewModel(Team.Red, [.. rows.Where(r => r.Stats.Team != Team.Blue)]);

        Comparisons =
        [
            TeamComparison.Of(Loc.Get("Compare_Kills"), Blue.Kills, Red.Kills),
            TeamComparison.Of(Loc.Get("Compare_Gold"), Blue.Gold, Red.Gold, MatchFormat.Thousands),
            TeamComparison.Of(Loc.Get("Compare_Damage"), Blue.Damage, Red.Damage, MatchFormat.Thousands),
            TeamComparison.Of(Loc.Get("Compare_Towers"), Blue.Towers, Red.Towers),
            TeamComparison.Of(Loc.Get("Compare_Inhibitors"), Blue.Inhibitors, Red.Inhibitors),
            TeamComparison.Of(Loc.Get("Compare_Dragons"), Blue.Dragons, Red.Dragons),
            TeamComparison.Of(Loc.Get("Compare_Grubs"), Blue.Grubs, Red.Grubs),
            TeamComparison.Of(Loc.Get("Compare_Barons"), Blue.Barons, Red.Barons),
            TeamComparison.Of(Loc.Get("Compare_Vision"), Blue.VisionScore, Red.VisionScore),
        ];

        Charts =
        [
            Chart(Loc.Get("Chart_DamageToChampions"), p => p.DamageToChampions),
            Chart(Loc.Get("Chart_DamageTaken"), p => p.DamageTaken),
            Chart(Loc.Get("Chart_Gold"), p => p.GoldEarned),
            Chart(Loc.Get("Chart_CreepScore"), p => p.CreepScore),
            Chart(Loc.Get("Chart_VisionScore"), p => p.VisionScore),
            Chart(Loc.Get("Chart_TeamSupport"), p => p.HealOnTeammates + p.ShieldOnTeammates),
            Chart(Loc.Get("Chart_CrowdControl"), p => p.CrowdControlSeconds),
            Chart(Loc.Get("Chart_Buildings"), p => p.DamageToBuildings),
        ];

        allStats = metadata.Players
            .SelectMany(p => p.RawStats.Keys)
            .Distinct(StringComparer.Ordinal)
            .Where(key => key.Length > 0 && !char.IsAsciiDigit(key[0]) && !HiddenStatPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .Select(key => new StatRow(key, [.. rows.Select(r => r.Stats.RawStats.GetValueOrDefault(key, string.Empty))]))
            .ToList();
        FilteredStats = allStats;
    }

    private readonly IReadOnlyList<StatRow> allStats;

    public ReplayItemViewModel Item { get; }

    /// <summary>The Timeline tab; filled once the timeline is loaded.</summary>
    public TimelineViewModel Timeline { get; } = new();

    /// <summary>The timeline shown, file-only or captured; <c>null</c> until loaded.</summary>
    public Core.Timelines.MatchTimeline? LoadedTimeline
    {
        get => loadedTimeline;
        private set => SetProperty(ref loadedTimeline, value);
    }

    /// <summary>Shows a loaded timeline in the Timeline tab and keeps it for the overlay.</summary>
    public void ShowTimeline(Core.Timelines.MatchTimeline timeline, IconProvider icons, GameCatalog? catalog)
    {
        LoadedTimeline = timeline;
        Timeline.Show(timeline, AllPlayers);
        ItemIcon = ItemIconsFor(catalog, icons);
    }

    /// <summary>Item icons by item id for a patch's catalog, each loaded once.</summary>
    public static Func<int, IconSlot> ItemIconsFor(GameCatalog? catalog, IconProvider icons)
    {
        var itemIcons = new Dictionary<int, IconSlot>();
        return id =>
        {
            if (!itemIcons.TryGetValue(id, out var slot))
            {
                var name = catalog?.Items.GetValueOrDefault(id)?.Name ?? id.ToString(CultureInfo.InvariantCulture);
                slot = new IconSlot(name, isEmpty: id == 0);
                slot.Load(icons, catalog is null || id == 0 ? null : DataDragonParser.ItemIconPath(catalog.Version, id));
                itemIcons[id] = slot;
            }

            return slot;
        };
    }

    /// <summary>Item icons by item id for this replay's patch, for the overlay.</summary>
    public Func<int, IconSlot> ItemIcon { get; private set; } = id => new IconSlot(id.ToString(CultureInfo.InvariantCulture), isEmpty: id == 0);

    public void Dispose()
    {
    }

    public ReplayMetadata Metadata { get; }

    public IReadOnlyList<PlayerRowViewModel> AllPlayers { get; }

    public TeamViewModel Blue { get; }

    public TeamViewModel Red { get; }

    public bool HasPlayers => AllPlayers.Count > 0;

    public string Duration => MatchFormat.Duration(Metadata.GameLength);

    public string Version => Metadata.Version.ToString();

    public string WinnerText => Metadata.WinningTeam switch
    {
        Team.Blue => Loc.Get("Winner_Blue"),
        Team.Red => Loc.Get("Winner_Red"),
        _ => Loc.Get("Winner_Unknown"),
    };

    public IReadOnlyList<TeamComparison> Comparisons { get; }

    public IReadOnlyList<MetricChart> Charts { get; }

    public IReadOnlyList<StatRow> FilteredStats { get; private set; }

    /// <summary>Shows only stats whose Riot name contains this text.</summary>
    public string StatFilter
    {
        get => statFilter;
        set
        {
            if (SetProperty(ref statFilter, value))
            {
                FilteredStats = [.. allStats.Where(s => s.Name.Contains(value.Trim(), StringComparison.OrdinalIgnoreCase))];
                OnPropertyChanged(nameof(FilteredStats));
            }
        }
    }

    private MetricChart Chart(string title, Func<PlayerStats, long> value)
    {
        var max = AllPlayers.Count == 0 ? 0 : AllPlayers.Max(p => value(p.Stats));
        var bars = AllPlayers
            .OrderByDescending(p => value(p.Stats))
            .Select(p => new PlayerBar(
                p.Champion,
                p.Stats.GameName,
                value(p.Stats).ToString("N0", CultureInfo.CurrentCulture),
                max <= 0 ? 0 : (double)value(p.Stats) / max,
                p.IsBlue))
            .ToList();
        return new MetricChart(title, bars);
    }
}
