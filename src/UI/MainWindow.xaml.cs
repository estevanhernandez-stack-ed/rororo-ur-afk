using System.ComponentModel;

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
}
