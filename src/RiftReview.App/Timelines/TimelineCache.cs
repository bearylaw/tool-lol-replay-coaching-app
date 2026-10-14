using System.Security.Cryptography;
using RiftReview.Core.Logging;
using RiftReview.Core.Matches;
using RiftReview.Core.Results;
using RiftReview.Core.Timelines;
using RiftReview.Timeline;

namespace RiftReview.App.Timelines;

/// <summary>
/// Builds a replay's timeline once and keeps it as JSON in <c>%LOCALAPPDATA%\RiftReview\timelines</c>,
/// keyed by the file's content so renamed or copied replays share one timeline.
/// </summary>
internal sealed class TimelineCache(string folder, AppLog log)
{
    private const int SampleLength = 64 * 1024;

    /// <summary>The stored timeline, or a new one built from the replay file off the UI thread.</summary>
    public Task<Result<MatchTimeline, string>> GetAsync(string replayPath, ReplayMetadata metadata, CancellationToken cancellationToken) =>
        Task.Run(() => Get(replayPath, metadata), cancellationToken);

    private Result<MatchTimeline, string> Get(string replayPath, ReplayMetadata metadata)
    {
        try
        {
            using var stream = new FileStream(replayPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var path = Path.Combine(folder, Key(stream) + ".json");
            if (File.Exists(path) && TimelineJson.Read(File.ReadAllText(path)).TryGetValue(out var read, out _))
            {
                if (read.BuilderVersion >= TimelineBuilder.Version)
                {
                    return Result<MatchTimeline, string>.Success(read);
                }
            }

            var started = Environment.TickCount64;
            var built = TimelineBuilder.Build(stream, metadata);
            if (built.TryGetValue(out var timeline, out var error))
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(path, TimelineJson.Write(timeline));
                log.Info("timeline.built", ("file", Path.GetFileName(replayPath)), ("events", timeline.Events.Count), ("turrets", timeline.TurretFalls.Count), ("ms", Environment.TickCount64 - started));
                return Result<MatchTimeline, string>.Success(timeline);
            }
            else
            {
                log.Warning("timeline.failed", null, ("file", Path.GetFileName(replayPath)), ("detail", error));
            }

            return built;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            log.Warning("timeline.unreadable", exception, ("file", Path.GetFileName(replayPath)));
            return Result<MatchTimeline, string>.Failure(exception.Message);
        }
    }

    // Length plus the first and last 64 KB identify a replay (they hold its header, version and
    // metadata) without hashing 35 MB every time.
    private static string Key(Stream stream)
    {
        var sample = new byte[Math.Min(SampleLength, stream.Length)];
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(BitConverter.GetBytes(stream.Length));
        stream.Position = 0;
        stream.ReadExactly(sample);
        hash.AppendData(sample);
        stream.Position = stream.Length - sample.Length;
        stream.ReadExactly(sample);
        hash.AppendData(sample);
        return Convert.ToHexStringLower(hash.GetHashAndReset())[..32];
    }
}
