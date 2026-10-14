using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using RiftReview.App.Mvvm;

namespace RiftReview.App.Overlay;

/// <summary>What the drawing tools draw.</summary>
internal enum DrawTool
{
    /// <summary>The normal mouse: the tools stay open and the drawings stay, but clicks reach the game.</summary>
    Pointer,

    Pen,
    Highlighter,
    Line,
    Arrow,
    Circle,
    Eraser,
}

/// <summary>A colour of the drawing palette.</summary>
internal sealed record DrawColour(string Name, Color Colour)
{
    public SolidColorBrush Brush { get; } = Frozen(Colour);

    private static SolidColorBrush Frozen(Color colour)
    {
        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// The coach's drawing tools on the overlay: on or off, tool, colour and thickness. Undo and clear
/// are handled by the drawing layer, which listens for the requests.
/// </summary>
internal sealed class DrawingState : ObservableObject
{
    private bool isOn;
    private DrawTool tool = DrawTool.Pen;
    private DrawColour colour;
    private double thickness = 5;

    public DrawingState()
    {
        Palette =
        [
            new DrawColour("Gold", Color.FromRgb(0xF0, 0xD9, 0xA4)),
            new DrawColour("Blue", Color.FromRgb(0x3D, 0x9B, 0xFF)),
            new DrawColour("Red", Color.FromRgb(0xE8, 0x50, 0x5B)),
            new DrawColour("Green", Color.FromRgb(0x3F, 0xBF, 0x8A)),
            new DrawColour("White", Colors.White),
            new DrawColour("Purple", Color.FromRgb(0xB0, 0x7C, 0xFF)),
        ];
        colour = Palette[0];
        ToggleCommand = new RelayCommand(() => IsOn = !IsOn);
        ToolCommand = new RelayCommand(p =>
        {
            Tool = Enum.Parse<DrawTool>((string)p!);
            IsOn = true;
        });
        ColourCommand = new RelayCommand(p => Colour = (DrawColour)p!);
        UndoCommand = new RelayCommand(() => UndoRequested?.Invoke(this, EventArgs.Empty));
        ClearCommand = new RelayCommand(() => ClearRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Raised to remove the last drawing.</summary>
    public event EventHandler? UndoRequested;

    /// <summary>Raised to remove all drawings.</summary>
    public event EventHandler? ClearRequested;

    public IReadOnlyList<DrawColour> Palette { get; }

    public ICommand ToggleCommand { get; }

    public ICommand ToolCommand { get; }

    public ICommand ColourCommand { get; }

    public ICommand UndoCommand { get; }

    public ICommand ClearCommand { get; }

    /// <summary>The drawing tools are open.</summary>
    public bool IsOn
    {
        get => isOn;
        set
        {
            if (SetProperty(ref isOn, value))
            {
                OnPropertyChanged(nameof(TakesMouse));
            }
        }
    }

    /// <summary>The overlay takes the mouse for drawing: tools open and a drawing tool chosen, not the pointer.</summary>
    public bool TakesMouse => IsOn && Tool != DrawTool.Pointer;

    public DrawTool Tool
    {
        get => tool;
        set
        {
            if (SetProperty(ref tool, value))
            {
                OnPropertyChanged(nameof(TakesMouse));
            }
        }
    }

    public DrawColour Colour
    {
        get => colour;
        set => SetProperty(ref colour, value);
    }

    /// <summary>Line width in pixels, 2–24.</summary>
    public double Thickness
    {
        get => thickness;
        set => SetProperty(ref thickness, Math.Clamp(value, 2, 24));
    }

    public void Undo() => UndoRequested?.Invoke(this, EventArgs.Empty);

    public void Clear() => ClearRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>Point lists for the shape tools, one list per stroke.</summary>
internal static class DrawingShapes
{
    private const int CirclePoints = 72;

    /// <summary>The strokes of a shape dragged from <paramref name="start"/> to <paramref name="end"/>.</summary>
    public static IReadOnlyList<IReadOnlyList<Point>> For(DrawTool tool, Point start, Point end, double thickness) => tool switch
    {
        DrawTool.Line => [[start, end]],
        DrawTool.Arrow => Arrow(start, end, thickness),
        DrawTool.Circle => [Circle(start, end)],
        _ => [],
    };

    // Head length: this share of the arrow, at least a size readable at the line width, never more
    // than half the arrow; its width is this share of its length.
    private const double HeadShare = 0.3;
    private const double MaxHeadShare = 0.5;
    private const double HeadWidth = 0.6;

    // A shaft and a filled head that grows with the arrow's length: the shaft first, then the head's
    // outline (back corner, tip, other back corner, closed), then lines from the tip that fill it.
    internal static IReadOnlyList<IReadOnlyList<Point>> Arrow(Point start, Point end, double thickness)
    {
        var direction = end - start;
        var length = direction.Length;
        if (length < 1)
        {
            return [[start, end]];
        }

        direction /= length;
        var head = Math.Min(length * MaxHeadShare, Math.Max(length * HeadShare, 12 + (thickness * 2.5)));
        var back = end - (direction * head);
        var side = new Vector(-direction.Y, direction.X) * head * HeadWidth;
        var strokes = new List<IReadOnlyList<Point>>
        {
            new[] { start, back + (direction * Math.Min(head / 2, thickness)) },
            new[] { back + side, end, back - side, back + side },
        };

        // Fill: one line from the tip to each point across the back, a line width apart.
        var across = (side * 2).Length;
        var steps = Math.Clamp((int)(across / Math.Max(1, thickness * 0.8)), 1, 60);
        for (var i = 1; i < steps; i++)
        {
            strokes.Add(new[] { end, back + side - (side * 2 * i / steps) });
        }

        return strokes;
    }

    // A circle centred where the drag started, through where it ends (a range or a zone).
    internal static IReadOnlyList<Point> Circle(Point centre, Point edge)
    {
        var radius = (edge - centre).Length;
        return [.. Enumerable.Range(0, CirclePoints + 1).Select(i =>
        {
            var angle = 2 * Math.PI * i / CirclePoints;
            return new Point(centre.X + (radius * Math.Cos(angle)), centre.Y + (radius * Math.Sin(angle)));
        })];
    }
}
