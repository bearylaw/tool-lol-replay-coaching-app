using RiftReview.App.Localization;
using RiftReview.App.Mvvm;
using RiftReview.App.Replays;
using RiftReview.Core.Matches;
using RiftReview.Core.Timelines;

namespace RiftReview.App.Timelines;

/// <summary>One line of the event list.</summary>
internal sealed record TimelineRow(string Time, IconSlot Champion, string Player, string Text, bool IsBlue, bool IsDeath);

/// <summary>The Timeline tab: loading state, then the events read from the replay file.</summary>
internal sealed class TimelineViewModel : ObservableObject
{
    private IReadOnlyList<TimelineRow> allRows = [];
    private bool isLoading = true;
    private string? error;
    private bool showLevelUps = true;

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public string? Error
    {
        get => error;
        private set => SetProperty(ref error, value);
    }

    /// <summary>Level-ups make the list long; the coach can hide them to see deaths only.</summary>
    public bool ShowLevelUps
    {
        get => showLevelUps;
        set
        {
            if (SetProperty(ref showLevelUps, value))
            {
                OnPropertyChanged(nameof(Rows));
            }
        }
    }

    public IReadOnlyList<TimelineRow> Rows => showLevelUps ? allRows : [.. allRows.Where(r => r.IsDeath)];

    public bool IsEmpty => !IsLoading && Error is null && allRows.Count == 0;

    /// <summary>Shows a loaded timeline; champions map to the scoreboard rows by index.</summary>
    public void Show(MatchTimeline timeline, IReadOnlyList<PlayerRowViewModel> players)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        allRows = [.. timeline.Events
            .Where(e => e.Champion >= 0 && e.Champion < players.Count && e.Kind is TimelineEventKind.Death or TimelineEventKind.LevelUp)
            .Select(e => Row(e, players[e.Champion]))];
        IsLoading = false;
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(IsEmpty));
    }

    public void Fail(string reason)
    {
        Error = Loc.Format("Timeline_Failed", reason);
        IsLoading = false;
        OnPropertyChanged(nameof(IsEmpty));
    }

    private static TimelineRow Row(TimelineEvent e, PlayerRowViewModel player) => new(
        MatchFormat.Duration(TimeSpan.FromSeconds(e.Time)),
        player.Champion,
        player.Stats.GameName,
        e.Kind == TimelineEventKind.Death ? Loc.Get("Timeline_Died") : Loc.Format("Timeline_LevelUp", e.Value),
        player.Stats.Team == Team.Blue,
        e.Kind == TimelineEventKind.Death);
}
