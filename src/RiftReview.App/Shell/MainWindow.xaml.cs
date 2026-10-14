using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RiftReview.App.Shell;

/// <summary>The main window. Handles what XAML cannot: dropped files and the dark title bar.</summary>
internal sealed partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => NativeMethods.UseDarkTitleBar(this);
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        // async void: a UI event boundary; ImportAsync reports its own failures.
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            await ViewModel.ImportAsync([.. paths.Where(p => p.EndsWith(".rofl", StringComparison.OrdinalIgnoreCase))]);
        }
    }
}

internal static partial class NativeMethods
{
    private const int DwmUseImmersiveDarkMode = 20;

    /// <summary>Gives a window the dark title bar that matches the app.</summary>
    internal static void UseDarkTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var enabled = 1;
        // Cosmetic only: older Windows builds ignore the attribute and keep a light title bar.
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AttachConsole(int processId);
}
