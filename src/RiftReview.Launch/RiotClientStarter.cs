using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace RiftReview.Launch;

/// <summary>
/// Starts the League client the way the Start-menu shortcut does:
/// <c>RiotClientServices.exe --launch-product=league_of_legends --launch-patchline=live</c>.
/// </summary>
public static class RiotClientStarter
{
    /// <summary>
    /// Finds <c>RiotClientServices.exe</c> from <c>RiotClientInstalls.json</c> (the client
    /// associated with this League install, else the default client). <c>null</c> when unknown.
    /// </summary>
    public static string? FindRiotClient(string riotClientInstallsJson, LeagueInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        try
        {
            using var document = JsonDocument.Parse(riotClientInstallsJson);
            var root = document.RootElement;
            if (root.TryGetProperty("associated_client", out var associated) && associated.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in associated.EnumerateObject())
                {
                    var installDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(entry.Name));
                    if (string.Equals(installDirectory, installation.InstallDirectory, StringComparison.OrdinalIgnoreCase)
                        && entry.Value.ValueKind == JsonValueKind.String)
                    {
                        return Path.GetFullPath(entry.Value.GetString()!);
                    }
                }
            }

            return root.TryGetProperty("rc_default", out var fallback) && fallback.ValueKind == JsonValueKind.String
                ? Path.GetFullPath(fallback.GetString()!)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The Riot Client of this machine, read from <c>%ProgramData%\Riot Games\RiotClientInstalls.json</c>.</summary>
    public static string? FindRiotClient(InstallSearchPaths paths, LeagueInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var file = Path.Combine(Path.GetDirectoryName(paths.RiotMetadataDirectory)!, "RiotClientInstalls.json");
        return File.Exists(file) ? FindRiotClient(File.ReadAllText(file), installation) : null;
    }

    /// <summary>Starts the client; returns an English error detail, or <c>null</c> on success.</summary>
    public static string? Start(string riotClientServices)
    {
        if (!File.Exists(riotClientServices))
        {
            return $"{riotClientServices} not found";
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(riotClientServices)
            {
                ArgumentList = { "--launch-product=league_of_legends", "--launch-patchline=live" },
                UseShellExecute = false,
            });
            return process is null ? "Process.Start returned no process" : null;
        }
        catch (Win32Exception exception)
        {
            return $"{riotClientServices}: {exception.Message}";
        }
    }
}
