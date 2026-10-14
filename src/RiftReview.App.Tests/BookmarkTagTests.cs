using RiftReview.App.Overlay;
using RiftReview.App.Replays;
using RiftReview.App.Settings;
using RiftReview.App.Theme;
using RiftReview.Core.Logging;

namespace RiftReview.App.Tests;

public sealed class BookmarkTagTests : IDisposable
{
    private static readonly BookmarkTag Mistake = new("mistake", "Mistake", "#E5484D");
    private static readonly BookmarkTag Vision = new("vision", "Vision", "#6E56CF");

    private readonly string folder = Path.Combine(Path.GetTempPath(), "riftreview-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void NormalizedDropsNamelessAndRepeatedTagsAndFixesColours()
    {
        BookmarkTag?[] tags = [new("a", "  Lane  ", "#12a594"), new("b", " ", "#FFFFFF"), new("a", "Again", "#FFFFFF"), new("c", "Odd", "red"), null];

        var normalized = BookmarkTag.Normalized(tags);

        Assert.Equal([new BookmarkTag("a", "Lane", "#12A594"), new BookmarkTag("c", "Odd", BookmarkTag.DefaultColour)], normalized);
    }

    [Fact]
    public void ATaggedBookmarkIsMarkedInTheTagColourAndKeepsItsTag()
    {
        var list = new BookmarkList(Store(), "EUW1-5", [Mistake, Vision]);

        var item = list.Add(120, string.Empty, tag: "vision");
        list.Add(60, "untagged");

        Assert.Equal("Vision", item.TagName);
        Assert.Equal("vision", Store().Get("EUW1-5").Single(b => b.Time == 120).Tag);
        Assert.Equal([TagBrushes.Of((string?)null), TagBrushes.Of(Vision)], list.Marks.Select(m => m.Colour));

        list.Edit(item, "now a mistake", tag: "mistake");

        Assert.Equal(TagBrushes.Of(Mistake), list.Marks[1].Colour);
        Assert.Equal("mistake", Store().Get("EUW1-5").Single(b => b.Time == 120).Tag);
    }

    [Fact]
    public void ABookmarkWhoseTagWasDeletedShowsUntagged()
    {
        var item = new BookmarkItem(new Bookmark(Guid.NewGuid(), 5, string.Empty, DateTimeOffset.UnixEpoch, Tag: "gone"), [Mistake]);

        Assert.Null(item.Tag);
        Assert.Equal(string.Empty, item.TagName);
    }

    [Fact]
    public void TagChoicesKeepAtMostOneOnAndReportEachSwitch()
    {
        var reported = new List<string?>();
        var choices = new TagChoices([Mistake, Vision], "mistake", reported.Add);

        choices.Options[1].IsSelected = true;
        choices.Toggle(1);
        choices.Toggle(0);

        Assert.Equal(["vision", null, "mistake"], reported);
        Assert.Equal(["1", "2"], choices.Options.Select(o => o.Shortcut));
        Assert.True(choices.Options[0].IsSelected);
        Assert.False(choices.Options[1].IsSelected);
    }

    [Fact]
    public void TheNotesTabSavesATagSwitch()
    {
        var saved = new List<Bookmark>();
        var edit = new BookmarkEdit(new Bookmark(Guid.NewGuid(), 143, "Go bot", DateTimeOffset.UnixEpoch), saved.Add, [Mistake, Vision]);

        edit.Tags.Toggle(0);

        Assert.Equal("mistake", Assert.Single(saved).Tag);
        Assert.Equal("mistake", edit.Bookmark.Tag);
    }

    [Fact]
    public void TheSettingsListRenamesRecoloursReordersAddsAndRemoves()
    {
        var settings = new AppSettings { OwnReplaysFolder = "a", PlayerReplaysFolder = "b", Tags = [Mistake, Vision] };
        var editor = new SettingsEditor(settings);
        var tags = editor.Tags;

        tags.Rows[0].Name = "Error";
        tags.Rows[1].PickCommand.Execute(tags.Rows[1].Palette.First(p => p.Colour == "#46A758"));
        tags.MoveUpCommand.Execute(tags.Rows[1]);
        tags.Add();
        tags.Rows[2].Name = "Wave";
        tags.Add();
        tags.RemoveCommand.Execute(tags.Rows[3]);

        var saved = editor.ApplyTo(settings).Tags;

        Assert.Equal(["vision", "mistake"], saved.Take(2).Select(t => t.Id));
        Assert.Equal(["Vision", "Error", "Wave"], saved.Select(t => t.Name));
        Assert.Equal("#46A758", saved[0].Colour);
        Assert.Equal(["Ctrl+Alt+1", "Ctrl+Alt+2", "Ctrl+Alt+3"], tags.Rows.Select(r => r.Shortcut));
        Assert.False(tags.Rows[0].CanMoveUp);
        Assert.False(tags.Rows[2].CanMoveDown);
    }

    [Fact]
    public void ANamelessNewTagIsNotSavedAndResetKeepsTheTags()
    {
        var settings = new AppSettings { OwnReplaysFolder = "a", PlayerReplaysFolder = "b", Tags = [Mistake] };
        var editor = new SettingsEditor(settings);
        editor.Tags.Add();
        editor.Tags.Rows[1].Name = "  ";

        editor.ResetToDefaults();

        Assert.Equal([Mistake], editor.ApplyTo(settings).Tags);
    }

    [Fact]
    public void TagsSurviveASaveAndOlderSettingsGetTheDefaults()
    {
        var file = Path.Combine(folder, "settings.json");
        var store = new SettingsStore(file, new AppLog(new StringWriter(), TimeProvider.System));
        var defaults = new AppSettings { OwnReplaysFolder = "a", PlayerReplaysFolder = "b" };

        store.Save(defaults with { Tags = [Vision, Mistake] });
        Assert.Equal([Vision, Mistake], store.Load(defaults).Tags);

        File.WriteAllText(file, """{ "version": 2, "ownReplaysFolder": "x", "playerReplaysFolder": "y" }""");
        Assert.Equal(["mistake", "good", "vision", "macro", "laning"], store.Load(defaults).Tags.Select(t => t.Id));
    }

    private BookmarkStore Store() => new(Path.Combine(folder, "bookmarks.json"), new AppLog(new StringWriter(), TimeProvider.System));
}
