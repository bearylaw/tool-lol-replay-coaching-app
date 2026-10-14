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
                OnPropertyChanged(nameof(HasDrawing));
            }
        }
    }

    public double Time => bookmark.Time;

    public string Text => bookmark.Text;

    public string TimeText => MatchFormat.Duration(TimeSpan.FromSeconds(bookmark.Time));

    /// <summary>A drawing was kept with this bookmark.</summary>
    public bool HasDrawing => bookmark.Drawing is { Strokes.Length: > 0 };
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

    /// <summary>Adds a bookmark at a game time, with the drawing on screen if there is one.</summary>
    public BookmarkItem Add(double time, string text, BookmarkDrawing? drawing = null)
    {
        var item = new BookmarkItem(new Bookmark(Guid.NewGuid(), Math.Max(0, time), text.Trim(), DateTimeOffset.UtcNow, drawing));
        store.Put(key, item.Bookmark);
        var index = Items.TakeWhile(i => i.Time <= item.Time).Count();
        Items.Insert(index, item);
        Refresh();
        return item;
    }

    /// <summary>Changes a bookmark's note; a new drawing replaces the kept one, none keeps it.</summary>
    public void Edit(BookmarkItem item, string text, BookmarkDrawing? drawing = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Bookmark = item.Bookmark with { Text = text.Trim(), Drawing = drawing ?? item.Bookmark.Drawing };
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
