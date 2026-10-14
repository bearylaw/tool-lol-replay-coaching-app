using System.Runtime.InteropServices;

namespace RiftReview.Capture;

/// <summary>A rectangle in screen pixels.</summary>
public readonly record struct ScreenRect(int Left, int Top, int Width, int Height);

/// <summary>
/// Where League's game window is, so the overlay can lie exactly over it. Only the window's
/// position is read: nothing on screen, nothing in the game.
/// </summary>
public static partial class GameWindow
{
    /// <summary>The game window's handle, or zero when no game window is open.</summary>
    public static IntPtr FindHandle() => NativeMethods.FindWindowW(null, "League of Legends (TM) Client");

    /// <summary>The game window's client area in screen pixels, or <c>null</c> when no game window is open.</summary>
    public static ScreenRect? Find()
    {
        var window = FindHandle();
        if (window == IntPtr.Zero || !NativeMethods.GetClientRect(window, out var client))
        {
            return null;
        }

        var origin = new NativePoint { X = 0, Y = 0 };
        return NativeMethods.ClientToScreen(window, ref origin) && client.Right > 0 && client.Bottom > 0
            ? new ScreenRect(origin.X, origin.Y, client.Right, client.Bottom)
            : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
        internal static partial IntPtr FindWindowW(string? className, string windowName);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetClientRect(IntPtr window, out NativeRect rect);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool ClientToScreen(IntPtr window, ref NativePoint point);
    }
}
