using System.Globalization;
using System.Text.RegularExpressions;
using RiftReview.App.Localization;

namespace RiftReview.App.Settings;

/// <summary>
/// A label the coach puts on bookmarks, such as "Mistake", with its colour on the timeline. The first
/// nine in the list have the overlay shortcuts Ctrl+Alt+1 to 9.
/// </summary>
/// <param name="Id">Stable identity kept by the bookmarks, so renaming a tag renames it everywhere.</param>
/// <param name="Name">Shown in the overlay, the Notes tab and the export.</param>
/// <param name="Colour">#RRGGBB.</param>
internal sealed partial record BookmarkTag(string Id, string Name, string Colour)
{
    /// <summary>Tags with a shortcut, Ctrl+Alt+1 to 9.</summary>
    public const int WithShortcut = 9;

    /// <summary>The colour of a bookmark without a tag, and of a tag whose colour is unreadable.</summary>
    public const string DefaultColour = "#F0D9A4";

    /// <summary>The colours the settings offer.</summary>
    public static readonly IReadOnlyList<string> Palette =
    [
        "#E5484D", "#F76B15", "#FFC53D", "#46A758", "#12A594", "#3E8ED0", "#6E56CF", "#D6409F",
        "#F0D9A4", "#A18072", "#8B8D98", "#FFFFFF",
    ];

    /// <summary>The tags a new installation starts with.</summary>
    public static IReadOnlyList<BookmarkTag> Defaults =>
    [
        new("mistake", Loc.Get("Tag_Mistake"), "#E5484D"),
        new("good", Loc.Get("Tag_GoodPlay"), "#46A758"),
        new("vision", Loc.Get("Tag_Vision"), "#6E56CF"),
        new("macro", Loc.Get("Tag_Macro"), "#3E8ED0"),
        new("laning", Loc.Get("Tag_Laning"), "#F76B15"),
    ];

    /// <summary>An id for a tag the coach adds.</summary>
    public static string NewId() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>Whether a text is a colour as tags store it (#RRGGBB).</summary>
    public static bool IsColour(string? text) => text is not null && ColourPattern().IsMatch(text);

    /// <summary>The colour as red, green, blue.</summary>
    public (byte Red, byte Green, byte Blue) Rgb()
    {
        var colour = IsColour(Colour) ? Colour : DefaultColour;
        var value = int.Parse(colour.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return ((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    /// <summary>
    /// The list as stored: names trimmed, nameless ones dropped, one tag per id, colours readable
    /// (upper-case #RRGGBB).
    /// </summary>
    public static IReadOnlyList<BookmarkTag> Normalized(IEnumerable<BookmarkTag?> tags) =>
    [
        .. tags.OfType<BookmarkTag>()
            .Where(t => !string.IsNullOrWhiteSpace(t.Id) && !string.IsNullOrWhiteSpace(t.Name))
            .DistinctBy(t => t.Id, StringComparer.Ordinal)
            .Select(t => t with
            {
                Id = t.Id.Trim(),
                Name = t.Name.Trim(),
                Colour = IsColour(t.Colour) ? t.Colour.ToUpperInvariant() : DefaultColour,
            }),
    ];

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColourPattern();
}
