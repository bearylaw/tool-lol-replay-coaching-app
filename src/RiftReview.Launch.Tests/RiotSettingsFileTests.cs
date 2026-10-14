namespace RiftReview.Launch.Tests;

public sealed class RiotSettingsFileTests
{
    // Shape of league_of_legends.live.product_settings.yaml, shortened.
    private const string ProductSettings = """
        auto_patching_enabled_by_player: false
        dependencies:
            Direct X 9:
                phase: "Succeeded"
                version: "1.0.0"
            vanguard: true
        locale_data:
            available_locales:
            - "de_DE"
            - "en_GB"
            default_locale: "en_GB"
        product_install_full_path: "C:/Riot Games/League of Legends"
        settings:
            create_shortcut: true
            locale: "de_DE"
        shortcut_name: "League of Legends.lnk"
        """;

    // Shape of Config/LeagueClientSettings.yaml, shortened.
    private const string ClientSettings = """
        install:
            game-settings:
                accountId: 1
            gameflow-patcher-lock: null
            globals:
                locale: "en_GB"
                region: "EUW"
            patcher:
                locales:
                - "en_GB"
        """;

    [Fact]
    public void ReadsATopLevelValue()
    {
        Assert.Equal("C:/Riot Games/League of Legends", RiotSettingsFile.ReadValue(ProductSettings, "product_install_full_path"));
    }

    [Fact]
    public void ReadsANestedValueByItsFullPath()
    {
        Assert.Equal("de_DE", RiotSettingsFile.ReadValue(ProductSettings, "settings.locale"));
        Assert.Equal("EUW", RiotSettingsFile.ReadValue(ClientSettings, "install.globals.region"));
    }

    [Fact]
    public void DoesNotConfuseKeysWithTheSameNameUnderOtherParents()
    {
        Assert.Equal("en_GB", RiotSettingsFile.ReadValue(ProductSettings, "locale_data.default_locale"));
        Assert.Null(RiotSettingsFile.ReadValue(ProductSettings, "locale"));
    }

    [Theory]
    [InlineData("install.globals")]
    [InlineData("install.gameflow-patcher-lock")]
    [InlineData("install.missing.region")]
    public void ReturnsNullForSectionsNullsAndMissingKeys(string path)
    {
        Assert.Null(RiotSettingsFile.ReadValue(ClientSettings, path));
    }

    [Fact]
    public void HandlesWindowsLineEndings()
    {
        Assert.Equal("EUW", RiotSettingsFile.ReadValue(ClientSettings.ReplaceLineEndings("\r\n"), "install.globals.region"));
    }
}
