using RiftReview.Core.Matches;
using RiftReview.Core.Results;
using RiftReview.Core.Rofl;

namespace RiftReview.Library;

/// <summary>Where a replay came from.</summary>
public enum ReplaySource
{
    /// <summary>The coach's own replays, downloaded by the League client.</summary>
    Own,

    /// <summary>Replays players sent, dropped into the player folder.</summary>
    Players,
}

/// <summary>A folder the library scans, including its subfolders.</summary>
public sealed record LibraryFolder(string Path, ReplaySource Source);

/// <summary>One <c>.rofl</c> file in the library and what could be read from it.</summary>
public sealed record ReplayEntry
{
    /// <summary>Full path of the file.</summary>
    public required string FilePath { get; init; }

    /// <summary>Which library folder the file is in.</summary>
    public required ReplaySource Source { get; init; }

    /// <summary>The subfolder inside the library folder, for example a player's name; empty at the top level.</summary>
    public required string Group { get; init; }

    /// <summary>File size in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>When the file was last written (downloaded or copied), in UTC.</summary>
    public required DateTimeOffset ModifiedUtc { get; init; }

    /// <summary>The metadata, or why it could not be read.</summary>
    public required Result<ReplayMetadata, RoflError> Metadata { get; init; }

    /// <summary>The file name with extension.</summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>Platform and game id when the file still has the client's name.</summary>
    public ReplayFileName? ClientName => ReplayFileName.TryParse(FilePath);
}
