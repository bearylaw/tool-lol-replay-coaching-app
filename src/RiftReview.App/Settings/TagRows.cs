using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using RiftReview.App.Localization;
using RiftReview.App.Mvvm;
using RiftReview.App.Theme;

namespace RiftReview.App.Settings;

/// <summary>
/// The coach's bookmark tags as an editable list in the settings window: rename, recolour, add,
/// remove and reorder (the place is the shortcut, Ctrl+Alt+1 is the first).
/// </summary>
internal sealed class TagList
{
    public TagList(IReadOnlyList<BookmarkTag> tags)
    {
        Reset(tags);
        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(p => Change(p, row => Rows.Remove(row)));
        MoveUpCommand = new RelayCommand(p => Change(p, row => Move(row, -1)));
        MoveDownCommand = new RelayCommand(p => Change(p, row => Move(row, +1)));
    }

    public ObservableCollection<TagRow> Rows { get; } = [];

    public ICommand AddCommand { get; }

    public ICommand RemoveCommand { get; }

    public ICommand MoveUpCommand { get; }

    public ICommand MoveDownCommand { get; }

    /// <summary>The tags as the rows say, nameless ones left out.</summary>
    public IReadOnlyList<BookmarkTag> Tags => BookmarkTag.Normalized(Rows.Select(r => new BookmarkTag(r.Id, r.Name, r.Colour)));

    /// <summary>Shows these tags instead.</summary>
    public void Reset(IReadOnlyList<BookmarkTag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        Rows.Clear();
        foreach (var tag in tags)
        {
            Rows.Add(new TagRow(tag));
        }

        Number();
    }

    /// <summary>Adds a tag named "New tag" in the first colour no tag has yet.</summary>
    public void Add()
    {
        var colour = BookmarkTag.Palette.FirstOrDefault(c => Rows.All(r => !string.Equals(r.Colour, c, StringComparison.OrdinalIgnoreCase))) ?? BookmarkTag.Palette[0];
        Rows.Add(new TagRow(new BookmarkTag(BookmarkTag.NewId(), Loc.Get("Tag_New"), colour)));
        Number();
    }

    private void Move(TagRow row, int by)
    {
        var from = Rows.IndexOf(row);
        var to = Math.Clamp(from + by, 0, Rows.Count - 1);
        if (from != to)
        {
            Rows.Move(from, to);
        }
    }

    private void Change(object? parameter, Action<TagRow> change)
    {
        if (parameter is TagRow row && Rows.Contains(row))
        {
            change(row);
            Number();
        }
    }

    private void Number()
    {
        for (var i = 0; i < Rows.Count; i++)
        {
            Rows[i].Shortcut = i < BookmarkTag.WithShortcut ? Loc.Format("Tag_Shortcut", (i + 1).ToString(CultureInfo.InvariantCulture)) : string.Empty;
            Rows[i].CanMoveUp = i > 0;
            Rows[i].CanMoveDown = i < Rows.Count - 1;
        }
    }
}

/// <summary>One tag in the settings window.</summary>
internal sealed class TagRow : ObservableObject
{
    private string name;
    private string colour;
    private string shortcut = string.Empty;
    private bool isPickerOpen;
    private bool canMoveUp;
    private bool canMoveDown;

    public TagRow(BookmarkTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        Id = tag.Id;
        name = tag.Name;
        colour = tag.Colour;

        // A colour from elsewhere (a shared review) stays choosable next to the palette's.
        Palette = [.. BookmarkTag.Palette.Append(tag.Colour).Distinct(StringComparer.OrdinalIgnoreCase).Select(c => new ColourOption(c, TagBrushes.Of(c)))];
        PickCommand = new RelayCommand(p =>
        {
            if (p is ColourOption option)
            {
                Colour = option.Colour;
            }

            IsPickerOpen = false;
        });
    }

    public string Id { get; }

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    /// <summary>#RRGGBB.</summary>
    public string Colour
    {
        get => colour;
        set
        {
            if (SetProperty(ref colour, value))
            {
                OnPropertyChanged(nameof(Brush));
            }
        }
    }

    public Brush Brush => TagBrushes.Of(colour);

    /// <summary>The colours to pick from.</summary>
    public IReadOnlyList<ColourOption> Palette { get; }

    public ICommand PickCommand { get; }

    /// <summary>Whether the colour palette is open.</summary>
    public bool IsPickerOpen
    {
        get => isPickerOpen;
        set => SetProperty(ref isPickerOpen, value);
    }

    /// <summary>Its overlay shortcut, for example Ctrl+Alt+1; empty past the ninth tag.</summary>
    public string Shortcut
    {
        get => shortcut;
        set => SetProperty(ref shortcut, value);
    }

    public bool CanMoveUp
    {
        get => canMoveUp;
        set => SetProperty(ref canMoveUp, value);
    }

    public bool CanMoveDown
    {
        get => canMoveDown;
        set => SetProperty(ref canMoveDown, value);
    }
}

/// <summary>A colour in a tag's palette.</summary>
internal sealed record ColourOption(string Colour, Brush Brush);
