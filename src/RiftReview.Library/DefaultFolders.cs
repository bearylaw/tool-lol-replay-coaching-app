namespace RiftReview.Library;

/// <summary>The folders the library uses until the coach picks others.</summary>
public static class DefaultFolders
{
    private const int MaxLevelsUp = 6;

    /// <summary>Where the League client saves replays: <c>Documents\League of Legends\Replays</c>.</summary>
    public static string OwnReplays(string documentsFolder) =>
        Path.Combine(documentsFolder, "League of Legends", "Replays");

    /// <summary>
    /// The player folder: the repository's <c>replays</c> folder when the app runs from inside
    /// this repository (for example <c>dist\RiftReview.exe</c>), else <c>Documents\Rift Review\Player Replays</c>.
    /// </summary>
    public static string PlayerReplays(string appDirectory, string documentsFolder)
    {
        var directory = new DirectoryInfo(appDirectory);
        for (var level = 0; directory is not null && level <= MaxLevelsUp; level++, directory = directory.Parent)
        {
            var isRepository = File.Exists(Path.Combine(directory.FullName, "RiftReview.slnx"))
                && File.Exists(Path.Combine(directory.FullName, "replays", "README.md"));
            if (isRepository)
            {
                return Path.Combine(directory.FullName, "replays");
            }
        }

        return Path.Combine(documentsFolder, "Rift Review", "Player Replays");
    }
}
