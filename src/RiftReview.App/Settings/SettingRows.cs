using System.ComponentModel;
using RiftReview.App.Mvvm;

namespace RiftReview.App.Settings;

/// <summary>One line in the settings window; the window picks a template by the row's type.</summary>
internal abstract class SettingRow(string label, string hint) : ObservableObject
{
    public string Label { get; } = label;

    /// <summary>A sentence under the label explaining the effect; empty for none.</summary>
    public string Hint { get; } = hint;
}

/// <summary>A heading that groups the rows under it.</summary>
internal sealed class HeadingRow(string label) : SettingRow(label, string.Empty);

/// <summary>An on/off switch.</summary>
internal sealed class ToggleRow : SettingRow
{
    private bool isOn;

    public ToggleRow(string label, string hint, bool isOn)
        : base(label, hint) => this.isOn = isOn;

    public bool IsOn
    {
        get => isOn;
        set => SetProperty(ref isOn, value);
    }
}

/// <summary>
/// A number on a slider, snapped to <see cref="Step"/> and shown through a format. It is greyed
/// out while the switch it depends on is off.
/// </summary>
internal sealed class RangeRow : SettingRow
{
    private readonly Func<double, string> format;
    private readonly ToggleRow? dependsOn;
    private double value;

    public RangeRow(string label, string hint, double value, (double Minimum, double Maximum, double Step) range, Func<double, string> format, ToggleRow? dependsOn = null)
        : base(label, hint)
    {
        (Minimum, Maximum, Step) = range;
        this.format = format;
        this.dependsOn = dependsOn;
        this.value = Snap(value);
        if (dependsOn is not null)
        {
            dependsOn.PropertyChanged += OnDependencyChanged;
        }
    }

    public double Minimum { get; }

    public double Maximum { get; }

    public double Step { get; }

    public double Value
    {
        get => value;
        set
        {
            if (SetProperty(ref this.value, Snap(value)))
            {
                OnPropertyChanged(nameof(Text));
            }
        }
    }

    public string Text => format(value);

    public bool IsEnabled => dependsOn?.IsOn ?? true;

    private double Snap(double raw) => Math.Clamp(Math.Round(raw / Step) * Step, Minimum, Maximum);

    private void OnDependencyChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(IsEnabled));
}

/// <summary>One of a few fixed values, shown as chips.</summary>
internal sealed class ChoiceRow : SettingRow
{
    public ChoiceRow(string label, string hint, IEnumerable<(string Label, double Value)> options, double selected)
        : base(label, hint)
    {
        Options = [.. options.Select(o => new Choice(this, o.Label, o.Value))];
        Select(Options.MinBy(o => Math.Abs(o.Value - selected))!);
    }

    public IReadOnlyList<Choice> Options { get; }

    public double Value => Options.First(o => o.IsSelected).Value;

    // Chips are not grouped by WPF (each sits in its own container), so the row keeps one selected.
    internal void Select(Choice choice)
    {
        foreach (var option in Options)
        {
            option.SetSelected(ReferenceEquals(option, choice));
        }
    }
}

/// <summary>A chip of a <see cref="ChoiceRow"/>.</summary>
internal sealed class Choice(ChoiceRow row, string label, double value) : ObservableObject
{
    private bool isSelected;

    public string Label { get; } = label;

    public double Value { get; } = value;

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (value)
            {
                row.Select(this);
            }
        }
    }

    internal void SetSelected(bool selected) => SetProperty(ref isSelected, selected, nameof(IsSelected));
}
