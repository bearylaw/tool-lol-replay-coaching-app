using System.Globalization;
using RiftReview.App.Localization;
using RiftReview.Core.Matches;

namespace RiftReview.App.Replays;

/// <summary>How numbers from a match are written on screen.</summary>
internal static class MatchFormat
{
    /// <summary><c>47:53</c>, or <c>1:02:07</c> past an hour.</summary>
    public static string Duration(TimeSpan length) =>
        length.TotalHours >= 1
            ? length.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : length.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    /// <summary>(kills + assists) / deaths with two decimals, or "Perfect" without deaths.</summary>
    public static string KdaRatio(int kills, int deaths, int assists) =>
        deaths == 0
            ? Loc.Get("Kda_Perfect")
            : ((kills + assists) / (double)deaths).ToString("0.00", CultureInfo.CurrentCulture);

    /// <summary>Per-minute rate with one decimal; zero for an empty game.</summary>
    public static string PerMinute(double value, TimeSpan length) =>
        length.TotalMinutes <= 0 ? "0" : (value / length.TotalMinutes).ToString("0.0", CultureInfo.CurrentCulture);

    /// <summary><c>16.8k</c> above a thousand, else the plain number.</summary>
    public static string Thousands(long value) =>
        value >= 1000
            ? (value / 1000d).ToString("0.0", CultureInfo.CurrentCulture) + "k"
            : value.ToString(CultureInfo.CurrentCulture);

    /// <summary>The localized role name.</summary>
    public static string Role(Position position) => Loc.Get($"Role_{position}");
}
