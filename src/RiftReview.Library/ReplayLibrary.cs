using RiftReview.Core.Matches;
using RiftReview.Core.Results;
using RiftReview.Core.Rofl;

namespace RiftReview.Library;

/// <summary>Finds and reads every replay in the library folders.</summary>
public static class ReplayLibrary
{
    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.CaseInsensitive,
    };

    /// <summary>
    /// Reads every <c>.rofl</c> under the folders, newest first. Folders that do not exist are
    /// skipped; files that cannot be read become entries with an error, never an exception.
    /// </summary>
    public static IReadOnlyList<ReplayEntry> Scan(IEnumerable<LibraryFolder> folders, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folders);

        var entries = new List<ReplayEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder.Path))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(folder.Path, "*.rofl", Recursive))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (seen.Add(Path.GetFullPath(path)))
                {
                    entries.Add(ReadEntry(path, folder));
                }
            }
        }

        return [.. WithoutClientCopies(entries).OrderByDescending(e => e.ModifiedUtc).ThenBy(e => e.FileName, StringComparer.OrdinalIgnoreCase)];
    }

    // Watching a player replay copies it into the League client's folder (the own folder), and the
    // copy keeps size and write time. List it once, as the player replay it came from.
    private static IEnumerable<ReplayEntry> WithoutClientCopies(List<ReplayEntry> entries)
    {
        var playerFiles = entries
            .Where(e => e.Source == ReplaySource.Players)
            .Select(e => (e.SizeBytes, e.ModifiedUtc))
            .ToHashSet();
        return entries.Where(e => e.Source != ReplaySource.Own || !playerFiles.Contains((e.SizeBytes, e.ModifiedUtc)));
    }

    /// <summary>Reads one file into an entry; used by the scan and when a single file is opened.</summary>
    public static ReplayEntry ReadEntry(string path, LibraryFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        var info = new FileInfo(path);
        var relativeDirectory = Path.GetRelativePath(folder.Path, info.DirectoryName ?? folder.Path);
        return new ReplayEntry
        {
            FilePath = info.FullName,
            Source = folder.Source,
            Group = relativeDirectory is "." || relativeDirectory.StartsWith("..", StringComparison.Ordinal) ? string.Empty : relativeDirectory,
            SizeBytes = info.Exists ? info.Length : 0,
            ModifiedUtc = info.Exists ? info.LastWriteTimeUtc : DateTimeOffset.MinValue,
            Metadata = ReadMetadata(info.FullName),
        };
    }

    private static Result<ReplayMetadata, RoflError> ReadMetadata(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return RoflReader.Read(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<ReplayMetadata, RoflError>.Failure(new RoflError(RoflErrorKind.Unreadable, exception.Message));
        }
    }
}
