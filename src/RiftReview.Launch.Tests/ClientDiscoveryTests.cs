namespace RiftReview.Launch.Tests;

public sealed class ClientDiscoveryTests
{
    private static readonly LeagueInstallation League = new()
    {
        InstallDirectory = @"C:\Riot Games\League of Legends",
        GameExecutable = @"C:\Riot Games\League of Legends\Game\League of Legends.exe",
        Version = null,
        Locale = "en_GB",
        Region = "EUW",
    };

    [Fact]
    public void ReadsPortAndPasswordFromTheLockfile()
    {
        Assert.Equal(new ClientLockfile(55339, "pa55-word_x"), ClientLockfile.Parse("LeagueClient:22944:55339:pa55-word_x:https"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("LeagueClient:22944:notaport:pw:https")]
    [InlineData("LeagueClient:22944:55339::https")]
    [InlineData("LeagueClient:22944:55339:pw:http")]
    [InlineData("LeagueClient:22944:99999:pw:https")]
    public void RejectsALockfileOfAnotherShape(string content)
    {
        Assert.Null(ClientLockfile.Parse(content));
    }

    [Fact]
    public void ReadsTheLockfileWhileTheClientHoldsItOpenForWriting()
    {
        // Regression: File.ReadAllText failed with a sharing violation while the client ran, so
        // the app said "client not running" with the client open.
        var folder = Path.Combine(Path.GetTempPath(), "riftreview-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var league = League with { InstallDirectory = folder };
            using (var held = new FileStream(ClientLockfile.PathIn(league), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
            {
                held.Write("LeagueClient:22944:55339:secret:https"u8);
                held.Flush();

                Assert.Equal(new ClientLockfile(55339, "secret"), ClientReplayWatcher.FindRunningClient(league));
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void FindsNoClientWithoutALockfile()
    {
        Assert.Null(ClientReplayWatcher.FindRunningClient(League with { InstallDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) }));
    }

    [Fact]
    public void FindsTheRiotClientAssociatedWithTheLeagueInstall()
    {
        // Shape of %ProgramData%\Riot Games\RiotClientInstalls.json.
        const string installs = """
            {"associated_client":{"C:/Riot Games/League of Legends/":"C:/Riot Games/Riot Client/RiotClientServices.exe",
             "C:/Riot Games/VALORANT/live/":"D:/Other/RiotClientServices.exe"},
             "rc_default":"E:/Default/RiotClientServices.exe"}
            """;

        Assert.Equal(@"C:\Riot Games\Riot Client\RiotClientServices.exe", RiotClientStarter.FindRiotClient(installs, League));
    }

    [Fact]
    public void FallsBackToTheDefaultRiotClient()
    {
        Assert.Equal(@"E:\Default\RiotClientServices.exe", RiotClientStarter.FindRiotClient("""{"rc_default":"E:/Default/RiotClientServices.exe"}""", League));
        Assert.Null(RiotClientStarter.FindRiotClient("{ broken", League));
    }
}
