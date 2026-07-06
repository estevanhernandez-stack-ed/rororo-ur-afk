using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Labs626.UrAfk.UI;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Close button hides to tray rather than exiting — App's ShutdownMode
    /// is OnExplicitShutdown, and the plugin keeps running in the tray watching the
    /// keep-active loop. Real exit only happens via TrayService's Quit item. Matches
    /// ur-task's RecorderWindow pattern (see ..\rororo-ur-task\src\UI\RecorderWindow.xaml.cs).</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    // ── Custom title bar handlers ──────────────────────────────────────────

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try { DragMove(); } catch (InvalidOperationException) { /* wrong button state; ignore */ }
        }
    }

    private void OnMinimizeClicked(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    /// <summary>Close button routes through OnClosing → hide-to-tray.</summary>
    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
