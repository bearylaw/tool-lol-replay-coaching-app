using System.Windows.Media;
using RiftReview.App.Overlay;

namespace RiftReview.App.Tests;

public sealed class DrawCursorsTests
{
    [Fact]
    public void BuildsLoadableCursorsOncePerColour()
    {
        // WPF drawing needs a single-threaded apartment.
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var red = DrawCursors.Pen(Colors.Red);
                Assert.Same(red, DrawCursors.Pen(Colors.Red));
                Assert.NotSame(red, DrawCursors.Pen(Colors.Yellow));
                Assert.NotNull(DrawCursors.Eraser());
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }
}
