using System.Diagnostics;
using RiftReview.Core.Matches;
using RiftReview.Core.Results;

namespace RiftReview.Launch;

/// <summary>Where the locator looks, injected so tests can point it at a temporary folder.</summary>
/// <param name="RiotMetadataDirectory">Normally <c>%ProgramData%\Riot Games\Metadata</c>.</param>
/// <param name="DefaultInstallDirectory">Normally <c>C:\Riot Games\League of Legends</c>.</param>
public sealed record InstallSearchPaths(string RiotMetadataDirectory, string DefaultInstallDirectory)
{
    /// <summary>The paths of a standard Windows install.</summary>
    public static InstallSearchPaths ForThisMachine() => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games", "Metadata"),
        @"C:\Riot Games\League of Legends");
}

/// <summary>
/// Finds the League install the way the Riot Client records it: the user's override first, then
/// the Riot Client's product settings, then the default folder.
/// </summary>
public static class LeagueInstallLocator
{
    private const string FallbackLocale = "en_US";

    /// <summary>
    /// Locates the install. <paramref name="overrideDirectory"/> is the folder the user picked, if any.
    /// Fails with the folders it tried when none contains the game.
    /// </summary>
    public static Result<LeagueInstallation, string> Locate(InstallSearchPaths paths, string? overrideDirectory)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var productSettings = ReadOrEmpty(Path.Combine(
            paths.RiotMetadataDirectory, "league_of_legends.live", "league_of_legends.live.product_settings.yaml"));

        var candidates = new[]
        {
            overrideDirectory,
            RiotSettingsFile.ReadValue(productSettings, "product_install_full_path"),
            paths.DefaultInstallDirectory,
        };

        var tried = new List<string>();
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
            var executable = Path.Combine(directory, "Game", "League of Legends.exe");
            if (File.Exists(executable))
            {
                return Result<LeagueInstallation, string>.Success(Describe(directory, executable, productSettings));
            }

            tried.Add(directory);
        }

        return Result<LeagueInstallation, string>.Failure(
            $"Game\\League of Legends.exe not found in: {string.Join("; ", tried.Distinct(StringComparer.OrdinalIgnoreCase))}");
    }

    private static LeagueInstallation Describe(string directory, string executable, string productSettings)
    {
        var clientSettings = ReadOrEmpty(Path.Combine(directory, "Config", "LeagueClientSettings.yaml"));
        var locale = RiotSettingsFile.ReadValue(productSettings, "settings.locale")
            ?? RiotSettingsFile.ReadValue(clientSettings, "install.globals.locale")
            ?? FallbackLocale;

        return new LeagueInstallation
        {
            InstallDirectory = directory,
            GameExecutable = executable,
            Version = GameVersion.TryParse(FileVersionInfo.GetVersionInfo(executable).FileVersion),
            Locale = locale,
            Region = RiotSettingsFile.ReadValue(clientSettings, "install.globals.region"),
        };
    }

    // Missing settings files are normal on a fresh install; the caller falls back to defaults.
    private static string ReadOrEmpty(string path) => File.Exists(path) ? File.ReadAllText(path) : string.Empty;
}
