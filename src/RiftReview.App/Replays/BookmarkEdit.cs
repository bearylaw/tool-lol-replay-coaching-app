using RiftReview.App.Mvvm;
using RiftReview.App.Settings;

namespace RiftReview.App.Replays;

/// <summary>A bookmark in the match's Notes tab, whose note can be changed there.</summary>
internal sealed class BookmarkEdit(Bookmark bookmark, Action<Bookmark> save) : ObservableObject
{
    public Bookmark Bookmark { get; private set; } = bookmark;

    /// <summary>Game time, for example 12:34.</summary>
    public string Time { get; } = MatchFormat.Duration(TimeSpan.FromSeconds(bookmark.Time));

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
