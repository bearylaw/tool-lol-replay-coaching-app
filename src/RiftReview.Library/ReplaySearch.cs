namespace RiftReview.Library;

/// <summary>Free-text search over the library.</summary>
public static class ReplaySearch
{
    /// <summary>
    /// True when every word of <paramref name="query"/> appears, ignoring case, in the file name,
    /// the subfolder, the patch, or a player's Riot ID or champion. An empty query matches all.
    /// </summary>
    public static bool Matches(ReplayEntry entry, string? query)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var words = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return true;
        }

        var haystack = new List<string> { entry.FileName, entry.Group };
        if (entry.Metadata.TryGetValue(out var metadata, out _))
        {
            haystack.Add(metadata.Version.Patch);
            foreach (var player in metadata.Players)
            {
                haystack.Add(player.RiotId);
                haystack.Add(player.Champion);
            }
        }

        return words.All(word => haystack.Exists(text => text.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }
}
