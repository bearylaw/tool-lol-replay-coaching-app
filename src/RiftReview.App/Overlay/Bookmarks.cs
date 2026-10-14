using System.Collections.ObjectModel;
using System.Windows.Media;
using RiftReview.App.Mvvm;
using RiftReview.App.Replays;
using RiftReview.App.Settings;
using RiftReview.App.Theme;
using RiftReview.App.Viewer;

namespace RiftReview.App.Overlay;

/// <summary>One bookmark as the overlay shows it.</summary>
internal sealed class BookmarkItem(Bookmark bookmark, IReadOnlyList<BookmarkTag> tags) : ObservableObject
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
                OnPropertyChanged(nameof(Tag));
                OnPropertyChanged(nameof(TagName));
                OnPropertyChanged(nameof(Colour));
            }
        }
    }

    public double Time => bookmark.Time;

    public string Text => bookmark.Text;

    public string TimeText => MatchFormat.Duration(TimeSpan.FromSeconds(bookmark.Time));

    /// <summary>A drawing was kept with this bookmark.</summary>
    public bool HasDrawing => bookmark.Drawing is { Strokes.Length: > 0 };

    /// <summary>The bookmark's tag; <c>null</c> without one or when the tag was deleted.</summary>
    public BookmarkTag? Tag => tags.FirstOrDefault(t => t.Id == bookmark.Tag);

    /// <summary>The tag's name, empty without a tag.</summary>
    public string TagName => Tag?.Name ?? string.Empty;

    /// <summary>The tag's colour, gold without a tag.</summary>
    public Brush Colour => TagBrushes.Of(Tag);
}

/// <summary>The bookmarks of the replay the overlay plays, kept in the store as they change.</summary>
internal sealed class BookmarkList : ObservableObject
{
    private readonly BookmarkStore store;
    private readonly string key;
    private IReadOnlyList<double> times = [];
    private IReadOnlyList<BookmarkMark> marks = [];

    public BookmarkList(BookmarkStore store, string key, IReadOnlyList<BookmarkTag>? tags = null)
    {
        this.store = store;
        this.key = key;
        Tags = tags ?? [];
        foreach (var bookmark in store.Get(key))
        {
            Items.Add(new BookmarkItem(bookmark, Tags));
        }

        Refresh();
    }

    /// <summary>The coach's tags, in shortcut order.</summary>
    public IReadOnlyList<BookmarkTag> Tags { get; }

    /// <summary>The bookmarks in time order.</summary>
    public ObservableCollection<BookmarkItem> Items { get; } = [];

    /// <summary>Their times.</summary>
    public IReadOnlyList<double> Times
    {
        get => times;
        private set => SetProperty(ref times, value);
    }

    /// <summary>The marks on the timeline, in the tags' colours.</summary>
    public IReadOnlyList<BookmarkMark> Marks
    {
        get => marks;
        private set => SetProperty(ref marks, value);
    }

    public bool IsEmpty => Items.Count == 0;

    /// <summary>Adds a bookmark at a game time, with the drawing on screen if there is one, and a tag id.</summary>
    public BookmarkItem Add(double time, string text, BookmarkDrawing? drawing = null, string? tag = null)
    {
        var item = new BookmarkItem(new Bookmark(Guid.NewGuid(), Math.Max(0, time), text.Trim(), DateTimeOffset.UtcNow, drawing, tag), Tags);
        store.Put(key, item.Bookmark);
        var index = Items.TakeWhile(i => i.Time <= item.Time).Count();
        Items.Insert(index, item);
        Refresh();
        return item;
    }

    /// <summary>Changes a bookmark's note and tag; a new drawing replaces the kept one, none keeps it.</summary>
    public void Edit(BookmarkItem item, string text, BookmarkDrawing? drawing = null, string? tag = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Bookmark = item.Bookmark with { Text = text.Trim(), Drawing = drawing ?? item.Bookmark.Drawing, Tag = tag };
        store.Put(key, item.Bookmark);
        Refresh();
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
        Marks = [.. Items.Select(i => new BookmarkMark(i.Time, i.Colour))];
        OnPropertyChanged(nameof(IsEmpty));
    }
}
