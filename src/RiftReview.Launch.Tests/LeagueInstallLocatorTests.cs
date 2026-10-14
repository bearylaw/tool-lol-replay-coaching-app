namespace RiftReview.Launch.Tests;

public sealed class LeagueInstallLocatorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "riftreview-tests", Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void FindsTheInstallTheRiotClientRecorded()
    {
        var install = FakeInstall("Games/LoL", region: "EUW", clientLocale: "en_GB");
        WriteProductSettings($"product_install_full_path: \"{install.Replace('\\', '/')}\"\nsettings:\n    locale: \"de_DE\"\n");

        var found = Locate(overrideDirectory: null);

        Assert.Equal(install, found.InstallDirectory);
        Assert.Equal(Path.Combine(install, "Game", "League of Legends.exe"), found.GameExecutable);
        Assert.Equal("de_DE", found.Locale);
        Assert.Equal("EUW", found.Region);
    }

    [Fact]
    public void PrefersTheFolderTheUserPicked()
    {
        FakeInstall("Recorded");
        var picked = FakeInstall("Picked");
        WriteProductSettings($"product_install_full_path: \"{Path.Combine(root, "Recorded")}\"\n");

        Assert.Equal(picked, Locate(overrideDirectory: picked + Path.DirectorySeparatorChar).InstallDirectory);
    }

    [Fact]
    public void FallsBackToTheDefaultFolderAndClientSettings()
    {
        var install = FakeInstall("Default", region: "EUNE", clientLocale: "pl_PL");

        var found = Locate(overrideDirectory: null);

        Assert.Equal(install, found.InstallDirectory);
        Assert.Equal("pl_PL", found.Locale);
        Assert.Equal("EUNE", found.Region);
    }

    [Fact]
    public void UsesEnglishWhenNoLocaleIsRecorded()
    {
        FakeInstall("Default");

        Assert.Equal("en_US", Locate(overrideDirectory: null).Locale);
        Assert.Null(Locate(overrideDirectory: null).Region);
    }

    [Fact]
    public void ReportsEveryFolderItTriedWhenNoGameIsFound()
    {
        Directory.CreateDirectory(root);

        var result = LeagueInstallLocator.Locate(Paths(), Path.Combine(root, "Nowhere"));

        Assert.False(result.TryGetValue(out _, out var error));
        Assert.Contains(Path.Combine(root, "Nowhere"), error, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(root, "Default"), error, StringComparison.Ordinal);
    }

    private InstallSearchPaths Paths() => new(Path.Combine(root, "Metadata"), Path.Combine(root, "Default"));

    private LeagueInstallation Locate(string? overrideDirectory)
    {
        var result = LeagueInstallLocator.Locate(Paths(), overrideDirectory);
        Assert.True(result.TryGetValue(out var installation, out var error), error);
        return installation;
    }

    private string FakeInstall(string relative, string? region = null, string? clientLocale = null)
    {
        var directory = Path.GetFullPath(Path.Combine(root, relative));
        Directory.CreateDirectory(Path.Combine(directory, "Game"));
        File.WriteAllBytes(Path.Combine(directory, "Game", "League of Legends.exe"), []);
        if (region is not null || clientLocale is not null)
        {
            Directory.CreateDirectory(Path.Combine(directory, "Config"));
            File.WriteAllText(
                Path.Combine(directory, "Config", "LeagueClientSettings.yaml"),
                $"install:\n    globals:\n        locale: \"{clientLocale}\"\n        region: \"{region}\"\n");
        }

        return directory;
    }

    private void WriteProductSettings(string content)
    {
        var directory = Path.Combine(root, "Metadata", "league_of_legends.live");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "league_of_legends.live.product_settings.yaml"), content);
    }
}
