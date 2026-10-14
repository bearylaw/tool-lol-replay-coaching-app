using RiftReview.App.Settings;
using RiftReview.Core.Logging;

namespace RiftReview.App.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private static readonly AppSettings Defaults = new() { OwnReplaysFolder = @"C:\Own", PlayerReplaysFolder = @"C:\Players" };

    private readonly string folder = Path.Combine(Path.GetTempPath(), "riftreview-tests", Guid.NewGuid().ToString("N"));
    private readonly StringWriter logOutput = new();

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private string FilePath => Path.Combine(folder, "settings.json");

    [Fact]
    public void ReturnsTheDefaultsOnFirstRun()
    {
        Assert.Same(Defaults, Store().Load(Defaults));
    }

    [Fact]
    public void RoundTripsEverySetting()
    {
        var saved = Defaults with
        {
            PlayerReplaysFolder = @"D:\Team\Replays",
            LeagueInstallOverride = @"E:\Games\League",
            Roster = ["Player One#EUW", "Player Two#123"],
        };

        Store().Save(saved);
        var loaded = Store().Load(Defaults);

        Assert.Equal(saved.PlayerReplaysFolder, loaded.PlayerReplaysFolder);
        Assert.Equal(saved.LeagueInstallOverride, loaded.LeagueInstallOverride);
        Assert.Equal(saved.Roster, loaded.Roster);
        Assert.True(loaded.IsOnRoster("player one#euw"));
    }

    [Fact]
    public void RoundTripsDisplayAndOverlaySettings()
    {
        var saved = Defaults with
        {
            InterfaceScale = 1.1,
            Overlay = new OverlaySettings { Scale = 1.4, PauseWhileNoting = true, DrawingFadeSeconds = 20, HideLeagueCameraPanel = true },
        };

        Store().Save(saved);
        var loaded = Store().Load(Defaults);

        Assert.Equal(saved.InterfaceScale, loaded.InterfaceScale);
        Assert.Equal(saved.Overlay, loaded.Overlay);
    }

    [Fact]
    public void AVersionOneFileGetsTheDefaultOverlaySettings()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(FilePath, """{"version":1,"playerReplaysFolder":"D:\\Old","roster":[]}""");

        var loaded = Store().Load(Defaults);

        Assert.Equal(@"D:\Old", loaded.PlayerReplaysFolder);
        Assert.Equal(new OverlaySettings(), loaded.Overlay);
        Assert.Equal(1, loaded.InterfaceScale);
    }

    [Fact]
    public void BringsHandEditedNumbersIntoRange()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(FilePath, """
            {"version":2,"interfaceScale":9,
             "overlay":{"scale":0.1,"drawingFadeSeconds":500}}
            """);

        var loaded = Store().Load(Defaults);

        Assert.Equal(1.25, loaded.InterfaceScale);
        Assert.Equal(0.6, loaded.Overlay.Scale);
        Assert.Equal(60, loaded.Overlay.DrawingFadeSeconds);
    }

    [Fact]
    public void AnUnreadableSectionFallsBackAloneAndKeepsTheFolders()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(FilePath, """{"version":2,"playerReplaysFolder":"D:\\Team","overlay":{"scale":"huge"},"viewer":{"iconScale":2},"capture":{"speed":4}}""");

        var loaded = Store().Load(Defaults);

        Assert.Equal(@"D:\Team", loaded.PlayerReplaysFolder);
        // The broken overlay section falls back; sections from older versions are ignored.
        Assert.Equal(new OverlaySettings(), loaded.Overlay);
        Assert.Contains("settings.section_unreadable", logOutput.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MigratesAFileWithoutAVersion()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(FilePath, """{"playerReplaysFolder":"D:\\Old","roster":["A#1"]}""");

        var loaded = Store().Load(Defaults);

        Assert.Equal(@"D:\Old", loaded.PlayerReplaysFolder);
        Assert.Equal(Defaults.OwnReplaysFolder, loaded.OwnReplaysFolder);
        Assert.Equal(["A#1"], loaded.Roster);
    }

    [Fact]
    public void KeepsABrokenFileAndFallsBackToDefaults()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(FilePath, "{ not json");

        var loaded = Store().Load(Defaults);

        Assert.Same(Defaults, loaded);
        Assert.Equal("{ not json", File.ReadAllText(FilePath + ".broken"));
        Assert.Contains("settings.unreadable", logOutput.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesSettingsFromANewerVersion()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(FilePath, """{"version":99,"playerReplaysFolder":"D:\\Future"}""");

        Assert.Same(Defaults, Store().Load(Defaults));
    }

    private SettingsStore Store() => new(FilePath, new AppLog(logOutput, TimeProvider.System));
}
