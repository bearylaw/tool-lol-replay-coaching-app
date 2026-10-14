using System.Windows.Media;
using RiftReview.App.Mvvm;
using RiftReview.App.Settings;
using RiftReview.App.Theme;

namespace RiftReview.App.Replays;

/// <summary>The coach's tags as switches for one bookmark; at most one is on.</summary>
internal sealed class TagChoices : ObservableObject
{
    private readonly Action<string?>? changed;
    private string? selected;

    /// <param name="tags">The coach's tags, in shortcut order.</param>
    /// <param name="selected">The bookmark's tag id; <c>null</c> for none.</param>
    /// <param name="changed">Called with the new tag id after the coach switched one.</param>
    public TagChoices(IReadOnlyList<BookmarkTag> tags, string? selected, Action<string?>? changed = null)
    {
        ArgumentNullException.ThrowIfNull(tags);
        this.changed = changed;
        Options = [.. tags.Select((t, i) => new TagChoice(this, t, i < BookmarkTag.WithShortcut ? (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty))];

        // A tag deleted in the settings stays on the bookmark until another is chosen.
        this.selected = selected;
    }

    public IReadOnlyList<TagChoice> Options { get; }

    public bool HasOptions => Options.Count > 0;

    /// <summary>The chosen tag's id; <c>null</c> for none.</summary>
    public string? Selected => selected;

    /// <summary>Switches the tag at a place in the list (the shortcut's number − 1).</summary>
    public void Toggle(int index)
    {
        if (index >= 0 && index < Options.Count)
        {
            Choose(Options[index], Options[index].Tag.Id != selected);
        }
    }

    internal void Choose(TagChoice choice, bool on)
    {
        var next = on ? choice.Tag.Id : selected == choice.Tag.Id ? null : selected;
        if (next == selected)
        {
            return;
        }

        selected = next;
        foreach (var option in Options)
        {
            option.Refresh();
        }

        OnPropertyChanged(nameof(Selected));
        changed?.Invoke(selected);
    }
}

/// <summary>One tag of <see cref="TagChoices"/>.</summary>
internal sealed class TagChoice(TagChoices owner, BookmarkTag tag, string shortcut) : ObservableObject
{
    public BookmarkTag Tag { get; } = tag;

    public string Name => Tag.Name;

    public Brush Colour => TagBrushes.Of(Tag);

    /// <summary>The digit of its shortcut, empty past the ninth tag.</summary>
    public string Shortcut { get; } = shortcut;

    public bool IsSelected
    {
        get => owner.Selected == Tag.Id;
        set => owner.Choose(this, value);
    }

    internal void Refresh() => OnPropertyChanged(nameof(IsSelected));
}
