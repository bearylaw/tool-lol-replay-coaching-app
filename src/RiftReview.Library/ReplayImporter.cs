using System.Security.Cryptography;
using RiftReview.Core.Rofl;

namespace RiftReview.Library;

/// <summary>What happened to one file during an import.</summary>
public enum ImportResult
{
    /// <summary>Copied into the player folder under its own name.</summary>
    Imported,

    /// <summary>Copied under a new name because another replay already had its name.</summary>
    ImportedRenamed,

    /// <summary>The same file is already in the folder; nothing was copied.</summary>
    AlreadyPresent,

    /// <summary>The file is not a League replay; nothing was copied.</summary>
    NotAReplay,

    /// <summary>Reading or copying failed.</summary>
    Failed,
}

/// <summary>The outcome for one source file. <see cref="TargetPath"/> is set when the replay is in the folder.</summary>
public sealed record ImportOutcome(string SourcePath, ImportResult Result, string? TargetPath, string? Detail);

/// <summary>Copies replay files players sent into the player folder. Never overwrites and never moves.</summary>
public static class ReplayImporter
{
    /// <summary>Imports each file into <paramref name="targetFolder"/>, creating the folder if needed.</summary>
    public static IReadOnlyList<ImportOutcome> Import(IEnumerable<string> sourcePaths, string targetFolder)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFolder);

        Directory.CreateDirectory(targetFolder);
        return [.. sourcePaths.Select(path => ImportOne(path, targetFolder))];
    }

    private static ImportOutcome ImportOne(string sourcePath, string targetFolder)
    {
        try
        {
            var source = Path.GetFullPath(sourcePath);
            if (!HasReplaySignature(source))
            {
                return new ImportOutcome(source, ImportResult.NotAReplay, null, null);
            }

            if (IsInside(source, targetFolder))
            {
                return new ImportOutcome(source, ImportResult.AlreadyPresent, source, null);
            }

            var name = Path.GetFileNameWithoutExtension(source);
            for (var attempt = 1; ; attempt++)
            {
                var candidate = Path.Combine(targetFolder, attempt == 1 ? $"{name}.rofl" : $"{name} ({attempt}).rofl");
                if (!File.Exists(candidate))
                {
                    File.Copy(source, candidate, overwrite: false);
                    var result = attempt == 1 ? ImportResult.Imported : ImportResult.ImportedRenamed;
                    return new ImportOutcome(source, result, candidate, null);
                }

                if (HaveSameContent(source, candidate))
                {
                    return new ImportOutcome(source, ImportResult.AlreadyPresent, candidate, null);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ImportOutcome(sourcePath, ImportResult.Failed, null, exception.Message);
        }
    }

    private static bool HasReplaySignature(string path)
    {
        Span<byte> start = stackalloc byte[4];
        using var stream = File.OpenRead(path);
        return stream.ReadAtLeast(start, start.Length, throwOnEndOfStream: false) == start.Length
            && start.SequenceEqual(RoflReader.Signature[..4]);
    }

    private static bool IsInside(string path, string folder)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(folder), path);
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    private static bool HaveSameContent(string first, string second)
    {
        if (new FileInfo(first).Length != new FileInfo(second).Length)
        {
            return false;
        }

        using var a = File.OpenRead(first);
        using var b = File.OpenRead(second);
        return SHA256.HashData(a).AsSpan().SequenceEqual(SHA256.HashData(b));
    }
}
