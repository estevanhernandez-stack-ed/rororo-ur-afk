using Labs626.UrAfk.Win32;
using Xunit;

namespace Labs626.UrAfk.Tests;

public class SkipHotkeyServiceTests
{
    [Fact]
    public void Dispose_WithoutStart_DoesNotThrow()
    {
        var svc = new SkipHotkeyService(0x77);
        svc.Dispose(); // must return cleanly
    }

    [Fact]
    public void UnboundVk_StartThenDispose_NoOps()
    {
        var svc = new SkipHotkeyService(0); // 0 = unbound → Start no-ops
        svc.Start();
        svc.Dispose(); // must return cleanly, no pump thread created
    }

    [Fact]
    public void StartThenImmediateDispose_CompletesWithinBudget()
    {
        // Exercises the readiness handshake: Dispose must not hang even when it
        // races the pump thread's startup. Uses a real vk (F8); if RegisterHotKey
        // fails in this environment the pump returns early and Dispose still returns.
        var svc = new SkipHotkeyService(0x77);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        svc.Start();
        svc.Dispose();
        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"Start-then-Dispose took {sw.Elapsed} — handshake likely hung.");
    }
}
