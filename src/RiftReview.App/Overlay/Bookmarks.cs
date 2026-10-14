using System.Collections.ObjectModel;
using RiftReview.App.Mvvm;
using RiftReview.App.Replays;
using RiftReview.App.Settings;

namespace RiftReview.App.Overlay;

/// <summary>One bookmark as the overlay shows it.</summary>
internal sealed class BookmarkItem(Bookmark bookmark) : ObservableObject
{
    private Bookmark bookmark = bookmark;

    public Bookmark Bookmark
    {
        get => bookmark;
        set
        {
            if (SetProperty(ref bookmark, value))
            {
                OnPropertyChanged(nameof(Text));
                OnPropertyChanged(nameof(TimeText));
            }
        }
    }

    public double Time => bookmark.Time;

    public string Text => bookmark.Text;

    public string TimeText => MatchFormat.Duration(TimeSpan.FromSeconds(bookmark.Time));
}

/// <summary>The bookmarks of the replay the overlay plays, kept in the store as they change.</summary>
internal sealed class BookmarkList : ObservableObject
{
    private readonly BookmarkStore store;
    private readonly string key;
    private IReadOnlyList<double> times = [];

    public BookmarkList(BookmarkStore store, string key)
    {
        this.store = store;
        this.key = key;
        foreach (var bookmark in store.Get(key))
        {
            Items.Add(new BookmarkItem(bookmark));
        }

        times = [.. Items.Select(i => i.Time)];
    }

    /// <summary>The bookmarks in time order.</summary>
    public ObservableCollection<BookmarkItem> Items { get; } = [];

    /// <summary>Their times, for the marks on the timeline.</summary>
    public IReadOnlyList<double> Times
    {
        get => times;
        private set => SetProperty(ref times, value);
    }

    public bool IsEmpty => Items.Count == 0;

    /// <summary>Adds a bookmark at a game time.</summary>
    public BookmarkItem Add(double time, string text)
    {
        var item = new BookmarkItem(new Bookmark(Guid.NewGuid(), Math.Max(0, time), text.Trim(), DateTimeOffset.UtcNow));
        store.Put(key, item.Bookmark);
        var index = Items.TakeWhile(i => i.Time <= item.Time).Count();
        Items.Insert(index, item);
        Refresh();
        return item;
    }

    /// <summary>Changes a bookmark's note.</summary>
    public void Edit(BookmarkItem item, string text)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Bookmark = item.Bookmark with { Text = text.Trim() };
        store.Put(key, item.Bookmark);
    }

    public void Remove(BookmarkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        store.Remove(key, item.Bookmark.Id);
        Items.Remove(item);
        Refresh();
    }

    private void Refresh()
    {
        Times = [.. Items.Select(i => i.Time)];
        OnPropertyChanged(nameof(IsEmpty));
    }
}
