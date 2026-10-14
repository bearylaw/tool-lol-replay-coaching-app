using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using RiftReview.Core.Matches;
using RiftReview.Core.Results;

namespace RiftReview.Launch;

/// <summary>Why the client did not start a replay.</summary>
public enum WatchFailure
{
    /// <summary>The League client is not running (no lockfile).</summary>
    ClientNotRunning,

    /// <summary>The client is running but nobody is logged in yet.</summary>
    NotLoggedIn,

    /// <summary>The client is patching.</summary>
    Patching,

    /// <summary>A game or replay is already running.</summary>
    GameRunning,

    /// <summary>Replays are switched off in the client.</summary>
    ReplaysDisabled,

    /// <summary>The replay file is gone.</summary>
    ReplayMissing,

    /// <summary>The client's replay folder already has a different file under this replay's name.</summary>
    NameTaken,

    /// <summary>Copying the replay into the client's replay folder failed.</summary>
    CopyFailed,

    /// <summary>A renamed file from a region whose platform is unknown, so it cannot be given a client name.</summary>
    UnknownPlatform,

    /// <summary>The client says it cannot play this replay, usually another patch.</summary>
    Incompatible,

    /// <summary>The client did not report the replay as ready in time.</summary>
    NotReady,

    /// <summary>The client's API answered with an error or not at all.</summary>
    ClientError,
}

