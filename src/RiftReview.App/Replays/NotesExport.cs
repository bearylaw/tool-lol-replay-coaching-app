using System.Text;
using RiftReview.App.Localization;
using RiftReview.App.Settings;
using RiftReview.Core.Matches;

namespace RiftReview.App.Replays;

/// <summary>A player as the export names them: Riot ID game name and champion.</summary>
internal sealed record ExportPlayer(string Name, string Champion, Team Team);

/// <summary>
/// A replay's bookmarks and free notes as Markdown that Discord renders: a header line with the game,
/// both teams, then one line per bookmark in time order (<c>- **2:23** – Team should have gone bot</c>)
/// and the free notes at the end. Tagged bookmarks are grouped under their tag, in the order of the
/// coach's tags, with a coloured circle for the tag's colour; untagged ones follow under "Other".
/// </summary>
internal static class NotesExport
{
    /// <summary>The export for one replay; <paramref name="details"/> is the line under the title.</summary>
    public static string Markdown(string title, string details, IReadOnlyList<ExportPlayer> players, IReadOnlyList<Bookmark> bookmarks, string note, IReadOnlyList<BookmarkTag>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(bookmarks);
        var text = new StringBuilder();
        text.Append("## ").AppendLine(title);
        if (details.Length > 0)
        {
            text.AppendLine(details);
        }

        AppendTeam(text, Loc.Get("Scoreboard_Blue"), players.Where(p => p.Team == Team.Blue));
        AppendTeam(text, Loc.Get("Scoreboard_Red"), players.Where(p => p.Team == Team.Red));

        if (bookmarks.Count > 0)
        {
            text.AppendLine().Append("**").Append(Loc.Get("Export_Bookmarks")).AppendLine("**");
            AppendBookmarks(text, bookmarks, tags ?? []);
        }

        if (note.Trim().Length > 0)
        {
            text.AppendLine().Append("**").Append(Loc.Get("Export_Notes")).AppendLine("**").AppendLine(note.Trim().ReplaceLineEndings("\n"));
        }

        return text.ToString().ReplaceLineEndings("\n").TrimEnd() + "\n";
    }

    /// <summary>The coloured circle emoji closest to a tag's colour.</summary>
    public static string Circle(BookmarkTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        var (red, green, blue) = tag.Rgb();
        return Circles.MinBy(c => Square(c.Red - red) + Square(c.Green - green) + Square(c.Blue - blue)).Emoji;
    }

    // Discord's circle emoji and their colours.
    private static readonly (string Emoji, int Red, int Green, int Blue)[] Circles =
    [
        ("🔴", 221, 46, 68), ("🟠", 244, 144, 12), ("🟡", 253, 203, 88), ("🟢", 120, 177, 89), ("🔵", 85, 172, 238),
        ("🟣", 170, 142, 214), ("🟤", 193, 105, 79), ("⚫", 49, 55, 61), ("⚪", 230, 231, 232),
    ];

    private static int Square(int value) => value * value;

    private static void AppendBookmarks(StringBuilder text, IReadOnlyList<Bookmark> bookmarks, IReadOnlyList<BookmarkTag> tags)
    {
        var tagged = tags.Select(t => (Tag: t, Bookmarks: bookmarks.Where(b => b.Tag == t.Id).ToList())).Where(g => g.Bookmarks.Count > 0).ToList();
        if (tagged.Count == 0)
        {
            AppendLines(text, bookmarks, emptyText: Loc.Get("Bookmark_NoText"));
            return;
        }

        foreach (var (tag, list) in tagged)
        {
            text.Append(Circle(tag)).Append(" **").Append(tag.Name).AppendLine("**");
            AppendLines(text, list, emptyText: null);
        }

        var known = tagged.Select(g => g.Tag.Id).ToHashSet(StringComparer.Ordinal);
        var other = bookmarks.Where(b => b.Tag is null || !known.Contains(b.Tag)).ToList();
        if (other.Count > 0)
        {
            text.Append("**").Append(Loc.Get("Export_Other")).AppendLine("**");
            AppendLines(text, other, emptyText: Loc.Get("Bookmark_NoText"));
        }
    }

    // One line per bookmark in time order; a bookmark without a note shows emptyText, or only its time.
    private static void AppendLines(StringBuilder text, IEnumerable<Bookmark> bookmarks, string? emptyText)
    {
        foreach (var bookmark in bookmarks.OrderBy(b => b.Time))
        {
            var line = bookmark.Text.Trim().ReplaceLineEndings(" ");
            text.Append("- **").Append(MatchFormat.Duration(TimeSpan.FromSeconds(bookmark.Time))).Append("**");
            if (line.Length > 0 || emptyText is not null)
            {
                text.Append(" – ").Append(line.Length == 0 ? emptyText : line);
            }

            text.AppendLine();
        }
    }

    private static void AppendTeam(StringBuilder text, string name, IEnumerable<ExportPlayer> players)
    {
        var list = string.Join(", ", players.Select(p => $"{p.Name} ({p.Champion})"));
        if (list.Length > 0)
        {
            text.Append("**").Append(name).Append(":** ").AppendLine(list);
        }
    }
}
