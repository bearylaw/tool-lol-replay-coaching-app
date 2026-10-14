using System.Globalization;

namespace RiftReview.Launch;

/// <summary>
/// Where the running League client's local API listens, from the <c>lockfile</c> it writes into
/// its install folder while it runs: <c>LeagueClient:pid:port:password:https</c>.
/// </summary>
public sealed record ClientLockfile(int Port, string Password)
{
    /// <summary>The lockfile's path inside a League install.</summary>
    public static string PathIn(LeagueInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        return Path.Combine(installation.InstallDirectory, "lockfile");
    }

    /// <summary>Parses the lockfile's content; <c>null</c> when it does not have the expected shape.</summary>
    public static ClientLockfile? Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var parts = content.Trim().Split(':');
        if (parts.Length != 5
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port is <= 0 or > 65535
            || parts[3].Length == 0
            || !string.Equals(parts[4], "https", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new ClientLockfile(port, parts[3]);
    }
}
