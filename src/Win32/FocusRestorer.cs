using System.Runtime.InteropServices;
using Labs626.UrAfk.Core;

namespace Labs626.UrAfk.Win32;

/// <summary>Steal + restore: capture the user's foreground hwnd before a grab,
/// put it back after. Restore is best-effort — never fight the user for focus.
///
/// Restore uses a bare SetForegroundWindow: at restore time OUR process is the
/// foreground owner (we just focused the Roblox window via our own grab), so
/// Windows permits handing focus back without the AttachThreadInput dance that
/// Win32Focus needs for the initial steal. If manual smoke shows restores
/// failing, route restore through an hwnd-overload of Win32Focus instead
/// (noted in Task 12's smoke script).</summary>
public sealed class FocusRestorer : IFocusRestorer
{
    public nint CaptureForeground() => GetForegroundWindow();

    public bool RestoreForeground(nint hwnd)
    {
        if (hwnd == 0) return false;
        return SetForegroundWindow(hwnd);
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);
}
