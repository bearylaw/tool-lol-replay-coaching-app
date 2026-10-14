using System.Text.Json;
using System.Text.Json.Serialization;
using RiftReview.Core.Matches;
using RiftReview.Core.Results;

namespace RiftReview.Core.Timelines;

/// <summary>
/// Reads and writes <see cref="MatchTimeline"/> as JSON with a schema number. The file format is
/// independent of the C# types, so the model can change without breaking stored timelines; fields
/// this version does not know are ignored.
/// </summary>
public static class TimelineJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Serializes a timeline with the current schema number.</summary>
    public static string Write(MatchTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        var file = new TimelineFile(
            MatchTimeline.CurrentSchema,
            timeline.GameVersion.ToString(),
            timeline.GameLength.TotalSeconds,
            [.. timeline.Champions.Select(c => new ChampionEntry(c.Index, c.NetworkId, c.RiotId, c.Champion, c.Team))],
            [.. timeline.Events.Select(e => new EventEntry(Math.Round(e.Time, 3), e.Kind, e.Champion, e.Value))],
            [.. timeline.ActivityPerMinute.Select(row => row.ToArray())],
            timeline.BuilderVersion,
            timeline.TurretFalls.Count == 0 ? null : [.. timeline.TurretFalls.Select(t => t with { Time = Math.Round(t.Time, 2) })],
            timeline.Objectives.Count == 0 ? null : [.. timeline.Objectives.Select(o => o with { Time = Math.Round(o.Time, 2) })]);
        return JsonSerializer.Serialize(file, Options);
    }

    /// <summary>Parses a stored timeline; fails with a reason for malformed files or a newer schema.</summary>
    public static Result<MatchTimeline, string> Read(string json)
    {
        try
        {
            var file = JsonSerializer.Deserialize<TimelineFile>(json, Options);
            if (file is null)
            {
                return Result<MatchTimeline, string>.Failure("timeline file is empty");
            }

            if (file.Schema != MatchTimeline.CurrentSchema)
            {
                return Result<MatchTimeline, string>.Failure($"timeline schema {file.Schema}, this app reads {MatchTimeline.CurrentSchema}");
            }

            if (GameVersion.TryParse(file.GameVersion) is not { } version)
            {
                return Result<MatchTimeline, string>.Failure($"timeline game version \"{file.GameVersion}\" is not a version");
            }

            return Result<MatchTimeline, string>.Success(new MatchTimeline
            {
                GameVersion = version,
                GameLength = TimeSpan.FromSeconds(file.GameLengthSeconds),
                Champions = [.. (file.Champions ?? []).Select(c => new TimelineChampion(c.Index, c.NetworkId, c.RiotId, c.Champion, c.Team))],
                Events = [.. (file.Events ?? []).Where(e => Enum.IsDefined(e.Kind)).Select(e => new TimelineEvent(e.Time, e.Kind, e.Champion, e.Value))],
                ActivityPerMinute = [.. (file.ActivityPerMinute ?? []).Select(row => (IReadOnlyList<int>)row)],
                BuilderVersion = file.BuilderVersion,
                TurretFalls = file.TurretFalls ?? [],
                Objectives = [.. (file.Objectives ?? []).Where(o => Enum.IsDefined(o.Kind)).Select(o => o with { Participants = o.Participants ?? [] })],
            });
        }
        catch (JsonException exception)
        {
            return Result<MatchTimeline, string>.Failure($"timeline is not valid JSON: {exception.Message}");
        }
    }

    private sealed record TimelineFile(
        int Schema,
        string GameVersion,
        double GameLengthSeconds,
        ChampionEntry[]? Champions,
        EventEntry[]? Events,
        int[][]? ActivityPerMinute,
        int BuilderVersion = 0,
        TurretFall[]? TurretFalls = null,
        ObjectiveKill[]? Objectives = null);

    private sealed record ChampionEntry(int Index, uint NetworkId, string RiotId, string Champion, Team Team);

    private sealed record EventEntry(double Time, TimelineEventKind Kind, int Champion, int Value);
}
