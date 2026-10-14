using System.Windows;
using RiftReview.App.Overlay;

namespace RiftReview.App.Tests;

public sealed class OverlayTests
{
    [Fact]
    public void AnArrowIsAShaftAndAFilledHeadAtItsEnd()
    {
        var strokes = DrawingShapes.Arrow(new Point(0, 0), new Point(200, 0), thickness: 4);

        Assert.Equal(new Point(0, 0), strokes[0][0]);
        Assert.True(strokes[0][1].X < 200);
        Assert.Equal(new Point(200, 0), strokes[1][1]);
        Assert.True(strokes[1][0].X < 200 && strokes[1][0].Y > 0 && strokes[1][2].Y < 0);
        Assert.True(strokes.Count > 3);
        Assert.All(strokes.Skip(2), fill => Assert.Equal(new Point(200, 0), fill[0]));
    }

    [Fact]
    public void TheHeadGrowsWithTheArrow()
    {
        static double Head(double length) => length - DrawingShapes.Arrow(new Point(0, 0), new Point(length, 0), thickness: 4)[1][0].X;

        Assert.Equal(2 * Head(300), Head(600), 3);
        Assert.True(Head(600) > 150);
    }

    [Fact]
    public void AShortArrowKeepsItsHeadSmallerThanTheLine()
    {
        var strokes = DrawingShapes.Arrow(new Point(0, 0), new Point(10, 0), thickness: 20);

        Assert.True(strokes[1][0].X >= 5);
    }

    [Fact]
    public void ThePointerKeepsTheToolsOpenButLeavesTheMouseToTheGame()
    {
        var drawing = new DrawingState { IsOn = true };
        Assert.True(drawing.TakesMouse);

        drawing.Tool = DrawTool.Pointer;

        Assert.True(drawing.IsOn);
        Assert.False(drawing.TakesMouse);
    }

    [Fact]
    public void ACircleIsCentredWhereTheDragStarted()
    {
        var points = DrawingShapes.Circle(new Point(100, 100), new Point(130, 140));

        Assert.All(points, p => Assert.Equal(50, (p - new Point(100, 100)).Length, 6));
        Assert.Equal(0, (points[0] - points[^1]).Length, 6);
    }

    [Fact]
    public void DrawingThicknessStaysInRange()
    {
        var drawing = new DrawingState { Thickness = 100 };

        Assert.Equal(24, drawing.Thickness);
    }

}
