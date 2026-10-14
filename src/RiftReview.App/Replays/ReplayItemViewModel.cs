using System.Globalization;
using RiftReview.App.Localization;
using RiftReview.App.Mvvm;
using RiftReview.Core.Matches;
using RiftReview.Core.Rofl;
using RiftReview.GameData;
using RiftReview.Launch;
using RiftReview.Library;

namespace RiftReview.App.Replays;

/// <summary>A bookmark as a line of text: game time and note.</summary>
/// <param name="Bookmark">The stored bookmark.</param>
/// <param name="Time">Game time, for example 12:34.</param>
/// <param name="Text">The note, or a placeholder when it has none.</param>
internal sealed record BookmarkLine(Settings.Bookmark Bookmark, string Time, string Text)
{
    public static BookmarkLine From(Settings.Bookmark bookmark) => new(
        bookmark,
        MatchFormat.Duration(TimeSpan.FromSeconds(bookmark.Time)),
        bookmark.Text.Length == 0 ? Loc.Get("Bookmark_NoText") : bookmark.Text.ReplaceLineEndings(" "));
}

/// <summary>One card in the replay list.</summary>
internal sealed class ReplayItemViewModel : ObservableObject
{
    private Compatibility compatibility = Compatibility.Unknown;
    private string? rosterSummary;
    private GameCatalog? catalog;
    private IReadOnlyList<BookmarkLine> bookmarkLines = [];
    private string moreBookmarks = string.Empty;

    // The card shows this many bookmarks; the match's Notes tab shows all.
    private const int BookmarksOnCard = 3;

    public ReplayItemViewModel(ReplayEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entry = entry;
        entry.Metadata.TryGetValue(out var metadata, out var error);
        Metadata = metadata;
        Error = error;

        var clientName = entry.ClientName;
        Title = clientName is null
            ? Path.GetFileNameWithoutExtension(entry.FileName)
            : Loc.Format("Replay_GameTitle", clientName.GameId.ToString(CultureInfo.InvariantCulture));
        NoteKey = clientName is null ? entry.FileName : $"{clientName.PlatformId}-{clientName.GameId}";

        var parts = new List<string> { entry.Source == ReplaySource.Own ? Loc.Get("Source_Own") : Loc.Get("Source_Players") };
        if (entry.Group.Length > 0)
        {
            parts.Add(entry.Group);
        }

        if (clientName is not null)
        {
            parts.Add(clientName.PlatformId);
        }

        parts.Add(entry.ModifiedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));
        Subtitle = string.Join(" · ", parts);
    }

    public ReplayEntry Entry { get; }

    public ReplayMetadata? Metadata { get; }

    public RoflError? Error { get; }

    public string Title { get; }

    public string Subtitle { get; }

    /// <summary>The key the coach's note and bookmarks for this replay are stored under.</summary>
    public string NoteKey { get; }

    /// <summary>The first bookmarks of this replay, in time order.</summary>
    public IReadOnlyList<BookmarkLine> BookmarkLines
    {
        get => bookmarkLines;
        private set => SetProperty(ref bookmarkLines, value);
    }

    /// <summary>"+2 more" when the card does not show them all.</summary>
    public string MoreBookmarks
    {
        get => moreBookmarks;
        private set => SetProperty(ref moreBookmarks, value);
    }

    public void SetBookmarks(IReadOnlyList<Settings.Bookmark> bookmarks)
    {
        ArgumentNullException.ThrowIfNull(bookmarks);
        BookmarkLines = [.. bookmarks.Take(BookmarksOnCard).Select(BookmarkLine.From)];
        MoreBookmarks = bookmarks.Count > BookmarksOnCard ? Loc.Format("Bookmark_More", bookmarks.Count - BookmarksOnCard) : string.Empty;
    }

    public bool IsReadable => Metadata is not null;

    public bool HasPlayers => Metadata is { Players.Count: > 0 };

    public string Patch => Metadata?.Version.Patch ?? string.Empty;

    public string Duration => Metadata is null ? string.Empty : MatchFormat.Duration(Metadata.GameLength);

    public Team WinningTeam => Metadata?.WinningTeam ?? Team.Other;

    public string ErrorText => Error is null ? string.Empty : Loc.Get($"RoflError_{Error.Kind}");

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "WPF bindings need an instance property.")]
    public string NoPlayersText => Loc.Get("Replay_NoPlayerStats");

    public IReadOnlyList<IconSlot> BlueChampions { get; private set; } = [];

    public IReadOnlyList<IconSlot> RedChampions { get; private set; } = [];

    public Compatibility Compatibility
    {
        get => compatibility;
        set
        {
            if (SetProperty(ref compatibility, value))
            {
                OnPropertyChanged(nameof(CompatibilityText));
            }
        }
    }

    public string CompatibilityText => Loc.Get($"Compat_{Compatibility}");

    /// <summary>The roster players in this game with champion and score, or <c>null</c> when none played.</summary>
    public string? RosterSummary
    {
        get => rosterSummary;
        private set => SetProperty(ref rosterSummary, value);
    }

    /// <summary>Recomputes the roster line after the roster changed.</summary>
    public void UpdateRoster(Func<string, bool> isOnRoster)
    {
        var players = Metadata?.Players.Where(p => isOnRoster(p.RiotId)).ToList() ?? [];
        RosterSummary = players.Count == 0
            ? null
            : string.Join("   ", players.Select(p => $"★ {p.GameName} · {catalog?.FindChampion(p.Champion)?.Name ?? p.Champion} · {p.Kills}/{p.Deaths}/{p.Assists}"));
    }

    /// <summary>Fills the champion strips once the catalog for the replay's patch is known.</summary>
    public void LoadChampions(GameCatalog? patchCatalog, IconProvider icons, Func<string, bool> isOnRoster)
    {
        if (Metadata is null)
        {
            return;
        }

        catalog = patchCatalog;
        UpdateRoster(isOnRoster);

        BlueChampions = Strip(Metadata.PlayersOn(Team.Blue), catalog, icons);
        RedChampions = Strip(Metadata.Players.Where(p => p.Team != Team.Blue), catalog, icons);
        OnPropertyChanged(nameof(BlueChampions));
        OnPropertyChanged(nameof(RedChampions));
    }

    private static List<IconSlot> Strip(IEnumerable<PlayerStats> players, GameCatalog? catalog, IconProvider icons) =>
        [.. players.Select(p =>
        {
            var champion = catalog?.FindChampion(p.Champion);
            var slot = new IconSlot(champion?.Name ?? p.Champion);
            slot.Load(icons, catalog is null || champion is null ? null : DataDragonParser.ChampionIconPath(catalog.Version, champion));
            return slot;
        })];
}
