using System.Linq;
using Labs626.UrAfk.Core;
using Xunit;

namespace Labs626.UrAfk.Tests;

public class GrabExecutorTests
{
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(50);

    private sealed class Fakes
    {
        public readonly List<string> Calls = new();
        public bool FocusOk = true;
        public int ForegroundPid;
        public bool DownOk = true;
        public bool UpOk = true;

        /// <summary>Runs when the executor waits out the key hold — the seam where
        /// a real user alt-tabbing mid-hold is simulated.</summary>
        public Action? OnHold;

        /// <summary>Thrown from the hold wait, to model cancellation mid-hold.</summary>
        public Exception? HoldThrows;

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
            public bool SpaceDown() { f.Calls.Add("down"); return f.DownOk; }
            public bool SpaceUp() { f.Calls.Add("up"); return f.UpOk; }
        }
        private sealed class FakeDelay(Fakes f) : IDelay
        {
            public Task Wait(TimeSpan d, CancellationToken ct)
            {
                if (d == Hold)
                {
                    f.Calls.Add("hold");
                    f.OnHold?.Invoke();
                    if (f.HoldThrows is not null) throw f.HoldThrows;
                }
                else f.Calls.Add("settle");
                return Task.CompletedTask;
            }
        }
    }

    private static DueCandidate Target(int pid = 500)
        => new("acct-1", "Este", pid, 1000);

    private static GrabExecutor Build(Fakes f)
        => new(f.Restorer, f.Focus, f.Probe, f.Keys, f.Delay, Settle, Hold);

    [Fact]
    public async Task HappyPath_HoldsSpaceBetweenDownAndUp_ThenRestores()
    {
        var f = new Fakes { ForegroundPid = 500 };
        var outcome = await Build(f).ExecuteAsync(Target(500), CancellationToken.None);

        Assert.Equal(GrabOutcome.Jumped, outcome);
        Assert.Equal(
            new[] { "capture", "focus:500", "settle", "verify", "down", "hold", "verify", "up", "restore:42" },
            f.Calls);
    }

    [Fact]
    public async Task ForegroundDriftsDuringHold_RefocusesTargetBeforeReleasingSpace()
    {
        var f = new Fakes { ForegroundPid = 500 };
        f.OnHold = () => f.ForegroundPid = 777;   // user alt-tabbed while Space was down

        await Build(f).ExecuteAsync(Target(500), CancellationToken.None);

        var upIdx = f.Calls.IndexOf("up");
        var refocusIdx = f.Calls.LastIndexOf("focus:500");
        Assert.Equal(2, f.Calls.Count(c => c == "focus:500"));   // initial grab + reclaim
        Assert.True(refocusIdx < upIdx,
            $"target must be refocused before the key-up so the release lands on it. Calls: {string.Join(",", f.Calls)}");
    }

    [Fact]
    public async Task ForegroundDriftsDuringHold_ReportsJumpedAfterDrift()
    {
        var f = new Fakes { ForegroundPid = 500 };
        f.OnHold = () => f.ForegroundPid = 777;

        var outcome = await Build(f).ExecuteAsync(Target(500), CancellationToken.None);

        Assert.Equal(GrabOutcome.JumpedAfterDrift, outcome);
    }

    [Fact]
    public async Task NoDrift_DoesNotRefocus()
    {
        var f = new Fakes { ForegroundPid = 500 };
        await Build(f).ExecuteAsync(Target(500), CancellationToken.None);

        Assert.Equal(1, f.Calls.Count(c => c == "focus:500"));
    }

    [Fact]
    public async Task HoldCancelled_StillReleasesSpace()
    {
        var f = new Fakes { ForegroundPid = 500, HoldThrows = new OperationCanceledException() };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Build(f).ExecuteAsync(Target(500), CancellationToken.None));

        Assert.Contains("up", f.Calls);          // no stuck key on the target, ever
        Assert.Contains("restore:42", f.Calls);
    }

    [Fact]
    public async Task SpaceDownRejected_NeverSendsUp_ReportsInputRejected()
    {
        var f = new Fakes { ForegroundPid = 500, DownOk = false };
        var outcome = await Build(f).ExecuteAsync(Target(500), CancellationToken.None);

        Assert.Equal(GrabOutcome.InputRejected, outcome);
        Assert.DoesNotContain("up", f.Calls);    // nothing went down, nothing to release
        Assert.Contains("restore:42", f.Calls);
    }

    [Fact]
    public async Task VerifyFails_NoKeystrokeEver_StillRestores()
    {
        var f = new Fakes { ForegroundPid = 777 };   // user's window won the race
        var outcome = await Build(f).ExecuteAsync(Target(500), CancellationToken.None);

        Assert.Equal(GrabOutcome.SkippedVerifyFailed, outcome);
        Assert.DoesNotContain("down", f.Calls);      // THE invariant
        Assert.DoesNotContain("up", f.Calls);
        Assert.Contains("restore:42", f.Calls);
    }

    [Fact]
    public async Task FocusFails_SkipsWithoutVerifyOrSpace_StillRestores()
    {
        var f = new Fakes { FocusOk = false };
        var outcome = await Build(f).ExecuteAsync(Target(500), CancellationToken.None);

        Assert.Equal(GrabOutcome.SkippedFocusFailed, outcome);
        Assert.DoesNotContain("down", f.Calls);
        Assert.DoesNotContain("verify", f.Calls);
        Assert.Contains("restore:42", f.Calls);
    }

    [Fact]
    public async Task SpaceUpRejected_ReportsJumpedReleaseFailed()
    {
        var f = new Fakes { ForegroundPid = 500, UpOk = false };
        var outcome = await Build(f).ExecuteAsync(Target(500), CancellationToken.None);

        // The down landed, so the account did register activity, but the release
        // did not go through and Space may still be held on the target. That is
        // the worst state this class can leave behind, so it never stays silent.
        Assert.Equal(GrabOutcome.JumpedReleaseFailed, outcome);
    }
}
