using System.Text.RegularExpressions;
using System.Xml.Linq;
using RiftReview.App.Localization;
using RiftReview.Core.Matches;
using RiftReview.Core.Rofl;
using RiftReview.Launch;

namespace RiftReview.App.Tests;

/// <summary>
/// UI text is looked up by key at run time, so a typo shows as ⟦Key⟧ on screen instead of failing
/// the build. These tests read the sources and make both directions fail here instead.
/// </summary>
public sealed partial class LocalizationTests
{
    private static readonly string AppFolder = Path.Combine(FindRepositoryRoot(), "src", "RiftReview.App");

    [Fact]
    public void EveryKeyTheAppUsesExists()
    {
        var missing = UsedKeys().Except(ResourceKeys(), StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryResourceIsUsed()
    {
        var unused = ResourceKeys().Except(UsedKeys(), StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.Empty(unused);
    }

    [Fact]
    public void TheEmbeddedResourcesLoadAtRunTime()
    {
        Assert.Equal("Rift Review", Loc.Get("App_Title"));
        Assert.Equal("⟦No_Such_Key⟧", Loc.Get("No_Such_Key"));
    }

    private static HashSet<string> UsedKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(AppFolder, "*.xaml", SearchOption.AllDirectories).Where(IsSource))
        {
            keys.UnionWith(XamlKey().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value));
        }

        foreach (var file in Directory.EnumerateFiles(AppFolder, "*.cs", SearchOption.AllDirectories).Where(IsSource))
        {
            keys.UnionWith(CodeKey().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value));
        }

        // Keys built from enum names at run time, e.g. Loc.Get($"Role_{position}").
        keys.UnionWith(Enum.GetNames<Position>().Select(n => $"Role_{n}"));
        keys.UnionWith(Enum.GetNames<Position>().Select(n => $"Overlay_Quest_{n}"));
        keys.UnionWith(Enum.GetNames<RoflErrorKind>().Select(n => $"RoflError_{n}"));
        keys.UnionWith(Enum.GetNames<Compatibility>().Select(n => $"Compat_{n}"));
        keys.UnionWith(Enum.GetNames<Compatibility>().Where(n => n != nameof(Compatibility.Compatible)).Select(n => $"Launch_Confirm_{n}"));
        keys.UnionWith(Enum.GetNames<WatchFailure>().Select(n => $"Watch_Failed_{n}"));
        return keys;
    }

    private static HashSet<string> ResourceKeys() =>
        XDocument.Load(Path.Combine(AppFolder, "Localization", "Strings.resx"))
            .Root!.Elements("data")
            .Select(e => (string)e.Attribute("name")!)
            .ToHashSet(StringComparer.Ordinal);

    private static bool IsSource(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RiftReview.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"RiftReview.slnx not found above {AppContext.BaseDirectory}");
    }

    [GeneratedRegex(@"\{loc:Text (?:Key=)?([A-Za-z0-9_]+)\}")]
    private static partial Regex XamlKey();

    [GeneratedRegex(@"Loc\.(?:Get|Format)\(\s*""([A-Za-z0-9_]+)""")]
    private static partial Regex CodeKey();
}
