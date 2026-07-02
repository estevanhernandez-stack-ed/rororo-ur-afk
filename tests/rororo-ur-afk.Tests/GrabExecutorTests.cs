using Labs626.UrAfk.Core;
using Xunit;

namespace Labs626.UrAfk.Tests;

public class GrabExecutorTests
{
    private sealed class Fakes
    {
        public readonly List<string> Calls = new();
        public bool FocusOk = true;
        public int ForegroundPid;
        public bool TapOk = true;

        public IFocusRestorer Restorer => new FakeRestorer(this);
        public IWindowFocus Focus => new FakeFocus(this);
        public IForegroundPidProbe Probe => new FakeProbe(this);
        public IKeystrokeSender Keys => new FakeKeys(this);
        public IDelay Delay => new FakeDelay(this);

        private sealed class FakeRestorer(Fakes f) : IFocusRestorer
        {
            public nint CaptureForeground() { f.Calls.Add("capture"); return 0x42; }
            public bool RestoreForeground(nint hwnd) { f.Calls.Add($"restore:{hwnd:x}"); return true; }
        }
        private sealed class FakeFocus(Fakes f) : IWindowFocus
        {
            public (bool ok, string? error) Focus(int pid)
            { f.Calls.Add($"focus:{pid}"); return f.FocusOk ? (true, null) : (false, "denied"); }
        }
        private sealed class FakeProbe(Fakes f) : IForegroundPidProbe
        {
            public int GetForegroundPid() { f.Calls.Add("verify"); return f.ForegroundPid; }
        }
        private sealed class FakeKeys(Fakes f) : IKeystrokeSender
        {
            public bool TapSpace() { f.Calls.Add("space"); return f.TapOk; }
        }
        private sealed class FakeDelay(Fakes f) : IDelay
        {
            public Task Wait(TimeSpan d, CancellationToken ct) { f.Calls.Add("settle"); return Task.CompletedTask; }
        }
    }

    private static DueCandidate Target(int pid = 500)
        => new("acct-1", "Este", pid, 1000);

    private static GrabExecutor Build(Fakes f)
        => new(f.Restorer, f.Focus, f.Probe, f.Keys, f.Delay, TimeSpan.FromSeconds(1));

    [Fact]
    public async Task HappyPath_OrderIsCaptureFocusSettleVerifySpaceRestore()
    {
        var f = new Fakes { ForegroundPid = 500 };
        var outcome = await Build(f).ExecuteAsync(Target(500), CancellationToken.None);

        Assert.Equal(GrabOutcome.Jumped, outcome);
        Assert.Equal(new[] { "capture", "focus:500", "settle", "verify", "space", "restore:42" }, f.Calls);
    }

    [Fact]
    public async Task VerifyFails_NoKeystrokeEver_StillRestores()
    {
        var f = new Fakes { ForegroundPid = 777 };   // user's window won the race
        var outcome = await Build(f).ExecuteAsync(Target(500), CancellationToken.None);

        Assert.Equal(GrabOutcome.SkippedVerifyFailed, outcome);
        Assert.DoesNotContain("space", f.Calls);      // THE invariant
        Assert.Contains("restore:42", f.Calls);
    }

    [Fact]
    public async Task FocusFails_SkipsWithoutVerifyOrSpace_StillRestores()
    {
        var f = new Fakes { FocusOk = false };
        var outcome = await Build(f).ExecuteAsync(Target(500), CancellationToken.None);

        Assert.Equal(GrabOutcome.SkippedFocusFailed, outcome);
        Assert.DoesNotContain("space", f.Calls);
        Assert.DoesNotContain("verify", f.Calls);
        Assert.Contains("restore:42", f.Calls);
    }

    [Fact]
    public async Task InputRejected_ReportsButRestores()
    {
        var f = new Fakes { ForegroundPid = 500, TapOk = false };
        var outcome = await Build(f).ExecuteAsync(Target(500), CancellationToken.None);

        Assert.Equal(GrabOutcome.InputRejected, outcome);
        Assert.Contains("restore:42", f.Calls);
    }
}
