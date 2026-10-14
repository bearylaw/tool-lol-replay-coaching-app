using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using RiftReview.Capture;

namespace RiftReview.App.Overlay;

/// <summary>
/// A transparent window kept exactly over League's replay window. It never takes the keyboard
/// focus from the game (clicks on its panels work, clicks elsewhere reach the game), and it hides
/// while another program is in front.
/// </summary>
internal sealed partial class OverlayWindow : Window
{
    private const int ExtendedStyle = -20;
    private const int NoActivate = 0x08000000;
    private const int ToolWindow = 0x00000080;
    private const uint NoActivateFlag = 0x0010;

    // Panels are laid out for a 1080-pixel-high game window and scale with it.
    private const double DesignHeight = 1080;
    private static readonly IntPtr TopmostWindow = new(-1);

    private readonly DispatcherTimer track = new() { Interval = TimeSpan.FromMilliseconds(300) };

    // Tab and O are League's (it reads the keyboard itself, so its scoreboard comes up whatever is registered);
    // the overlay watches the key this often, only reading it.
    private readonly DispatcherTimer tabWatch = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private bool oDown;
    private bool scoreboardToggled;
    private bool shifted;
    private ScreenRect? placed;
    private NoteWindow? note;

    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.NoteRequested += OnNoteRequested;
        Ink.Attach(viewModel.Drawing, () => viewModel.Settings.DrawingFadeSeconds, async () => await viewModel.DrawingStartedAsync());
        SourceInitialized += (_, _) =>
        {
            UseNoActivateStyle();
            HwndSource.FromHwnd(Handle)?.AddHook(OnMessage);
        };
        track.Tick += (_, _) => Track();
        tabWatch.Tick += (_, _) => WatchTab();
        Loaded += (_, _) =>
        {
            Track();
            track.Start();
        };
        Closed += (_, _) =>
        {
            track.Stop();
            tabWatch.Stop();
            SetShortcuts(false);
        };
    }

    private IntPtr Handle => new WindowInteropHelper(this).Handle;

    private void UseNoActivateStyle()
    {
        var style = NativeMethods.GetWindowLongPtrW(Handle, ExtendedStyle);
        NativeMethods.SetWindowLongPtrW(Handle, ExtendedStyle, new IntPtr((long)style | NoActivate | ToolWindow));
    }

    // Follows the game window in physical pixels; shows only while the game or this overlay is in front.
    private void Track()
    {
        if (GameWindow.Find() is not { } game)
        {
            Hide();
            return;
        }

        // The note box is the overlay's own window: while it has the keyboard the overlay stays, and
        // the shortcuts are off so Space types a space.
        var foreground = NativeMethods.GetForegroundWindow();
        var gameInFront = foreground == GameWindow.FindHandle();
        var inFront = gameInFront || foreground == Handle || (note is not null && foreground == new WindowInteropHelper(note).Handle);
        SetShortcuts(gameInFront);
        tabWatch.IsEnabled = gameInFront;
        if (!inFront)
        {
            Hide();
            return;
        }

        if (!IsVisible)
        {
            Show();
        }

        if (placed != game)
        {
            placed = game;
            NativeMethods.SetWindowPos(Handle, TopmostWindow, game.Left, game.Top, game.Width, game.Height, NoActivateFlag);
            var pixelsPerDip = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleY;
            var scale = ((OverlayViewModel)DataContext).Settings.Scale * Math.Max(0.5, game.Height / pixelsPerDip / DesignHeight);
            PanelScale.ScaleX = scale;
            PanelScale.ScaleY = scale;
        }

    }

    private OverlayViewModel ViewModel => (OverlayViewModel)DataContext;

    // Shortcuts while League is in front (the overlay never takes the keyboard): Space pause/play,
    // Ctrl+Alt+D draw on/off, Ctrl+Alt+Z undo, Ctrl+Alt+X clear, Ctrl+Alt+B bookmark, Ctrl+Alt+1 to 9
    // a bookmark with the tag in that place. Registered only while the game is in front, so Space
    // and the combinations work normally in every other program; Ctrl+Shift is left to League (its
    // replay keys, such as unlocking the zoom).
    private static readonly Dictionary<int, (uint Modifiers, uint Key, Action<OverlayViewModel> Run)> Shortcuts = new()
    {
        [1] = (ControlAlt, 0x44, vm => vm.Drawing.IsOn = !vm.Drawing.IsOn),
        [2] = (ControlAlt, 0x5A, vm => vm.Drawing.Undo()),
        [3] = (ControlAlt, 0x58, vm => vm.Drawing.Clear()),
        [4] = (NoRepeat, 0x20, vm => vm.PlayPauseCommand.Execute(null)),
        [5] = (ControlAlt, 0x42, vm => vm.AddBookmarkCommand.Execute(null)),
    };

    // Ctrl+Alt+1 to 9: hotkey ids from here, keys from the digit 1.
    private const int FirstTagShortcut = 100;
    private const uint DigitOne = 0x31;

    private const int TabKey = 0x09;
    private const int OKey = 0x4F;

    // League's Tab scoreboard covers the bottom 231 of 1080 pixels of the game window (16.20, measured
    // at 308 of 1440); the bottom column, 12 above the bottom, slides up past it with a small gap.
    private const double LeagueScoreboardHeight = 231 + 8 - 12;
    private static readonly Duration Slide = new(TimeSpan.FromMilliseconds(180));

    // League shows its scoreboard at the bottom while Tab is held, and from one O to the next; the
    // bar moves out of its way meanwhile.
    private void WatchTab()
    {
        var o = (NativeMethods.GetAsyncKeyState(OKey) & 0x8000) != 0;
        if (o && !oDown)
        {
            scoreboardToggled = !scoreboardToggled;
        }

        oDown = o;
        var shift = (NativeMethods.GetAsyncKeyState(TabKey) & 0x8000) != 0 || scoreboardToggled;
        if (shift == shifted)
        {
            return;
        }

        shifted = shift;

        // The column is scaled with the overlay's own size setting; League's scoreboard is not.
        var to = shift ? -LeagueScoreboardHeight / ViewModel.Settings.Scale : 0;
        BottomShift.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            new System.Windows.Media.Animation.DoubleAnimation(to, Slide) { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
    }

    private const int HotKeyMessage = 0x0312;
    private const uint NoRepeat = 0x4000;
    private const uint ControlAlt = 0x0001 | 0x0002 | NoRepeat;
    private bool shortcutsOn;

    private void SetShortcuts(bool on)
    {
        if (on == shortcutsOn)
        {
            return;
        }

        shortcutsOn = on;
        var tags = Math.Min(ViewModel.Bookmarks?.Tags.Count ?? 0, Settings.BookmarkTag.WithShortcut);
        var all = Shortcuts.Select(s => (Id: s.Key, s.Value.Modifiers, s.Value.Key))
            .Concat(Enumerable.Range(0, tags).Select(i => (Id: FirstTagShortcut + i, Modifiers: ControlAlt, Key: DigitOne + (uint)i)));
        foreach (var (id, modifiers, key) in all)
        {
            // Another program may own a combination already; that shortcut then stays unused.
            _ = on ? NativeMethods.RegisterHotKey(Handle, id, modifiers, key) : NativeMethods.UnregisterHotKey(Handle, id);
        }
    }

    private IntPtr OnMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == HotKeyMessage && Shortcuts.TryGetValue(wParam.ToInt32(), out var shortcut))
        {
            shortcut.Run(ViewModel);
            handled = true;
        }
        else if (message == HotKeyMessage && wParam.ToInt32() - FirstTagShortcut is >= 0 and < Settings.BookmarkTag.WithShortcut and var tag)
        {
            _ = ViewModel.AddTaggedBookmarkAsync(tag);
            handled = true;
        }

        return IntPtr.Zero;
    }

    // Opens the note box centred over the game, above the control bar; when it closes the keyboard
    // goes back to the game.
    private void OnNoteRequested(object? sender, NoteRequest request)
    {
        if (note is not null)
        {
            note.Activate();
            return;
        }

        var time = Replays.MatchFormat.Duration(TimeSpan.FromSeconds(request.Time));
        var heading = request.Existing is null ? Localization.Loc.Format("Bookmark_New", time) : Localization.Loc.Format("Bookmark_Edit", time);
        var tags = new Replays.TagChoices(ViewModel.Bookmarks?.Tags ?? [], request.Existing?.Bookmark.Tag);
        note = new NoteWindow(heading, request.Existing?.Text ?? string.Empty, tags) { Owner = this };
        note.Loaded += (_, _) =>
        {
            note.Left = Left + ((ActualWidth - note.ActualWidth) / 2);
            note.Top = Top + ActualHeight - note.ActualHeight - (160 * PanelScale.ScaleY);
        };
        note.Done += async (_, result) =>
        {
            note = null;
            if (GameWindow.FindHandle() is { } game && game != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(game);
            }

            await ViewModel.FinishNoteAsync(request, result);
        };
        note.Show();
    }




    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll")]
        internal static partial IntPtr GetForegroundWindow();

        [LibraryImport("user32.dll")]
        internal static partial short GetAsyncKeyState(int key);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetForegroundWindow(IntPtr window);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool UnregisterHotKey(IntPtr window, int id);

        [LibraryImport("user32.dll")]
        internal static partial IntPtr GetWindowLongPtrW(IntPtr window, int index);

        [LibraryImport("user32.dll")]
        internal static partial IntPtr SetWindowLongPtrW(IntPtr window, int index, IntPtr value);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    }
}
