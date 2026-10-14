using System.Windows.Media;
using RiftReview.App.Mvvm;

namespace RiftReview.App.Replays;

/// <summary>One icon on screen (champion, item, spell, rune) with the text shown until or instead of it.</summary>
internal sealed class IconSlot : ObservableObject
{
    private ImageSource? image;

    public IconSlot(string label, bool isEmpty = false)
    {
        Label = isEmpty ? string.Empty : label;
        IsEmpty = isEmpty;
    }

    /// <summary>The name, shown as tooltip and as fallback when the icon is unavailable.</summary>
    public string Label { get; }

    /// <summary>An empty item slot.</summary>
    public bool IsEmpty { get; }

    /// <summary>Two letters for the fallback tile.</summary>
    public string Initials => IsEmpty ? string.Empty : Label.Length <= 2 ? Label : Label[..2];

    public ImageSource? Image
    {
        get => image;
        private set => SetProperty(ref image, value);
    }

    /// <summary>Starts loading the icon; the slot updates itself when it arrives.</summary>
    public async void Load(IconProvider icons, string? cdnPath)
    {
        // async void is the boundary here: icon failures are already handled inside IconProvider.
        if (cdnPath is null || IsEmpty)
        {
            return;
        }

        Image = await icons.GetAsync(cdnPath);
    }
}
