using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using RiftReview.Core.Matches;
using RiftReview.Launch;

namespace RiftReview.App.Theme;

/// <summary>Visible when the value is true, a non-empty string, a non-zero number or any other non-null object; <c>Invert=True</c> flips it.</summary>
internal sealed class VisibleWhenConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            null => false,
            bool flag => flag,
            string text => text.Length > 0,
            int number => number != 0,
            _ => true,
        };
        return visible ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Turns a share between 0 and 1 into a star width; with <c>ConverterParameter=Rest</c>, into the remainder.</summary>
internal sealed class ShareToStarConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var share = Math.Clamp(value is double d ? d : 0, 0, 1);
        var star = parameter as string == "Rest" ? 1 - share : share;
        return new GridLength(Math.Max(star, 0.0001), GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Picks the blue or red team brush from a bool (IsBlue) or a <see cref="Team"/>.</summary>
internal sealed class TeamBrushConverter : IValueConverter
{
    public Brush Blue { get; set; } = Brushes.SteelBlue;

    public Brush Red { get; set; } = Brushes.IndianRed;

    public Brush Neutral { get; set; } = Brushes.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        true or Team.Blue => Blue,
        false or Team.Red => Red,
        _ => Neutral,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Green for a replay that plays on the installed game, amber for maybe, red for no, grey for unknown.</summary>
internal sealed class CompatibilityBrushConverter : IValueConverter
{
    public Brush Compatible { get; set; } = Brushes.SeaGreen;

    public Brush Maybe { get; set; } = Brushes.Goldenrod;

    public Brush Incompatible { get; set; } = Brushes.IndianRed;

    public Brush Unknown { get; set; } = Brushes.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        Compatibility.Compatible => Compatible,
        Compatibility.DifferentBuild => Maybe,
        Compatibility.DifferentPatch => Incompatible,
        _ => Unknown,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when the bound value equals the parameter's text, for filter chips bound to an enum.</summary>
internal sealed class EqualsParameterConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
