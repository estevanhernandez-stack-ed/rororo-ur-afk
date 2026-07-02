using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Labs626.UrAfk.Core;

namespace Labs626.UrAfk.UI;

/// <summary>PillStateKind → the 626 palette brush for the pill's status dot.
/// Off/Disconnected → muted grey; Watching → cyan; PreGrab/Grabbing → magenta;
/// ConsentRevoked → amber. Same IValueConverter shape as the family's
/// StatusDotBrushConverter. Colors are inline for v0.1 — the full design-skill
/// icon/chrome pass is a Task 12 pre-ship gate, not this one.</summary>
public sealed class PillBrushConverter : IValueConverter
{
    private static readonly Brush Grey = Frozen("#4A5C70");
    private static readonly Brush Cyan = Frozen("#17D4FA");
    private static readonly Brush Magenta = Frozen("#F22F89");
    private static readonly Brush Amber = Frozen("#F1B232");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is PillStateKind kind
            ? kind switch
            {
                PillStateKind.Watching => Cyan,
                PillStateKind.PreGrab or PillStateKind.Grabbing => Magenta,
                PillStateKind.ConsentRevoked => Amber,
                _ => Grey, // Off, Disconnected
            }
            : Grey;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
