using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RiftReview.App.Viewer;

/// <summary>A mark on the scrubber, for example a death; drawn above the track for blue, below for red.</summary>
internal sealed record ScrubberMark(double Time, bool IsBlue);

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
        nameof(Bookmarks), typeof(IReadOnlyList<double>), typeof(TimelineScrubber), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double Inset = 8;
    private const double TrackHeight = 6;

    private static readonly Brush Track = Frozen(Color.FromRgb(0x24, 0x32, 0x46));
    private static readonly Brush Played = Frozen(Color.FromRgb(0xC8, 0xAA, 0x6E));
    private static readonly Brush Handle = Frozen(Color.FromRgb(0xF0, 0xD9, 0xA4));
    private static readonly Brush Blue = Frozen(Color.FromRgb(0x3D, 0x9B, 0xFF));
    private static readonly Brush Red = Frozen(Color.FromRgb(0xE8, 0x50, 0x5B));
    private static readonly Brush Bookmark = Frozen(Color.FromRgb(0xF0, 0xD9, 0xA4));
    private static readonly Pen TowerOutline = FrozenPen(Color.FromRgb(0xF4, 0xF6, 0xFA), 1.2);

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

    /// <summary>Times of the coach's bookmarks, drawn as tall gold marks with a dot on top.</summary>
    public IReadOnlyList<double>? Bookmarks
    {
        get => (IReadOnlyList<double>?)GetValue(BookmarksProperty);
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

        foreach (var mark in Marks ?? [])
        {
            var x = Inset + (width * Share(mark.Time));
            var top = mark.IsBlue ? middle - 15 : middle + 6;
            drawingContext.DrawRectangle(mark.IsBlue ? Blue : Red, null, new Rect(x - 1, top, 2, 9));
        }

        foreach (var turret in Turrets ?? [])
        {
            DrawTower(drawingContext, Inset + (width * Share(turret.Time)), turret.IsBlue ? middle - 18 : middle + 5, turret.IsBlue ? Blue : Red);
        }

        foreach (var time in Bookmarks ?? [])
        {
            var x = Inset + (width * Share(time));
            drawingContext.DrawRectangle(Bookmark, null, new Rect(x - 1, middle - 16, 2, 32));
            drawingContext.DrawEllipse(Bookmark, null, new Point(x, middle - 16), 3, 3);
        }

        drawingContext.DrawEllipse(Handle, null, new Point(Inset + (width * Share(Position)), middle), 7, 7);
    }

    // A tower 11 wide and 13 high from (x, top): battlements on a body, with a light outline so it
    // reads as an icon next to the thin death ticks.
    private static void DrawTower(DrawingContext context, double x, double top, Brush fill)
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
        context.DrawGeometry(fill, TowerOutline, tower);
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
