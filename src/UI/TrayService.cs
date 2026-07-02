using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Hardcodet.Wpf.TaskbarNotification;

namespace Labs626.UrAfk.UI;

/// <summary>
/// The plugin's own system-tray icon. Left-click/double-click surfaces the main
/// window; right-click context menu offers Open, a Keep-active toggle bound
/// two-way to MainViewModel.MasterEnabled, and Quit. Modeled on ur-task's
/// TrayService (see ..\rororo-ur-task\src\UI\TrayService.cs), trimmed to three
/// menu items.
///
/// The tray icon itself is a small vector-drawn navy/cyan mark built via
/// IconSource (a pure WPF ImageSource — no System.Drawing.Common dependency,
/// which matters here since this project sets UseWindowsForms=false). It's a
/// placeholder: the real icon asset is a Task 12 pre-ship gate through the
/// 626labs-design skill.
/// </summary>
internal sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly Window _window;

    public TrayService(Window window, MainViewModel vm)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        ArgumentNullException.ThrowIfNull(vm);

        _icon = new TaskbarIcon
        {
            IconSource = BuildPlaceholderIcon(),
            ToolTipText = "RoRoRo Ur AFK",
            ContextMenu = BuildMenu(vm),
        };
        _icon.TrayLeftMouseUp += (_, _) => SurfaceWindow();
        _icon.TrayMouseDoubleClick += (_, _) => SurfaceWindow();
    }

    public void Dispose() => _icon.Dispose();

    private ContextMenu BuildMenu(MainViewModel vm)
    {
        var menu = new ContextMenu();

        var open = new MenuItem { Header = "Open" };
        open.Click += (_, _) => SurfaceWindow();
        menu.Items.Add(open);

        var keepActive = new MenuItem { Header = "Keep-active", IsCheckable = true, DataContext = vm };
        keepActive.SetBinding(MenuItem.IsCheckedProperty,
            new Binding(nameof(MainViewModel.MasterEnabled)) { Mode = BindingMode.TwoWay });
        menu.Items.Add(keepActive);

        menu.Items.Add(new Separator());

        var quit = new MenuItem { Header = "Quit" };
        quit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(quit);

        return menu;
    }

    private void SurfaceWindow()
    {
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
        _window.Focus();
    }

    private static ImageSource BuildPlaceholderIcon()
    {
        var background = new GeometryDrawing(
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F1F31")),
            null, new RectangleGeometry(new Rect(0, 0, 32, 32), 6, 6));

        var dot = new GeometryDrawing(
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17D4FA")),
            null, new EllipseGeometry(new Point(16, 16), 8, 8));

        var group = new DrawingGroup();
        group.Children.Add(background);
        group.Children.Add(dot);
        group.Freeze();

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
