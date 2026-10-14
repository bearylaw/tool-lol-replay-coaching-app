namespace RiftReview.Launch;

/// <summary>
/// Reads single values from the indented <c>key: value</c> settings files Riot writes, such as
/// <c>league_of_legends.live.product_settings.yaml</c>. Only plain and double-quoted scalars are
/// supported; that is all these files use for the keys read here.
/// </summary>
public static class RiotSettingsFile
{
    /// <summary>
    /// Returns the scalar at a dotted path such as <c>install.globals.region</c>, or <c>null</c>
    /// when the path is missing or not a scalar.
    /// </summary>
    public static string? ReadValue(string content, string path)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(path);

        var target = path.Split('.');
        var parents = new List<(int Indent, string Key)>();

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.TrimStart(' ');
            if (trimmed.Length == 0 || trimmed[0] is '#' or '-')
            {
                continue;
            }

            var colon = trimmed.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                continue;
            }

            var indent = line.Length - trimmed.Length;
            parents.RemoveAll(p => p.Indent >= indent);
            var key = trimmed[..colon].Trim();
            var value = trimmed[(colon + 1)..].Trim();

            if (value.Length == 0)
            {
                parents.Add((indent, key));
            }
            else if (IsPath(parents, key, target))
            {
                return Unquote(value);
            }
        }

        return null;
    }

    private static bool IsPath(List<(int Indent, string Key)> parents, string key, string[] target) =>
        parents.Count == target.Length - 1
        && string.Equals(key, target[^1], StringComparison.Ordinal)
        && parents.Select(p => p.Key).SequenceEqual(target[..^1], StringComparer.Ordinal);

    private static string? Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        return value is "null" or "~" ? null : value;
    }
}
