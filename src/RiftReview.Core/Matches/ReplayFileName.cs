using System.Globalization;
using System.Text.RegularExpressions;

namespace RiftReview.Core.Matches;

/// <summary>
/// The name the League client gives a downloaded replay, <c>EUW1-7000000004.rofl</c>: the platform
/// the game was played on and its game id. Files players rename carry neither.
/// </summary>
public sealed partial record ReplayFileName(string PlatformId, long GameId)
{
    /// <summary>Reads platform and game id from a file name or path; <c>null</c> when it is not a client name.</summary>
    public static ReplayFileName? TryParse(string? fileNameOrPath)
    {
        if (string.IsNullOrEmpty(fileNameOrPath))
        {
            return null;
        }

        var match = ClientNamePattern().Match(Path.GetFileName(fileNameOrPath));
        return match.Success && long.TryParse(match.Groups[2].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var gameId)
            ? new ReplayFileName(match.Groups[1].Value.ToUpperInvariant(), gameId)
            : null;
    }

    [GeneratedRegex(@"^([A-Za-z]{2,4}[0-9]?)-([0-9]{1,18})\.rofl$", RegexOptions.CultureInvariant)]
    private static partial Regex ClientNamePattern();
}
