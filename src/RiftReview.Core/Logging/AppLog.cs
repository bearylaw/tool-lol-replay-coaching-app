using System.Text;
using System.Text.Json;

namespace RiftReview.Core.Logging;

/// <summary>Severity of a log line.</summary>
public enum LogLevel
{
    /// <summary>Normal operation worth recording: startup, scans, launches.</summary>
    Info,

    /// <summary>Something went wrong and the app carried on, for example an icon download.</summary>
    Warning,

    /// <summary>A failure the user saw, or a crash.</summary>
    Error,
}

/// <summary>
/// Structured log: one JSON object per line, written to the writer given at the composition root.
/// Never log Riot IDs, account ids or file contents; file paths and game ids are fine.
/// </summary>
public sealed class AppLog : IDisposable
{
    private readonly TextWriter writer;
    private readonly TimeProvider clock;
    private readonly Lock gate = new();

    /// <summary>Creates a log writing to <paramref name="writer"/>; the log owns and disposes it.</summary>
    public AppLog(TextWriter writer, TimeProvider clock)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Opens (appending) a daily log file <c>riftreview-yyyyMMdd.log</c> in <paramref name="directory"/>.</summary>
    public static AppLog ToDailyFile(string directory, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Directory.CreateDirectory(directory);
        var day = clock.GetUtcNow().ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var stream = new FileStream(Path.Combine(directory, $"riftreview-{day}.log"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        return new AppLog(new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true }, clock);
    }

    /// <summary>Logs normal operation.</summary>
    public void Info(string eventName, params (string Key, object? Value)[] fields) =>
        Write(LogLevel.Info, eventName, null, fields);

    /// <summary>Logs a problem the app recovered from.</summary>
    public void Warning(string eventName, Exception? exception, params (string Key, object? Value)[] fields) =>
        Write(LogLevel.Warning, eventName, exception, fields);

    /// <summary>Logs a failure, with the original exception when there is one.</summary>
    public void Error(string eventName, Exception? exception, params (string Key, object? Value)[] fields) =>
        Write(LogLevel.Error, eventName, exception, fields);

    /// <inheritdoc />
    public void Dispose() => writer.Dispose();

    private void Write(LogLevel level, string eventName, Exception? exception, (string Key, object? Value)[] fields)
    {
        var line = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ts"] = clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["level"] = level.ToString().ToLowerInvariant(),
            ["event"] = eventName,
        };
        foreach (var (key, value) in fields)
        {
            line[key] = value?.ToString();
        }

        if (exception is not null)
        {
            line["error"] = exception.ToString();
        }

        var json = JsonSerializer.Serialize(line);
        lock (gate)
        {
            writer.WriteLine(json);
        }
    }
}
