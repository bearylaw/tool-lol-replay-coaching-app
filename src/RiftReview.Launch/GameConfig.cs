namespace RiftReview.Launch;

/// <summary>
/// The League settings Rift Review may change, each with the coach's consent and a backup, in
/// <c>Config\game.cfg</c>: <c>EnableReplayApi=1</c> in <c>[General]</c>, which switches on Riot's
/// replay API, and <c>EnableDirectedCamera=1</c> in <c>[Replay]</c>, which opens replays in League's
/// directed camera.
/// </summary>
public static class GameConfig
{
    private const string Setting = "EnableReplayApi=1";
    private const string DirectedCamera = "EnableDirectedCamera=1";

    /// <summary>The path of game.cfg in an install.</summary>
    public static string PathIn(LeagueInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        return Path.Combine(installation.InstallDirectory, "Config", "game.cfg");
    }

    /// <summary>Whether the replay API is switched on in the config text.</summary>
    public static bool IsReplayApiEnabled(string config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Split('\n').Any(line => string.Equals(line.Trim(), Setting, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether replays open in League's directed camera (<c>EnableDirectedCamera=1</c> in <c>[Replay]</c>).</summary>
    public static bool IsDirectedCameraEnabled(string config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var section = string.Empty;
        foreach (var line in config.Split('\n').Select(l => l.Trim()))
        {
            if (line.StartsWith('['))
            {
                section = line;
            }
            else if (string.Equals(section, "[Replay]", StringComparison.OrdinalIgnoreCase) && string.Equals(line, DirectedCamera, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The config text with <c>EnableDirectedCamera=1</c> in <c>[Replay]</c>, replacing a switched-off
    /// line there and keeping the file's line endings; a file without <c>[Replay]</c> gets the section.
    /// </summary>
    public static string WithDirectedCamera(string config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (IsDirectedCameraEnabled(config))
        {
            return config;
        }

        var newline = config.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = config.Split(newline).ToList();
        var replay = lines.FindIndex(l => string.Equals(l.Trim(), "[Replay]", StringComparison.OrdinalIgnoreCase));
        if (replay < 0)
        {
            return config + (config.EndsWith(newline, StringComparison.Ordinal) || config.Length == 0 ? string.Empty : newline) + "[Replay]" + newline + DirectedCamera + newline;
        }

        var end = lines.FindIndex(replay + 1, l => l.TrimStart().StartsWith('['));
        end = end < 0 ? lines.Count : end;
        var existing = lines.FindIndex(replay + 1, end - replay - 1, l => l.Trim().StartsWith("EnableDirectedCamera=", StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            lines[existing] = DirectedCamera;
        }
        else
        {
            lines.Insert(replay + 1, DirectedCamera);
        }

        return string.Join(newline, lines);
    }

    /// <summary>
    /// The config text with <c>EnableReplayApi=1</c> as the first line of <c>[General]</c>, keeping
    /// the file's line endings; unchanged when the setting is already there. A file without
    /// <c>[General]</c> gets the section appended.
    /// </summary>
    public static string WithReplayApi(string config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (IsReplayApiEnabled(config))
        {
            return config;
        }

        var newline = config.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = config.Split(newline).ToList();
        var general = lines.FindIndex(l => string.Equals(l.Trim(), "[General]", StringComparison.OrdinalIgnoreCase));
        if (general < 0)
        {
            return config + (config.EndsWith(newline, StringComparison.Ordinal) || config.Length == 0 ? string.Empty : newline) + "[General]" + newline + Setting + newline;
        }

        // A line saying EnableReplayApi=0 is replaced rather than contradicted.
        lines.RemoveAll(l => l.Trim().StartsWith("EnableReplayApi=", StringComparison.OrdinalIgnoreCase));
        general = lines.FindIndex(l => string.Equals(l.Trim(), "[General]", StringComparison.OrdinalIgnoreCase));
        lines.Insert(general + 1, Setting);
        return string.Join(newline, lines);
    }
}
