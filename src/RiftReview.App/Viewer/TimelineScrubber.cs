using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RiftReview.App.Viewer;

/// <summary>A mark on the scrubber, for example a death; drawn above the track for blue, below for red.</summary>
internal sealed record ScrubberMark(double Time, bool IsBlue);

/// <summary>What a highlighted mark of the selected player is.</summary>
internal enum FocusKind
{
    /// <summary>The player died; drawn on their team's side.</summary>
    Death,

    /// <summary>The player killed someone; drawn on the victim's side.</summary>
    Kill,

    /// <summary>The player assisted a kill; drawn on the victim's side.</summary>
    Assist,

    /// <summary>The player took part in destroying a turret; drawn where the turret is.</summary>
    Turret,
}

/// <summary>A mark of the selected player; <paramref name="IsBlue"/> is the side it is drawn on.</summary>
internal sealed record FocusMark(double Time, FocusKind Kind, bool IsBlue);

/// <summary>A bookmark on the scrubber, in its tag's colour.</summary>
internal sealed record BookmarkMark(double Time, Brush Colour);

/// <summary>An objective taken, on the track itself in the colour of the team that took it.</summary>
/// <param name="Time">Game time in seconds.</param>
/// <param name="Letter">The objective's initial, for example D for a dragon.</param>
/// <param name="IsBlue">Blue took it.</param>
internal sealed record ObjectiveMark(double Time, string Letter, bool IsBlue);

