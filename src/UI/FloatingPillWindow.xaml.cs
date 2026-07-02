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
        var area = SystemParameters.WorkArea;

        (Left, Top) = vm.Corner switch
        {
            PillCorner.TopLeft => (area.Left + EdgeOffset, area.Top + EdgeOffset),
            PillCorner.TopRight => (area.Right - ActualWidth - EdgeOffset, area.Top + EdgeOffset),
            PillCorner.BottomLeft => (area.Left + EdgeOffset, area.Bottom - ActualHeight - EdgeOffset),
            _ => (area.Right - ActualWidth - EdgeOffset, area.Bottom - ActualHeight - EdgeOffset), // BottomRight
        };
    }
}
