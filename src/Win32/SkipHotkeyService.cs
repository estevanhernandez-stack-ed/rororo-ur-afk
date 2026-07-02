using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Labs626.UrAfk.Win32;

/// <summary>One global hotkey (default F8): skip the imminent grab during its
/// countdown. Modeled on ur-task's HotkeyService message-pump pattern (see
/// ..\rororo-ur-task\src\Hotkeys\HotkeyService.cs), trimmed to a single
/// unmodified key. vk == 0 means unbound (service no-ops).
///
/// Start() is fire-and-forget and never throws: App startup calls it bare
/// (no try/catch), so a RegisterHotKey conflict (another app already owns
/// this vk) surfaces as a Debug.WriteLine, not a crash — same "surface, don't
/// swallow, don't crash the host" posture as KeystrokeSender and
/// FocusRestorer elsewhere in this layer.</summary>
public sealed class SkipHotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int WM_QUIT_PUMP = 0x0012; // WM_QUIT
    private const int HotkeyId = 1;

    private readonly uint _vk;
    private Thread? _pumpThread;
    private uint _pumpThreadId;

    public event Action? SkipPressed;

    public SkipHotkeyService(uint vk) => _vk = vk;

    public void Start()
    {
        if (_vk == 0 || _pumpThread is not null) return;
        _pumpThread = new Thread(Pump) { IsBackground = true, Name = "urafk-skip-hotkey" };
        _pumpThread.Start();
    }

    private void Pump()
    {
        _pumpThreadId = GetCurrentThreadId();
        if (!RegisterHotKey(IntPtr.Zero, HotkeyId, 0 /* MOD_NONE */, _vk))
        {
            Debug.WriteLine($"[SkipHotkeyService] RegisterHotKey failed for vk=0x{_vk:X2}, win32 error {Marshal.GetLastWin32Error()}.");
            return;
        }
        try
        {
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == WM_HOTKEY && (int)msg.wParam == HotkeyId)
                {
                    try { SkipPressed?.Invoke(); } catch { }
                }
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        finally
        {
            UnregisterHotKey(IntPtr.Zero, HotkeyId);
        }
    }

    public void Dispose()
    {
        if (_pumpThreadId != 0) PostThreadMessage(_pumpThreadId, WM_QUIT_PUMP, IntPtr.Zero, IntPtr.Zero);
        _pumpThread?.Join(TimeSpan.FromSeconds(1));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
