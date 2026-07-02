using System.Diagnostics;
using System.Runtime.InteropServices;
using Labs626.UrAfk.Core;

namespace Labs626.UrAfk.Win32;

/// <summary>The ONLY input synthesis in this codebase: a single Space tap
/// (down, 50ms hold, up). Extracted from ur-task's keep-alive. Maps to the
/// system.synthesize-keyboard-input capability.</summary>
public sealed class KeystrokeSender : IKeystrokeSender
{
    public bool TapSpace()
    {
        const ushort VK_SPACE = 0x20;
        var down = SendKeyEvent(VK_SPACE, keyUp: false);
        Thread.Sleep(50); // briefly held
        var up = SendKeyEvent(VK_SPACE, keyUp: true);

        // SendInput returns the number of events inserted; 0 means Windows
        // rejected the call (e.g. cbSize mismatch). Surface it instead of
        // swallowing — a silent 0 here made ur-task's keep-alive a no-op
        // for every release through v0.2.2.
        var ok = down == 1 && up == 1;
        if (!ok)
            Debug.WriteLine($"[KeystrokeSender] Space rejected by SendInput (down={down}, up={up}).");
        return ok;
    }

    private static uint SendKeyEvent(ushort vk, bool keyUp)
    {
        var scanCode = (ushort)MapVirtualKey(vk, 0);
        var flags = keyUp ? 0x0002u : 0u; // KEYEVENTF_KEYUP

        var input = new INPUT { type = 1 };
        input.union.keyboard = new KEYBDINPUT { wVk = vk, wScan = scanCode, dwFlags = flags };
        return SendOne(ref input);
    }

    private static unsafe uint SendOne(ref INPUT input)
    {
        fixed (INPUT* p = &input) { return SendInput(1, p, Marshal.SizeOf<INPUT>()); }
    }

    /// <summary>Test seam: MUST equal the canonical Win32 INPUT size (40 on x64),
    /// or SendInput rejects every event.</summary>
    internal static int InputStructSize => Marshal.SizeOf<INPUT>();

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion union; }

    // The union MUST be sized to its largest member (MOUSEINPUT). We only write
    // the keyboard field, but Win32 validates cbSize against the full INPUT size —
    // drop MOUSEINPUT and cbSize comes up short, SendInput fails, no Space.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mouse;
        [FieldOffset(0)] public KEYBDINPUT keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern unsafe uint SendInput(uint cInputs, INPUT* pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);
}
