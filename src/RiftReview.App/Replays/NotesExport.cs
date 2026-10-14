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
/// and the free notes at the end.
/// </summary>
internal static class NotesExport
{
    /// <summary>The export for one replay; <paramref name="details"/> is the line under the title.</summary>
    public static string Markdown(string title, string details, IReadOnlyList<ExportPlayer> players, IReadOnlyList<Bookmark> bookmarks, string note)
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
            foreach (var bookmark in bookmarks.OrderBy(b => b.Time))
            {
                var line = bookmark.Text.Trim().ReplaceLineEndings(" ");
                text.Append("- **").Append(MatchFormat.Duration(TimeSpan.FromSeconds(bookmark.Time))).Append("** – ")
                    .AppendLine(line.Length == 0 ? Loc.Get("Bookmark_NoText") : line);
            }
        }

        if (note.Trim().Length > 0)
        {
            text.AppendLine().Append("**").Append(Loc.Get("Export_Notes")).AppendLine("**").AppendLine(note.Trim().ReplaceLineEndings("\n"));
        }

        return text.ToString().ReplaceLineEndings("\n").TrimEnd() + "\n";
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
