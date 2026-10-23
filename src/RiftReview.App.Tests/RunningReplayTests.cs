using RiftReview.App.Overlay;
using RiftReview.Core.Matches;

namespace RiftReview.App.Tests;

public sealed class RunningReplayTests
{
    private static readonly string[] Ten = ["Ahri", "Bard", "Cait", "Darius", "Ekko", "Fiora", "Garen", "Hecarim", "Irelia", "Jax"];

    [Fact]
    public void TheReplayWithTheSameLengthAndPlayersIsFound()
    {
        var other = Match(1800, Ten.Select(n => n + "X"));
        var match = Match(1805, Ten);
        var running = new RunningReplay(1812, [.. Ten.Select(n => n + "#EUW")]);

        Assert.Same(match, running.FindIn([other, match], m => m));
    }

    [Fact]
    public void PlayersDoNotMakeUpForAnotherLength()
    {
        var running = new RunningReplay(1500, Ten);

        Assert.Null(running.FindIn([Match(1800, Ten)], m => m));
    }

    [Fact]
    public void FewerThanHalfTheRunningPlayersIsAnotherGame()
    {
        var running = new RunningReplay(1800, Ten);

        Assert.Null(running.FindIn([Match(1800, Ten.Take(4))], m => m));
        Assert.NotNull(running.FindIn([Match(1800, Ten.Take(5))], m => m));
    }

    [Fact]
    public void NamesAreComparedWithoutTagOrCase()
    {
        var running = new RunningReplay(1800, [.. Ten.Select(n => n.ToUpperInvariant() + "#1234")]);

        Assert.NotNull(running.FindIn([Match(1800, Ten)], m => m));
    }

    [Fact]
    public void WithoutPlayersOnlyAUniqueLengthDecides()
    {
        var running = new RunningReplay(1800, []);
        var one = Match(1800, Ten);

        Assert.Same(one, running.FindIn([one, Match(2400, Ten)], m => m));
        Assert.Null(running.FindIn([one, Match(1810, Ten)], m => m));
    }

    [Fact]
    public void UnreadableReplaysAreSkipped()
    {
        var running = new RunningReplay(1800, Ten);

        Assert.Null(running.FindIn<ReplayMetadata>([Match(1800, Ten)], _ => null));
    }

    private static ReplayMetadata Match(double seconds, IEnumerable<string> names) => new()
    {
        Version = new GameVersion(16, 20, 0, 0),
        GameLength = TimeSpan.FromSeconds(seconds),
        Players = [.. names.Select(n => new PlayerStats
        {
            GameName = n,
            TagLine = string.Empty,
            Champion = "Annie",
            Team = Team.Blue,
            Position = Position.None,
            Won = true,
        })],
    };
}
