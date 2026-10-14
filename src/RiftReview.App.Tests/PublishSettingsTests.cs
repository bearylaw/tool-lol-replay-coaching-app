using System.Xml.Linq;

namespace RiftReview.App.Tests;

/// <summary>Guards build settings whose failure only shows after <c>just build</c>.</summary>
public sealed class PublishSettingsTests
{
    [Fact]
    public void SingleFileAnalyzerIsOnInEveryBuildSoTheLockfileMatchesPublish()
    {
        // Regression: with the analyzer on only during publish, `just build` added
        // Microsoft.NET.ILLink.Tasks to packages.lock.json and `just install` (locked mode) failed.
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "RiftReview.App", "RiftReview.App.csproj"));

        var unconditional = project.Root!.Elements("PropertyGroup")
            .Where(group => group.Attribute("Condition") is null)
            .Elements("EnableSingleFileAnalyzer")
            .Select(element => element.Value);

        Assert.Contains("true", unconditional);
    }

    private static string RepositoryRoot()
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
}
