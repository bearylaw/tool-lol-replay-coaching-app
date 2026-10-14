using System.Diagnostics;
using RiftReview.App.Localization;
using RiftReview.App.Shell;
using RiftReview.Core.Logging;
using RiftReview.Launch;

namespace RiftReview.App.Capture;

/// <summary>What the capture and the overlay both need around a replay running in League.</summary>
internal sealed class ReplayGame(Dialogs dialogs, AppLog log, string backupFolder)
{
    /// <summary>
    /// Makes replays open in League's directed camera, as the coach asked (2026-10-14). The change
    /// is one line of game.cfg, so it is asked for once and a backup is kept. False when declined.
    /// </summary>
    public bool EnsureDirectedCamera(LeagueInstallation league)
    {
        var path = GameConfig.PathIn(league);
        try
        {
            var config = File.ReadAllText(path);
            if (GameConfig.IsDirectedCameraEnabled(config))
            {
                return true;
            }

            if (!dialogs.Confirm(Loc.Get("Overlay_EnableDirectedCamera")))
            {
                return false;
            }

            Directory.CreateDirectory(backupFolder);
            File.Copy(path, Path.Combine(backupFolder, $"game.cfg.{DateTime.Now:yyyyMMdd-HHmmss}.bak"));
            File.WriteAllText(path, GameConfig.WithDirectedCamera(config));
            log.Info("replay.directed_camera_enabled", ("path", path));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            log.Error("replay.game_cfg", exception, ("path", path));
            return false;
        }
    }

    /// <summary>
    /// Makes sure Riot's replay API is on. Switching it on changes one line of the coach's game.cfg,
    /// so it is asked for and a backup is kept. False when the coach declines or the file is unusable.
    /// </summary>
    public bool EnsureReplayApi(LeagueInstallation league)
    {
        var path = GameConfig.PathIn(league);
        try
        {
            var config = File.ReadAllText(path);
            if (GameConfig.IsReplayApiEnabled(config))
            {
                return true;
            }

            if (!dialogs.Confirm(Loc.Get("Capture_EnableReplayApi")))
            {
                return false;
            }

            Directory.CreateDirectory(backupFolder);
            File.Copy(path, Path.Combine(backupFolder, $"game.cfg.{DateTime.Now:yyyyMMdd-HHmmss}.bak"));
            File.WriteAllText(path, GameConfig.WithReplayApi(config));
            log.Info("replay.api_enabled", ("path", path));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            log.Error("replay.game_cfg", exception, ("path", path));
            dialogs.Error(Loc.Format("Capture_Failed", exception.Message));
            return false;
        }
    }

    /// <summary>
    /// Closes the replay window Rift Review opened (League game processes started after
    /// <paramref name="started"/>). The game is only ever started by the client; this ends that session.
    /// </summary>
    public void CloseReplayStartedAfter(DateTime started)
    {
        foreach (var process in Process.GetProcessesByName("League of Legends"))
        {
            using (process)
            {
                try
                {
                    if (process.StartTime >= started.AddSeconds(-5))
                    {
                        process.Kill();
                        log.Info("replay.closed", ("pid", process.Id));
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    log.Warning("replay.close_failed", exception, ("pid", process.Id));
                }
            }
        }
    }
}
