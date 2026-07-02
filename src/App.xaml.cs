using System.Windows;
using Labs626.UrAfk.UI;

namespace Labs626.UrAfk;

public partial class App : Application
{
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _window = new MainWindow();
        _window.Show();
    }
}
