using System.Collections.Concurrent;
using System.Windows.Media;
using RiftReview.App.Settings;

namespace RiftReview.App.Theme;

/// <summary>Frozen brushes for tag colours, one per colour.</summary>
internal static class TagBrushes
{
    private static readonly ConcurrentDictionary<string, SolidColorBrush> Made = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The brush of a #RRGGBB colour; an unreadable one gets the untagged bookmark's gold.</summary>
    public static SolidColorBrush Of(string? colour) => Made.GetOrAdd(BookmarkTag.IsColour(colour) ? colour! : BookmarkTag.DefaultColour, key =>
    {
        var (red, green, blue) = new BookmarkTag(string.Empty, string.Empty, key).Rgb();
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    });

    /// <summary>The brush of a bookmark: its tag's colour, gold without one.</summary>
    public static SolidColorBrush Of(BookmarkTag? tag) => Of(tag?.Colour);
}
