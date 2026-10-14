using System.Text.Json;

namespace RiftReview.App.Settings;

/// <summary>
/// One replay's review as a file to send: the bookmarks with their drawings and camera, the tags they
/// use, and the free notes. It names the replay by the same key as the stores (platform and game
/// id), so it lands on the receiver's copy of the replay whatever the file is called.
/// </summary>
/// <param name="Format">Always <see cref="ReviewShare.FormatName"/>.</param>
/// <param name="Version">The file's schema version.</param>
/// <param name="Key">The replay's key in the bookmark and note stores.</param>
/// <param name="Title">The replay's title when it was shared, for the receiver's messages.</param>
/// <param name="Note">The free notes.</param>
/// <param name="Tags">The tags the bookmarks use.</param>
/// <param name="Bookmarks">The bookmarks.</param>
internal sealed record SharedReview(string Format, int Version, string Key, string Title, string Note, BookmarkTag[] Tags, Bookmark[] Bookmarks);

/// <summary>What importing a shared review changes.</summary>
/// <param name="Tags">The receiver's tags, with the review's new ones added at the end.</param>
/// <param name="Bookmarks">The bookmarks to store: the new ones and the changed ones.</param>
/// <param name="Added">How many bookmarks are new.</param>
/// <param name="Updated">How many replace an older version of themselves.</param>
/// <param name="Note">The receiver's free notes with the review's added.</param>
internal sealed record ReviewImport(IReadOnlyList<BookmarkTag> Tags, IReadOnlyList<Bookmark> Bookmarks, int Added, int Updated, string Note);

/// <summary>Writes and reads <c>.riftreview</c> files and merges them into what the receiver has.</summary>
internal static class ReviewShare
{
    /// <summary>The file extension.</summary>
    public const string Extension = ".riftreview";

    /// <summary>The value of the file's <c>format</c> field.</summary>
    public const string FormatName = "rift-review/review";

    private const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The file's text for one replay.</summary>
    public static string Write(string key, string title, string note, IReadOnlyList<Bookmark> bookmarks, IReadOnlyList<BookmarkTag> tags)
    {
        ArgumentNullException.ThrowIfNull(bookmarks);
        ArgumentNullException.ThrowIfNull(tags);
        var used = tags.Where(t => bookmarks.Any(b => b.Tag == t.Id)).ToArray();
        return JsonSerializer.Serialize(new SharedReview(FormatName, CurrentVersion, key, title, note, used, [.. bookmarks.OrderBy(b => b.Time)]), Json);
    }

    /// <summary>The review in a file's text, or <c>null</c> when it is not a review this app can read.</summary>
    public static SharedReview? Read(string text)
    {
        SharedReview? review;
        try
        {
            review = JsonSerializer.Deserialize<SharedReview>(text, Json);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return null;
        }

        if (review is null || review.Format != FormatName || review.Version is < 1 or > CurrentVersion || string.IsNullOrWhiteSpace(review.Key))
        {
            return null;
        }

        return review with
        {
            Title = review.Title ?? string.Empty,
            Note = review.Note ?? string.Empty,
            Tags = [.. BookmarkTag.Normalized(review.Tags ?? [])],
            Bookmarks = [.. (review.Bookmarks ?? []).Where(b => b is not null && double.IsFinite(b.Time)).Select(b => b with { Text = b.Text ?? string.Empty })],
        };
    }

    /// <summary>
    /// Merges a review into the receiver's tags, bookmarks and note. A tag the receiver has (same id,
    /// else same name) is used as theirs; a new one is added. A bookmark the receiver has is replaced
    /// by the review's version, so a review shared again brings its changes.
    /// </summary>
    public static ReviewImport Merge(SharedReview review, IReadOnlyList<BookmarkTag> tags, IReadOnlyList<Bookmark> existing, string note)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(existing);
        var merged = tags.ToList();
        var renamed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in review.Tags)
        {
            if (merged.FirstOrDefault(t => t.Id == tag.Id) is not null)
            {
                continue;
            }

            if (merged.FirstOrDefault(t => string.Equals(t.Name, tag.Name, StringComparison.OrdinalIgnoreCase)) is { } same)
            {
                renamed[tag.Id] = same.Id;
            }
            else
            {
                merged.Add(tag);
            }
        }

        var had = existing.ToDictionary(b => b.Id);
        var bookmarks = review.Bookmarks
            .Select(b => b.Tag is { } id && renamed.TryGetValue(id, out var local) ? b with { Tag = local } : b)
            .Where(b => !had.TryGetValue(b.Id, out var old) || !Same(old, b))
            .ToList();
        var updated = bookmarks.Count(b => had.ContainsKey(b.Id));
        return new ReviewImport(merged, bookmarks, bookmarks.Count - updated, updated, MergeNote(note, review.Note));
    }

    /// <summary>A file name for a review of a replay with this title.</summary>
    public static string FileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string([.. (title ?? string.Empty).Select(c => invalid.Contains(c) ? '-' : c)]).Trim();
        return (name.Length == 0 ? "review" : name) + Extension;
    }

    // Records compare their arrays by reference; the stored JSON says whether two versions differ.
    private static bool Same(Bookmark a, Bookmark b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json);

    private static string MergeNote(string mine, string theirs)
    {
        var shared = theirs.Trim();
        if (shared.Length == 0 || mine.Contains(shared, StringComparison.Ordinal))
        {
            return mine;
        }

        return mine.Trim().Length == 0 ? shared : mine.TrimEnd() + Environment.NewLine + Environment.NewLine + shared;
    }
}
