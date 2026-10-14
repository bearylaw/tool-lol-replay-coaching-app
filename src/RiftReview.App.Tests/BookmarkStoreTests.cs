using RiftReview.App.Overlay;
using RiftReview.App.Settings;
using RiftReview.Core.Logging;

namespace RiftReview.App.Tests;

public sealed class BookmarkStoreTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "riftreview-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private string FilePath => Path.Combine(folder, "bookmarks.json");

    [Fact]
    public void KeepsBookmarksAcrossSessionsInTimeOrder()
    {
        var list = new BookmarkList(Store(), "sample-4");
        list.Add(754.2, "  Jungler invades blue side without vision  ");
        list.Add(301, "first dragon fight");

        var saved = Store().Get("sample-4");

        Assert.Equal([301, 754.2], saved.Select(b => b.Time));
        Assert.Equal("Jungler invades blue side without vision", saved[1].Text);
    }

    [Fact]
    public void TheListKeepsItsOrderAndTimelineMarks()
    {
        var list = new BookmarkList(Store(), "EUW1-1");
        list.Add(600, "b");
        list.Add(120, "a");
        var late = list.Add(900, "c");

        Assert.Equal(["a", "b", "c"], list.Items.Select(i => i.Text));
        Assert.Equal([120, 600, 900], list.Times);

        list.Edit(late, "changed");
        list.Remove(list.Items[0]);

        Assert.Equal(["b", "changed"], Store().Get("EUW1-1").Select(b => b.Text));
        Assert.Equal([600, 900], list.Times);
    }

    [Fact]
    public void TellsListenersWhichReplayChanged()
    {
        var store = Store();
        var changed = new List<string>();
        store.Changed += (_, key) => changed.Add(key);

        new BookmarkList(store, "EUW1-2").Add(10, "x");

        Assert.Equal(["EUW1-2"], changed);
    }

    [Fact]
    public void NeverOverwritesAnUnreadableFile()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(FilePath, "{ broken");

        new BookmarkList(Store(), "EUW1-3").Add(5, "new");

        Assert.Equal("{ broken", File.ReadAllText(FilePath + ".broken"));
        Assert.Equal("new", Assert.Single(Store().Get("EUW1-3")).Text);
    }

    private BookmarkStore Store() => new(FilePath, new AppLog(new StringWriter(), TimeProvider.System));
}