/// <summary>
/// Plays a replay through the running League client. The game refuses to be started by anything
/// but Riot's client (Vanguard), see docs/decisions/0004. The client plays only files in its own
/// replay folder named <c>PLATFORM-GAMEID.rofl</c>, so a replay from elsewhere is copied there
/// first; a renamed file gets a stable made-up game id (the client does not check it).
/// </summary>
public sealed class ClientReplayWatcher(TimeProvider clock)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private const int MaxPolls = 20;

    // Riot platform ids for the client regions whose mapping is certain; used only for renamed files.
    private static readonly FrozenDictionary<string, string> PlatformByRegion = new Dictionary<string, string>
    {
        ["BR"] = "BR1",
        ["EUNE"] = "EUN1",
        ["EUW"] = "EUW1",
        ["JP"] = "JP1",
        ["KR"] = "KR",
        ["LA1"] = "LA1",
        ["LA2"] = "LA2",
        ["NA"] = "NA1",
        ["OC1"] = "OC1",
        ["RU"] = "RU",
        ["TR"] = "TR1",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads the lockfile of a running client, or <c>null</c> when the client is not running.</summary>
    public static ClientLockfile? FindRunningClient(LeagueInstallation installation)
    {
        var path = ClientLockfile.PathIn(installation);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            // The client keeps the lockfile open for writing while it runs; a plain
            // File.ReadAllText (FileShare.Read) fails with a sharing violation.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return ClientLockfile.Parse(reader.ReadToEnd());
        }
        catch (FileNotFoundException)
        {
            // The client exited between the check and the read.
            return null;
        }
    }

    /// <summary>
    /// Waits until the client is running, logged in and not patching (after it was started), and
    /// returns its lockfile; <c>null</c> after <paramref name="timeout"/>.
    /// </summary>
    public async Task<ClientLockfile?> WaitForReadyClientAsync(LeagueInstallation installation, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = clock.GetUtcNow() + timeout;
        while (clock.GetUtcNow() < deadline)
        {
            if (FindRunningClient(installation) is { } lockfile && await IsReadyAsync(lockfile, cancellationToken).ConfigureAwait(false))
            {
                return lockfile;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), clock, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// The name the replay gets in the client's folder: its own client name, or for a renamed file
    /// the client's platform and a 12-digit game id starting with 9, derived from the file's content,
    /// far above real game ids (10 digits in 2026). <c>null</c> when the platform is unknown.
    /// </summary>
    public static ReplayFileName? ClientNameFor(string replayPath, string? clientRegion)
    {
        if (ReplayFileName.TryParse(replayPath) is { } name)
        {
            return name;
        }

        if (clientRegion is null || !PlatformByRegion.TryGetValue(clientRegion, out var platform))
        {
            return null;
        }

        using var stream = File.OpenRead(replayPath);
        var hash = SHA256.HashData(stream);
        var digits = BitConverter.ToUInt64(hash, 0) % 100_000_000_000;
        return new ReplayFileName(platform, long.Parse("9" + digits.ToString("D11", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Copies the replay into the client's folder if needed, waits until the client lists it, and
    /// starts it. Returns the path the client plays.
    /// </summary>
    public async Task<Result<string, (WatchFailure Reason, string Detail)>> WatchAsync(
        LeagueClientApi client, string replayPath, GameVersion version, string? clientRegion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (!File.Exists(replayPath))
        {
            return Fail(WatchFailure.ReplayMissing, replayPath);
        }

        try
        {
            var status = await client.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            if (Blocker(status) is { } blocked)
            {
                return Fail(blocked, string.Empty);
            }

            if (ClientNameFor(replayPath, clientRegion) is not { } name)
            {
                return Fail(WatchFailure.UnknownPlatform, clientRegion ?? "unknown region");
            }

            var folder = await client.GetReplayFolderAsync(cancellationToken).ConfigureAwait(false);
            Result<string, string> placed;
            try
            {
                placed = PlaceInClientFolder(replayPath, folder, name);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return Fail(WatchFailure.CopyFailed, exception.Message);
            }

            if (!placed.TryGetValue(out var clientCopy, out var conflict))
            {
                return Fail(WatchFailure.NameTaken, conflict);
            }

            await client.ScanAsync(cancellationToken).ConfigureAwait(false);
            return await WaitAndWatchAsync(client, name.GameId, version, clientCopy, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            return Fail(WatchFailure.ClientError, exception.Message);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(WatchFailure.ClientError, $"timeout: {exception.Message}");
        }
    }

    /// <summary>
    /// The path of the replay inside the client folder, copying it there when it lives elsewhere.
    /// Fails with the conflicting path when a different file already has the name.
    /// </summary>
    public static Result<string, string> PlaceInClientFolder(string replayPath, string clientFolder, ReplayFileName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var target = Path.GetFullPath(Path.Combine(clientFolder, string.Create(CultureInfo.InvariantCulture, $"{name.PlatformId}-{name.GameId}.rofl")));
        var source = Path.GetFullPath(replayPath);
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            return Result<string, string>.Success(target);
        }

        if (File.Exists(target))
        {
            return SameContent(source, target) ? Result<string, string>.Success(target) : Result<string, string>.Failure(target);
        }

        Directory.CreateDirectory(clientFolder);
        File.Copy(source, target, overwrite: false);
        return Result<string, string>.Success(target);
    }

    private async Task<Result<string, (WatchFailure Reason, string Detail)>> WaitAndWatchAsync(
        LeagueClientApi client, long gameId, GameVersion version, string clientCopy, CancellationToken cancellationToken)
    {
        var created = false;
        string? state = null;
        for (var poll = 0; poll < MaxPolls; poll++)
        {
            state = await client.GetReplayStateAsync(gameId, cancellationToken).ConfigureAwait(false);
            switch (state)
            {
                case "watch":
                    await client.WatchAsync(gameId, cancellationToken).ConfigureAwait(false);
                    return Result<string, (WatchFailure, string)>.Success(clientCopy);
                case "incompatible" or "unsupported":
                    return Fail(WatchFailure.Incompatible, state);
                case null when !created:
                    // The scan did not register the file; register it the way match history does.
                    await client.CreateMetadataAsync(gameId, version, new FileInfo(clientCopy).LastWriteTimeUtc, cancellationToken).ConfigureAwait(false);
                    created = true;
                    break;
                default:
                    await Task.Delay(PollInterval, clock, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }

        return Fail(WatchFailure.NotReady, state ?? "no metadata");
    }

    private static async Task<bool> IsReadyAsync(ClientLockfile lockfile, CancellationToken cancellationToken)
    {
        using var client = new LeagueClientApi(lockfile);
        try
        {
            var status = await client.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            return status is { IsLoggedIn: true, IsPatching: false };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // A client that is still starting refuses connections; the caller polls again.
            return false;
        }
    }

    private static WatchFailure? Blocker(ClientStatus status) => status switch
    {
        { IsLoggedIn: false } => WatchFailure.NotLoggedIn,
        { IsPatching: true } => WatchFailure.Patching,
        { IsPlayingGame: true } => WatchFailure.GameRunning,
        { IsReplaysEnabled: false } => WatchFailure.ReplaysDisabled,
        _ => null,
    };

    private static bool SameContent(string first, string second)
    {
        if (new FileInfo(first).Length != new FileInfo(second).Length)
        {
            return false;
        }

        using var a = File.OpenRead(first);
        using var b = File.OpenRead(second);
        return SHA256.HashData(a).AsSpan().SequenceEqual(SHA256.HashData(b));
    }

    private static Result<string, (WatchFailure Reason, string Detail)> Fail(WatchFailure reason, string detail) =>
        Result<string, (WatchFailure, string)>.Failure((reason, detail));
}
