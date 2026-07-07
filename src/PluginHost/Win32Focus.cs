using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Labs626.UrAfk.PluginHost;

/// <summary>
/// Thin Win32 helper that forces a target window to the foreground even when the
/// user is idle — the exact condition a keep-alive plugin has to survive.
///
/// Windows refuses a bare SetForegroundWindow from a background process, and the
/// AttachThreadInput trick alone is NOT enough on modern Windows: when the user
/// has generated no recent input, the foreground-lock timeout makes
/// SetForegroundWindow silently no-op (verified live 2026-07-06 — grabs logged
/// SkippedVerifyFailed on idle accounts, the window never coming forward). The
/// robust remedy layers three moves: attach to the foreground thread's input
/// queue, temporarily zero the system foreground-lock timeout (restored right
/// after), and BringWindowToTop as a backstop. The caller still verifies the
/// foreground actually became the target pid before synthesizing any input, so
/// a focus that somehow still fails degrades to a skipped grab, never a stray
/// keystroke.
/// </summary>
internal static class Win32Focus
{
    private const uint SPI_GETFOREGROUNDLOCKTIMEOUT = 0x2000;
    private const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;
    private const uint SPIF_SENDCHANGE = 0x02;
    private const int SW_RESTORE = 9;

    /// <summary>
    /// Force the main window of the process identified by <paramref name="pid"/>
    /// to the foreground. Returns immediately after the Win32 calls — callers
    /// should add a settle delay and re-check the foreground pid before relying
    /// on it (the safety invariant).
    /// </summary>
    /// <returns>
    /// <c>true</c> if the focus sequence ran without error; <c>false</c> with an
    /// error description otherwise.
    /// </returns>
    public static (bool ok, string? error) AttachAndFocus(int pid)
    {
        try
        {
            var hwnd = Process.GetProcessById(pid).MainWindowHandle;
            if (hwnd == IntPtr.Zero) return (false, "MainWindowHandle is null.");

            // A minimized target won't take foreground from SetForegroundWindow alone.
            if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);

            var fgHwnd = GetForegroundWindow();
            var fgThreadId = fgHwnd != IntPtr.Zero
                ? GetWindowThreadProcessId(fgHwnd, out _)
                : 0u;
            var ourThreadId = GetCurrentThreadId();
            bool attached = false;
            if (fgThreadId != 0 && fgThreadId != ourThreadId)
            {
                attached = AttachThreadInput(fgThreadId, ourThreadId, true);
            }

            uint savedTimeout = 0;
            bool loweredLock = false;
            try
            {
                // Zero the foreground-lock timeout so our SetForegroundWindow is
                // honored while the user is idle; restore the user's value after.
                if (SystemParametersInfoGet(SPI_GETFOREGROUNDLOCKTIMEOUT, 0, ref savedTimeout, 0))
                {
                    SystemParametersInfoSet(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, IntPtr.Zero, SPIF_SENDCHANGE);
                    loweredLock = true;
                }

                SetForegroundWindow(hwnd);
                BringWindowToTop(hwnd);
            }
            finally
            {
                if (loweredLock)
                {
                    SystemParametersInfoSet(SPI_SETFOREGROUNDLOCKTIMEOUT, 0,
                        new IntPtr(savedTimeout), SPIF_SENDCHANGE);
                }
                if (attached) AttachThreadInput(fgThreadId, ourThreadId, false);
            }
            return (true, null);
        }
        catch (ArgumentException) { return (false, "Process not found (pid stale)."); }
        catch (Exception ex) { return (false, ex.Message); }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // SPI GET writes the DWORD into pvParam (by ref); SET passes the value AS pvParam.
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoGet(uint uiAction, uint uiParam, ref uint pvParam, uint fWinIni);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoSet(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
