using RiftReview.App.Replays;
using RiftReview.App.Settings;
using RiftReview.Core.Matches;

namespace RiftReview.App.Tests;

public sealed class NotesExportTests
{
    private static readonly DateTimeOffset Made = new(2026, 10, 14, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ListsBookmarksInTimeOrderThenTheNotes()
    {
        Bookmark[] bookmarks =
        [
            new(Guid.NewGuid(), 655, "Recall together\nfor tempo", Made),
            new(Guid.NewGuid(), 143, "Team should have went bot instead", Made),
            new(Guid.NewGuid(), 1260, "  ", Made),
        ];
        ExportPlayer[] players = [new("Northwind", "Ahri", Team.Blue), new("Ironbark", "Syndra", Team.Red), new("Ashen Vale", "Vi", Team.Blue)];

        var text = NotesExport.Markdown("Game 7000000002", "30:16", players, bookmarks, "Ward the river before grubs.\r\nPing earlier.");

        var lines = text.Split('\n');
        Assert.Equal("## Game 7000000002", lines[0]);
        Assert.Equal("30:16", lines[1]);
        Assert.Contains("Northwind (Ahri), Ashen Vale (Vi)", lines[2], StringComparison.Ordinal);
        Assert.Contains("Ironbark (Syndra)", lines[3], StringComparison.Ordinal);
        var items = lines.Where(l => l.StartsWith("- ", StringComparison.Ordinal)).ToList();
        Assert.Equal(["- **2:23** – Team should have went bot instead", "- **10:55** – Recall together for tempo", "- **21:00** – (no note)"], items);
        Assert.EndsWith("Ward the river before grubs.\nPing earlier.\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
    }

    [Fact]
    public void LeavesOutWhatIsEmpty()
    {
        var text = NotesExport.Markdown("Game 1", string.Empty, [], [], " ");

        Assert.Equal("## Game 1\n", text);
    }
}
