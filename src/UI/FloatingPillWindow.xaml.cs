using System.ComponentModel;
using System.Windows;
using Labs626.UrAfk.Core;

namespace Labs626.UrAfk.UI;

/// <summary>Small always-on-top corner pill mirroring the header pill's state.
/// DataContext is a PillViewModel (see App.xaml.cs — <c>DataContext = vm.Pill</c>).
/// Repositions to the configured corner (~16px offset from the screen work-area
/// edges, via SystemParameters.WorkArea) on load, whenever the window's own size
/// changes (SizeToContent means width/height aren't known until the first layout
/// pass), and whenever PillViewModel.Corner changes (settings save → MainViewModel
/// .Persist → PillViewModel.NotifySettingsChanged).</summary>
public partial class FloatingPillWindow : Window
{
    private const double EdgeOffset = 16;

    public FloatingPillWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, _) => Reposition();
        Loaded += (_, _) => Reposition();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PillViewModel oldVm) oldVm.PropertyChanged -= OnPillPropertyChanged;
        if (e.NewValue is PillViewModel newVm) newVm.PropertyChanged += OnPillPropertyChanged;
        Reposition();
    }

    private void OnPillPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PillViewModel.Corner) or null)
            Reposition();
    }

    private void Reposition()
    {
        if (DataContext is not PillViewModel vm) return;

        // A dragged position wins until the user picks a corner again (which
        // clears it). Clamp into the virtual screen so a resolution change or
        // unplugged monitor can't strand the pill off-screen.
        if (vm is { X: { } x, Y: { } y })
        {
            Left = Math.Clamp(x, SystemParameters.VirtualScreenLeft,
                Math.Max(SystemParameters.VirtualScreenLeft,
                    SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - ActualWidth));
            Top = Math.Clamp(y, SystemParameters.VirtualScreenTop,
                Math.Max(SystemParameters.VirtualScreenTop,
                    SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - ActualHeight));
            return;
        }

        var area = SystemParameters.WorkArea;
        (Left, Top) = vm.Corner switch
        {
            PillCorner.TopLeft => (area.Left + EdgeOffset, area.Top + EdgeOffset),
            PillCorner.TopRight => (area.Right - ActualWidth - EdgeOffset, area.Top + EdgeOffset),
            PillCorner.BottomLeft => (area.Left + EdgeOffset, area.Bottom - ActualHeight - EdgeOffset),
            _ => (area.Right - ActualWidth - EdgeOffset, area.Bottom - ActualHeight - EdgeOffset), // BottomRight
        };
    }

    /// <summary>Drag anywhere on the pill body to move it; the end position
    /// persists. A plain click (no movement) doesn't override the corner preset.</summary>
    private void OnPillDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        var (startLeft, startTop) = (Left, Top);
        try { DragMove(); } catch (InvalidOperationException) { return; }
        if (Math.Abs(Left - startLeft) < 3 && Math.Abs(Top - startTop) < 3) return;
        if (DataContext is PillViewModel vm) vm.SavePosition(Left, Top);
    }
}
