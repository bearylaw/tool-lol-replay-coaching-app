using System.Globalization;

namespace RiftReview.App.Replays;

/// <summary>A blue-versus-red total, for example team gold, drawn as one split bar.</summary>
internal sealed record TeamComparison(string Label, string BlueText, string RedText, double BlueShare)
{
    public static TeamComparison Of(string label, long blue, long red, Func<long, string>? format = null)
    {
        format ??= v => v.ToString(CultureInfo.CurrentCulture);
        var total = blue + red;
        return new TeamComparison(label, format(blue), format(red), total == 0 ? 0.5 : (double)blue / total);
    }
}

/// <summary>One player's bar in a metric chart.</summary>
internal sealed record PlayerBar(IconSlot Champion, string Name, string ValueText, double Share, bool IsBlue);

/// <summary>A metric across all players, for example damage to champions.</summary>
internal sealed record MetricChart(string Title, IReadOnlyList<PlayerBar> Bars);

/// <summary>A row of the full stats table: one Riot stat, one value per player.</summary>
internal sealed record StatRow(string Name, IReadOnlyList<string> Values);