/// <summary>
/// The playback bar: track, played part, event marks and a handle. Click or drag anywhere to seek;
/// <see cref="Position"/> binds two-way.
/// </summary>
internal sealed class TimelineScrubber : FrameworkElement
{
    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
        nameof(Duration), typeof(double), typeof(TimelineScrubber), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
        nameof(Position), typeof(double), typeof(TimelineScrubber),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty MarksProperty = DependencyProperty.Register(
        nameof(Marks), typeof(IReadOnlyList<ScrubberMark>), typeof(TimelineScrubber), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TurretsProperty = DependencyProperty.Register(
        nameof(Turrets), typeof(IReadOnlyList<ScrubberMark>), typeof(TimelineScrubber), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BookmarksProperty = DependencyProperty.Register(
        nameof(Bookmarks), typeof(IReadOnlyList<BookmarkMark>), typeof(TimelineScrubber), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FocusMarksProperty = DependencyProperty.Register(
        nameof(FocusMarks), typeof(IReadOnlyList<FocusMark>), typeof(TimelineScrubber), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ObjectivesProperty = DependencyProperty.Register(
        nameof(Objectives), typeof(IReadOnlyList<ObjectiveMark>), typeof(TimelineScrubber), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double Inset = 8;

    // While a player is selected, everyone else's deaths step back to this opacity.
    private const double Dimmed = 0.3;
    private const double TrackHeight = 6;

    private static readonly Brush Track = Frozen(Color.FromRgb(0x24, 0x32, 0x46));
    private static readonly Brush Played = Frozen(Color.FromRgb(0xC8, 0xAA, 0x6E));
    private static readonly Brush Handle = Frozen(Color.FromRgb(0xF0, 0xD9, 0xA4));
    private static readonly Brush Blue = Frozen(Color.FromRgb(0x3D, 0x9B, 0xFF));
    private static readonly Brush Red = Frozen(Color.FromRgb(0xE8, 0x50, 0x5B));
    private static readonly Brush Gold = Frozen(Color.FromRgb(0xF0, 0xD9, 0xA4));
    private static readonly Pen TowerOutline = FrozenPen(Color.FromRgb(0xF4, 0xF6, 0xFA), 1.2);
    private static readonly Pen FocusOutline = FrozenPen(Color.FromRgb(0xF0, 0xD9, 0xA4), 1.2);
    private static readonly Pen FocusTower = FrozenPen(Color.FromRgb(0xF0, 0xD9, 0xA4), 2.2);
    private static readonly Pen ObjectiveOutline = FrozenPen(Color.FromRgb(0x0B, 0x10, 0x18), 1.5);

    public TimelineScrubber()
    {
        Cursor = Cursors.Hand;
        Focusable = false;
    }

    public double Duration
    {
        get => (double)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public double Position
    {
        get => (double)GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public IReadOnlyList<ScrubberMark>? Marks
    {
        get => (IReadOnlyList<ScrubberMark>?)GetValue(MarksProperty);
        set => SetValue(MarksProperty, value);
    }

    /// <summary>
    /// Turrets destroyed, as small towers in the colour of the team that lost them, on that team's
    /// side of the bar like the deaths (blue above, red below).
    /// </summary>
    public IReadOnlyList<ScrubberMark>? Turrets
    {
        get => (IReadOnlyList<ScrubberMark>?)GetValue(TurretsProperty);
        set => SetValue(TurretsProperty, value);
    }

    /// <summary>
    /// The selected player's deaths: drawn tall with a light outline while the other deaths dim, so
    /// one player's game reads at a glance.
    /// </summary>
    public IReadOnlyList<FocusMark>? FocusMarks
    {
        get => (IReadOnlyList<FocusMark>?)GetValue(FocusMarksProperty);
        set => SetValue(FocusMarksProperty, value);
    }

    /// <summary>Dragons, barons, heralds and the finished grub camp, as lettered dots on the track.</summary>
    public IReadOnlyList<ObjectiveMark>? Objectives
    {
        get => (IReadOnlyList<ObjectiveMark>?)GetValue(ObjectivesProperty);
        set => SetValue(ObjectivesProperty, value);
    }

    /// <summary>The coach's bookmarks, drawn as tall marks with a dot on top in their tag's colour.</summary>
    public IReadOnlyList<BookmarkMark>? Bookmarks
    {
        get => (IReadOnlyList<BookmarkMark>?)GetValue(BookmarksProperty);
        set => SetValue(BookmarksProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width, 36);

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var middle = ActualHeight / 2;
        var width = Math.Max(1, ActualWidth - (2 * Inset));
        drawingContext.DrawRoundedRectangle(Track, null, new Rect(Inset, middle - (TrackHeight / 2), width, TrackHeight), 3, 3);
        drawingContext.DrawRoundedRectangle(Played, null, new Rect(Inset, middle - (TrackHeight / 2), width * Share(Position), TrackHeight), 3, 3);

        DrawEvents(drawingContext, width, middle);
        DrawFocus(drawingContext, width, middle);
        DrawObjectives(drawingContext, width, middle);

        foreach (var mark in Bookmarks ?? [])
        {
            var x = Inset + (width * Share(mark.Time));
            drawingContext.DrawRectangle(mark.Colour, null, new Rect(x - 1, middle - 16, 2, 32));
            drawingContext.DrawEllipse(mark.Colour, null, new Point(x, middle - 16), 3, 3);
        }

        drawingContext.DrawEllipse(Handle, null, new Point(Inset + (width * Share(Position)), middle), 7, 7);
    }

    // Deaths and turrets of everyone; they step back while a player is selected.
    private void DrawEvents(DrawingContext context, double width, double middle)
    {
        context.PushOpacity(FocusMarks is { Count: > 0 } ? Dimmed : 1);
        foreach (var mark in Marks ?? [])
        {
            var x = Inset + (width * Share(mark.Time));
            var top = mark.IsBlue ? middle - 15 : middle + 6;
            context.DrawRectangle(mark.IsBlue ? Blue : Red, null, new Rect(x - 1, top, 2, 9));
        }

        foreach (var turret in Turrets ?? [])
        {
            DrawTower(context, Inset + (width * Share(turret.Time)), turret.IsBlue ? middle - 18 : middle + 5, turret.IsBlue ? Blue : Red, TowerOutline);
        }

        context.Pop();
    }

    private void DrawFocus(DrawingContext context, double width, double middle)
    {
        foreach (var mark in FocusMarks ?? [])
        {
            var x = Inset + (width * Share(mark.Time));
            var top = mark.IsBlue ? middle - 18 : middle + 4;
            var side = mark.IsBlue ? Blue : Red;
            switch (mark.Kind)
            {
                case FocusKind.Death:
                    context.DrawRectangle(side, FocusOutline, new Rect(x - 2, top, 4, 14));
                    break;
                case FocusKind.Kill:
                    context.DrawRectangle(Gold, null, new Rect(x - 2, top, 4, 14));
                    break;
                case FocusKind.Assist:
                    context.DrawRectangle(null, FocusOutline, new Rect(x - 2, top, 4, 14));
                    break;
                default:
                    DrawTower(context, x, mark.IsBlue ? middle - 18 : middle + 5, side, FocusTower);
                    break;
            }
        }
    }

    private void DrawObjectives(DrawingContext context, double width, double middle)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach (var objective in Objectives ?? [])
        {
            var centre = new Point(Inset + (width * Share(objective.Time)), middle);
            context.DrawEllipse(objective.IsBlue ? Blue : Red, ObjectiveOutline, centre, 6.5, 6.5);
            var letter = new FormattedText(objective.Letter, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, LetterFace, 9, Brushes.White, dpi);
            context.DrawText(letter, new Point(centre.X - (letter.Width / 2), centre.Y - (letter.Height / 2)));
        }
    }

    private static readonly Typeface LetterFace = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    // A tower 11 wide and 13 high from (x, top): battlements on a body, with a light outline so it
    // reads as an icon next to the thin death ticks.
    private static void DrawTower(DrawingContext context, double x, double top, Brush fill, Pen outline)
    {
        var tower = new StreamGeometry();
        using (var g = tower.Open())
        {
            g.BeginFigure(new Point(x - 4, top + 13), isFilled: true, isClosed: true);
            g.PolyLineTo(
                [
                    new Point(x - 4, top + 4), new Point(x - 5.5, top + 4), new Point(x - 5.5, top), new Point(x - 3, top),
                    new Point(x - 3, top + 2), new Point(x - 1, top + 2), new Point(x - 1, top), new Point(x + 1, top),
                    new Point(x + 1, top + 2), new Point(x + 3, top + 2), new Point(x + 3, top), new Point(x + 5.5, top),
                    new Point(x + 5.5, top + 4), new Point(x + 4, top + 4), new Point(x + 4, top + 13),
                ],
                isStroked: true,
                isSmoothJoin: false);
        }

        tower.Freeze();
        context.DrawGeometry(fill, outline, tower);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        CaptureMouse();
        Seek(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (IsMouseCaptured)
        {
            Seek(e.GetPosition(this).X);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        base.OnMouseLeftButtonUp(e);
    }

    private void Seek(double x)
    {
        var width = Math.Max(1, ActualWidth - (2 * Inset));
        Position = Math.Clamp((x - Inset) / width, 0, 1) * Duration;
    }

    private double Share(double time) => Duration <= 0 ? 0 : Math.Clamp(time / Duration, 0, 1);

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Color colour, double thickness)
    {
        var pen = new Pen(new SolidColorBrush(colour), thickness);
        pen.Freeze();
        return pen;
    }
}
