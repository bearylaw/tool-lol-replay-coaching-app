using System.Diagnostics;
using RiftReview.Core.Matches;
using RiftReview.Core.Rofl;
using RiftReview.Core.Timelines;

namespace RiftReview.Timeline.Tests;

/// <summary>
/// Builds timelines for the real replays in RIFTREVIEW_REPLAY_DIR (skipped otherwise, since real
/// replays hold player data); run with <c>just verify-replays "...\Replays"</c> after a patch.
/// </summary>
public sealed class RealReplayTimelineTests
{
    [Fact]
    public void DeathsAndLevelUpsMatchTheEndOfGameStats()
    {
        var folder = Environment.GetEnvironmentVariable("RIFTREVIEW_REPLAY_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder), "RIFTREVIEW_REPLAY_DIR is not set");

        foreach (var path in Directory.GetFiles(folder!, "*.rofl"))
        {
            using var stream = File.OpenRead(path);
            Assert.True(RoflReader.Read(stream).TryGetValue(out var metadata, out var error), $"{path}: {error?.Detail}");
            var watch = Stopwatch.StartNew();
            var result = TimelineBuilder.Build(stream, metadata);
            Assert.True(result.TryGetValue(out var timeline, out var failure), $"{path}: {failure}");

            var name = Path.GetFileName(path);
            if (metadata.Players.Count == 0)
            {
                Assert.Empty(timeline.Events);
                TestContext.Current.SendDiagnosticMessage($"{name}: no players, no events");
                continue;
            }

            var deaths = Count(timeline, TimelineEventKind.Death);
            var respawns = Count(timeline, TimelineEventKind.Respawn);
            var levels = Count(timeline, TimelineEventKind.LevelUp);
            TestContext.Current.SendDiagnosticMessage(
                $"{name}: {watch.ElapsedMilliseconds} ms; deaths [{string.Join(",", deaths)}] vs [{string.Join(",", metadata.Players.Select(p => p.Deaths))}]; respawns [{string.Join(",", respawns)}]; level-ups [{string.Join(",", levels)}] vs [{string.Join(",", metadata.Players.Select(p => p.Level - 1))}]");
            // Every respawn follows a death of the same champion by a plausible respawn time.
            foreach (var respawn in timeline.Events.Where(e => e.Kind == TimelineEventKind.Respawn))
            {
                var died = timeline.Events.LastOrDefault(e => e.Kind == TimelineEventKind.Death && e.Champion == respawn.Champion && e.Time < respawn.Time);
                Assert.True(died is not null && respawn.Time - died.Time is >= 3 and <= 120, $"{name}: respawn at {respawn.Time:0.0} of champion {respawn.Champion} has no death 3-120 s before");
            }

            Assert.Equal(metadata.Players.Count, timeline.Champions.Count);
            for (var i = 0; i < metadata.Players.Count; i++)
            {
                var player = metadata.Players[i];
                // From packet timing alone a death without a clear respawn is left out: on the 16.20
                // replays 294 of 298 deaths are found, never more than two short per player.
                Assert.InRange(deaths[i], player.Deaths - 2, player.Deaths);
                // Level-up packets are approximate (docs/research/rofl2-payload.md): within two per champion.
                Assert.InRange(levels[i], player.Level - 3, player.Level + 1);
                Assert.True(timeline.Events.Where(e => e.Champion == i && e.Kind == TimelineEventKind.LevelUp).All(e => e.Value <= player.Level));
            }
        }
    }

    [Fact]
    public void TurretFallsMatchEachTeamsTurretsLost()
    {
        var folder = Environment.GetEnvironmentVariable("RIFTREVIEW_REPLAY_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder), "RIFTREVIEW_REPLAY_DIR is not set");

        foreach (var path in Directory.GetFiles(folder!, "*.rofl"))
        {
            using var stream = File.OpenRead(path);
            Assert.True(RoflReader.Read(stream).TryGetValue(out var metadata, out _));
            if (metadata.Players.Count == 0)
            {
                continue;
            }

            Assert.True(TimelineBuilder.Build(stream, metadata).TryGetValue(out var timeline, out var failure), failure);
            int Lost(Team team) => metadata.PlayersOn(team).Max(p => int.Parse(p.RawStats.GetValueOrDefault("FRIENDLY_TURRET_LOST", "0"), System.Globalization.CultureInfo.InvariantCulture));
            TestContext.Current.SendDiagnosticMessage($"{Path.GetFileName(path)}: {string.Join(" ", timeline.TurretFalls.Select(t => $"{TimeSpan.FromSeconds(t.Time):m\\:ss}{t.Owner}"))}");
            Assert.Equal(Lost(Team.Blue), timeline.TurretFalls.Count(t => t.Owner == Team.Blue));
            Assert.Equal(Lost(Team.Red), timeline.TurretFalls.Count(t => t.Owner == Team.Red));
        }
    }

    private static int[] Count(MatchTimeline timeline, TimelineEventKind kind) =>
        [.. timeline.Champions.Select(c => timeline.Events.Count(e => e.Kind == kind && e.Champion == c.Index))];
}
