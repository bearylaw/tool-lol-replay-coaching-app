using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RiftReview.App.Overlay;

/// <summary>
/// Where the coach draws on the overlay: freehand pen and highlighter, line, arrow, circle and
/// rectangle, an eraser, undo (also of a clear; right-click undoes too) and optional fading. While
/// draw mode is off it is transparent to the mouse, so clicks reach the game.
/// </summary>
internal sealed class DrawingLayer : InkCanvas
{
    private static readonly Brush Catcher = Frozen(Color.FromArgb(1, 0, 0, 0));

    private readonly Stack<(StrokeCollection Added, StrokeCollection Removed)> history = new();
    private readonly Dictionary<Stroke, DateTime> drawnAt = [];
    private readonly DispatcherTimer fade = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private DrawingState? state;
    private Func<double> fadeSeconds = () => 0;
    private Action started = () => { };
    private Point? shapeStart;
    private StrokeCollection preview = [];

    public DrawingLayer()
    {
        Background = null;
        IsHitTestVisible = false;
        UseCustomCursor = true;
        Cursor = Cursors.Pen;
        StrokeCollected += (_, e) => Record([e.Stroke], []);
        StrokeErasing += (_, e) => history.Push(([], [e.Stroke]));
        fade.Tick += (_, _) => FadeOld();
    }

    /// <summary>Connects the layer to the tools; <paramref name="onStarted"/> runs when a drawing begins.</summary>
    public void Attach(DrawingState drawing, Func<double> fadeAfterSeconds, Action onStarted)
    {
        state = drawing;
        fadeSeconds = fadeAfterSeconds;
        started = onStarted;
        drawing.PropertyChanged += OnStateChanged;
        drawing.UndoRequested += (_, _) => Undo();
        drawing.ClearRequested += (_, _) => Clear();
        Apply();
        fade.Start();
    }

    /// <summary>Removes every drawing; undo brings them back.</summary>
    public void Clear()
    {
        if (Strokes.Count > 0)
        {
            var all = Strokes.Clone();
            Strokes.Clear();
            history.Push(([], all));
        }
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.ChangedButton == MouseButton.Right)
        {
            Undo();
            e.Handled = true;
            return;
        }

        started();
        if (state is { Tool: DrawTool.Line or DrawTool.Arrow or DrawTool.Circle })
        {
            shapeStart = e.GetPosition(this);
            CaptureMouse();
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseDown(e);
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (shapeStart is { } start && state is not null)
        {
            Strokes.Remove(preview);
            preview = Shape(start, e.GetPosition(this));
            Strokes.Add(preview);
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseMove(e);
    }

    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (shapeStart is not null)
        {
            ReleaseMouseCapture();
            shapeStart = null;
            Record(preview, []);
            preview = [];
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseUp(e);
    }

    private StrokeCollection Shape(Point start, Point end)
    {
        var attributes = Attributes(state!);
        return new StrokeCollection(DrawingShapes.For(state!.Tool, start, end, state.Thickness)
            .Where(points => points.Count > 1)
            .Select(points => new Stroke(new StylusPointCollection(points), attributes.Clone())));
    }

    private void Undo()
    {
        if (history.Count == 0)
        {
            return;
        }

        var (added, removed) = history.Pop();
        Strokes.Remove(new StrokeCollection(added.Where(Strokes.Contains)));
        Strokes.Add(new StrokeCollection(removed.Where(s => !Strokes.Contains(s))));
    }

    private void Record(IEnumerable<Stroke> added, IEnumerable<Stroke> removed)
    {
        var addedStrokes = new StrokeCollection(added);
        foreach (var stroke in addedStrokes)
        {
            drawnAt[stroke] = DateTime.UtcNow;
        }

        history.Push((addedStrokes, new StrokeCollection(removed)));
    }

    private void FadeOld()
    {
        var seconds = fadeSeconds();
        if (seconds <= 0 || drawnAt.Count == 0)
        {
            return;
        }

        var old = drawnAt.Where(d => (DateTime.UtcNow - d.Value).TotalSeconds > seconds).Select(d => d.Key).ToList();
        foreach (var stroke in old)
        {
            drawnAt.Remove(stroke);
            Strokes.Remove(stroke);
        }
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e) => Apply();

    private void Apply()
    {
        if (state is null)
        {
            return;
        }

        IsHitTestVisible = state.TakesMouse;
        Background = state.TakesMouse ? Catcher : null;
        EditingMode = state.Tool switch
        {
            DrawTool.Pen or DrawTool.Highlighter => InkCanvasEditingMode.Ink,
            DrawTool.Eraser => InkCanvasEditingMode.EraseByStroke,
            _ => InkCanvasEditingMode.None,
        };
        // Windows' pen cursor is too small to see over the game.
        Cursor = state.Tool == DrawTool.Eraser ? DrawCursors.Eraser() : DrawCursors.Pen(state.Colour.Colour);
        DefaultDrawingAttributes = Attributes(state);
    }

    private static DrawingAttributes Attributes(DrawingState state)
    {
        var highlighter = state.Tool == DrawTool.Highlighter;
        var colour = state.Colour.Colour;
        return new DrawingAttributes
        {
            Color = highlighter ? Color.FromArgb(0x70, colour.R, colour.G, colour.B) : colour,
            Width = highlighter ? state.Thickness * 3 : state.Thickness,
            Height = highlighter ? state.Thickness * 3 : state.Thickness,
            FitToCurve = true,
            IgnorePressure = true,
            StylusTip = StylusTip.Ellipse,
        };
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
