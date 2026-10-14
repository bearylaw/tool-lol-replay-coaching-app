using RiftReview.App.Replays;
using RiftReview.App.Settings;
using RiftReview.Capture;
using RiftReview.Core.Matches;

namespace RiftReview.App.Tests;

public sealed class ReviewShareTests
{
    private static readonly DateTimeOffset Made = new(2026, 10, 14, 18, 0, 0, TimeSpan.Zero);
    private static readonly BookmarkTag Mistake = new("mistake", "Mistake", "#E5484D");

    [Fact]
    public void AReviewKeepsBookmarksDrawingsCameraTagsAndNotes()
    {
        var drawing = new BookmarkDrawing([new SavedStroke([0.1, 0.2, 0.3, 0.4], "#FFE85050", 0.004)], new CameraView(1, 2, 3, 0, 56, 0, 40, "top"));
        Bookmark[] bookmarks = [new(Guid.NewGuid(), 600, "Go bot", Made, drawing, "mistake"), new(Guid.NewGuid(), 60, string.Empty, Made)];

        var text = ReviewShare.Write("EUW1-7", "Game 7", "Ward earlier", bookmarks, [Mistake, new("unused", "Unused", "#FFFFFF")]);
        var review = ReviewShare.Read(text);

        Assert.NotNull(review);
        Assert.Equal("EUW1-7", review.Key);
        Assert.Equal("Ward earlier", review.Note);
        Assert.Equal([Mistake], review.Tags);
        Assert.Equal([60, 600], review.Bookmarks.Select(b => b.Time));
        var shared = review.Bookmarks[1];
        Assert.Equal("mistake", shared.Tag);
        Assert.Equal(drawing.Camera, shared.Drawing?.Camera);
        Assert.Equal(drawing.Strokes[0].Points, shared.Drawing?.Strokes[0].Points);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "format": "something else", "version": 1, "key": "EUW1-7" }""")]
    [InlineData("""{ "format": "rift-review/review", "version": 99, "key": "EUW1-7" }""")]
    [InlineData("""{ "format": "rift-review/review", "version": 1, "key": " " }""")]
    public void RefusesWhatIsNotAReviewItCanRead(string text) => Assert.Null(ReviewShare.Read(text));

    [Fact]
    public void MergingUsesTheReceiverTagsAddsNewOnesAndReplacesChangedBookmarks()
    {
        var same = Guid.NewGuid();
        var changed = Guid.NewGuid();
        var review = new SharedReview(ReviewShare.FormatName, 1, "EUW1-7", "Game 7", "Ward earlier",
            [new("coach-mistake", "mistake", "#FF0000"), new("tempo", "Tempo", "#12A594")],
            [
                new(same, 10, "kept", Made),
                new(changed, 20, "better note", Made, Tag: "coach-mistake"),
                new(Guid.NewGuid(), 30, "new", Made, Tag: "tempo"),
            ]);
        Bookmark[] mine = [new(same, 10, "kept", Made), new(changed, 20, "note", Made)];

        var import = ReviewShare.Merge(review, [Mistake], mine, "My own note");

        Assert.Equal(["mistake", "tempo"], import.Tags.Select(t => t.Id));
        Assert.Equal((1, 1), (import.Added, import.Updated));
        Assert.Equal([("better note", "mistake"), ("new", "tempo")], import.Bookmarks.Select(b => (b.Text, b.Tag)));
        Assert.Equal("My own note" + Environment.NewLine + Environment.NewLine + "Ward earlier", import.Note);
        Assert.Equal("My own note\nWard earlier", ReviewShare.Merge(review, [], [], "My own note\nWard earlier").Note);
    }

    [Fact]
    public void FileNamesAreSafe() => Assert.Equal("Game - 7.riftreview", ReviewShare.FileName("Game / 7"));

    [Fact]
    public void TheDiscordExportGroupsTaggedBookmarksUnderTheirTag()
    {
        var good = new BookmarkTag("good", "Good play", "#46A758");
        Bookmark[] bookmarks =
        [
            new(Guid.NewGuid(), 655, "Clean dive", Made, Tag: "good"),
            new(Guid.NewGuid(), 143, "Should have gone bot", Made, Tag: "mistake"),
            new(Guid.NewGuid(), 300, string.Empty, Made, Tag: "mistake"),
            new(Guid.NewGuid(), 900, "Baron call", Made),
        ];

        var text = NotesExport.Markdown("Game 1", string.Empty, [new("A", "Ahri", Team.Blue)], bookmarks, string.Empty, [Mistake, good]);

        var lines = text.Split('\n').SkipWhile(l => l != "**Bookmarks**").Skip(1).Where(l => l.Length > 0);
        Assert.Equal(
            ["🔴 **Mistake**", "- **2:23** – Should have gone bot", "- **5:00**", "🟢 **Good play**", "- **10:55** – Clean dive", "**Other**", "- **15:00** – Baron call"],
            lines);
    }
}
