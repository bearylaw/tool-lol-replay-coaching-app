using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RiftReview.App.Overlay;

/// <summary>
/// Large cursors for drawing over the game, where Windows' own pen cursor is hard to see: a pencil
/// whose tip shows the current colour (the hotspot is the tip) and a ring for the eraser. Each is
/// drawn once per colour and kept.
/// </summary>
internal static class DrawCursors
{
    private const int Size = 64;
    private static readonly Dictionary<Color, Cursor> Pens = [];
    private static Cursor? eraser;

    // The pencil runs from its tip at the bottom left up to the top right.
    private static readonly Point Tip = new(4, 60);

    /// <summary>The pencil cursor with a tip in <paramref name="colour"/>.</summary>
    public static Cursor Pen(Color colour)
    {
        if (!Pens.TryGetValue(colour, out var cursor))
        {
            cursor = Create(context => DrawPencil(context, colour), Tip);
            Pens[colour] = cursor;
        }

        return cursor;
    }

    /// <summary>A ring the size of what the eraser takes, with a dot in its centre (the hotspot).</summary>
    public static Cursor Eraser() => eraser ??= Create(DrawRing, new Point(Size / 2, Size / 2));

    private static void DrawPencil(DrawingContext context, Color colour)
    {
        var along = new Vector(1, -1) / Math.Sqrt(2);
        var across = new Vector(1, 1) / Math.Sqrt(2);
        const double halfWidth = 7;
        var coneEnd = Tip + (along * 15);
        var bodyEnd = Tip + (along * 50);
        var capEnd = Tip + (along * 58);
        var outline = new Pen(Brushes.Black, 3) { LineJoin = PenLineJoin.Round };
        var light = new Pen(Brushes.White, 1.2) { LineJoin = PenLineJoin.Round };

        var cone = Shape(Tip, coneEnd + (across * halfWidth), coneEnd - (across * halfWidth));
        var body = Shape(coneEnd + (across * halfWidth), bodyEnd + (across * halfWidth), bodyEnd - (across * halfWidth), coneEnd - (across * halfWidth));
        var cap = Shape(bodyEnd + (across * halfWidth), capEnd + (across * halfWidth), capEnd - (across * halfWidth), bodyEnd - (across * halfWidth));
        foreach (var part in new[] { cone, body, cap })
        {
            context.DrawGeometry(null, outline, part);
        }

        context.DrawGeometry(new SolidColorBrush(Color.FromRgb(0xF0, 0xD9, 0xA4)), light, body);
        context.DrawGeometry(new SolidColorBrush(Color.FromRgb(0x3A, 0x44, 0x52)), light, cap);
        context.DrawGeometry(new SolidColorBrush(colour), light, cone);
    }

    private static void DrawRing(DrawingContext context)
    {
        var centre = new Point(Size / 2, Size / 2);
        context.DrawEllipse(null, new Pen(Brushes.Black, 5), centre, 22, 22);
        context.DrawEllipse(null, new Pen(Brushes.White, 2.5), centre, 22, 22);
        context.DrawEllipse(Brushes.White, new Pen(Brushes.Black, 1.5), centre, 3, 3);
    }

    private static StreamGeometry Shape(params Point[] points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], isFilled: true, isClosed: true);
            context.PolyLineTo(points[1..], isStroked: true, isSmoothJoin: true);
        }

        geometry.Freeze();
        return geometry;
    }

    // A .cur file holding one PNG image: the directory says "cursor" and carries the hotspot.
    private static Cursor Create(Action<DrawingContext> draw, Point hotspot)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            draw(context);
        }

        var bitmap = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new MemoryStream();
        encoder.Save(png);

        var file = new MemoryStream();
        using (var writer = new BinaryWriter(file, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)2);
            writer.Write((ushort)1);
            writer.Write((byte)Size);
            writer.Write((byte)Size);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)hotspot.X);
            writer.Write((ushort)hotspot.Y);
            writer.Write((uint)png.Length);
            writer.Write((uint)22);
            writer.Write(png.ToArray());
        }

        file.Position = 0;
        return new Cursor(file, scaleWithDpi: true);
    }
}
