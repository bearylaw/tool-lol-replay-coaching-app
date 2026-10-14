using RiftReview.App.Mvvm;
using RiftReview.App.Settings;

namespace RiftReview.App.Replays;

/// <summary>A bookmark in the match's Notes tab, whose note and tag can be changed there.</summary>
internal sealed class BookmarkEdit : ObservableObject
{
    private readonly Action<Bookmark> save;

    public BookmarkEdit(Bookmark bookmark, Action<Bookmark> save, IReadOnlyList<BookmarkTag>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        Bookmark = bookmark;
        this.save = save;
        Time = MatchFormat.Duration(TimeSpan.FromSeconds(bookmark.Time));
        Tags = new TagChoices(tags ?? [], bookmark.Tag, tag =>
        {
            Bookmark = Bookmark with { Tag = tag };
            save(Bookmark);
        });
    }

    public Bookmark Bookmark { get; private set; }

    /// <summary>Game time, for example 12:34.</summary>
    public string Time { get; }

    /// <summary>The coach's tags; switching one saves the bookmark.</summary>
    public TagChoices Tags { get; }

    /// <summary>The note; setting it saves the bookmark.</summary>
    public string Text
    {
        get => Bookmark.Text;
        set
        {
            var text = value.Trim();
            if (text != Bookmark.Text)
            {
                Bookmark = Bookmark with { Text = text };
                OnPropertyChanged();
                save(Bookmark);
            }
        }
    }
}
