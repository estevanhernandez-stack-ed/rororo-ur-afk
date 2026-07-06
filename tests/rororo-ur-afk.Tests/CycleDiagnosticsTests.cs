using Labs626.UrAfk.Core;
using Labs626.UrAfk.PluginHost;
using Xunit;

namespace Labs626.UrAfk.Tests;

/// <summary>
/// v0.5.1 diagnostics: every grab outcome and per-cycle due math must reach the
/// log sink — a silent SkippedVerifyFailed loop (Windows foreground-lock while
/// idle) was previously invisible.
/// </summary>
public class CycleDiagnosticsTests
{
    private sealed class FakeQuery : IHostActivityQuery
    {
        public IReadOnlyList<AccountIdleInfo> Next = Array.Empty<AccountIdleInfo>();
        public Task<IReadOnlyList<AccountIdleInfo>> QueryAsync(CancellationToken ct) => Task.FromResult(Next);
    }

    private sealed class OutcomeGrabber : IGrabExecutor
    {
        public GrabOutcome Outcome = GrabOutcome.Jumped;
        public Task<GrabOutcome> ExecuteAsync(DueCandidate target, CancellationToken ct) => Task.FromResult(Outcome);
    }

    private sealed class InstantDelay : IDelay { public Task Wait(TimeSpan d, CancellationToken ct) => Task.CompletedTask; }
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; } = new(2026, 7, 6, 18, 0, 0, TimeSpan.Zero); }
    private sealed class ZeroJitter : IJitterSource { public int NextJitterSeconds() => 0; }

    private static (KeepActiveService svc, OutcomeGrabber grab, List<string> log) Build(GrabOutcome outcome)
    {
        var registry = new AccountRegistry();
        registry.OnLaunched(17880, 1L, "estehernandez", "acct-1");
        var query = new FakeQuery { Next = new[] { new AccountIdleInfo("acct-1", 1560) } }; // 26m idle
        var grab = new OutcomeGrabber { Outcome = outcome };
        var log = new List<string>();
        var settings = UrAfkSettings.Defaults with
        {
            MasterEnabled = true, ThresholdMinutes = 10, LeadSeconds = 0,
            EnabledAccountIds = new[] { "acct-1" },
        };
        var svc = new KeepActiveService(query, registry, new JitterBook(new ZeroJitter()),
            grab, new PillController(), new InstantDelay(), new FixedClock(), () => settings,
            log: log.Add);
        return (svc, grab, log);
    }

    [Fact]
    public async Task SilentFailure_IsNowLoggedWithOutcomeAndPid()
    {
        var (svc, _, log) = Build(GrabOutcome.SkippedVerifyFailed);

        await svc.RunCycleAsync(CancellationToken.None);

        // The cycle math is visible...
        Assert.Contains(log, l => l.Contains("cycle:") && l.Contains("estehernandez")
                                   && l.Contains("pid=17880") && l.Contains("due=Y"));
        // ...and the previously-invisible failed grab is named with its pid.
        Assert.Contains(log, l => l == "grab estehernandez pid=17880 -> SkippedVerifyFailed");
    }

    [Fact]
    public async Task SuccessfulGrab_AlsoLogsOutcome()
    {
        var (svc, _, log) = Build(GrabOutcome.Jumped);
        await svc.RunCycleAsync(CancellationToken.None);
        Assert.Contains(log, l => l == "grab estehernandez pid=17880 -> Jumped");
    }

    [Fact]
    public async Task NotDue_LogsCycleButNoGrabLine()
    {
        var (svc, _, log) = Build(GrabOutcome.Jumped);
        // Re-point to a fresh-idle account: not due, so no grab attempt.
        await new KeepActiveServiceHarness().RunNotDue(log);
        Assert.DoesNotContain(log, l => l.StartsWith("grab "));
    }

    private sealed class KeepActiveServiceHarness
    {
        public async Task RunNotDue(List<string> log)
        {
            var registry = new AccountRegistry();
            registry.OnLaunched(100, 1L, "est", "acct-1");
            var query = new FakeQuery { Next = new[] { new AccountIdleInfo("acct-1", 30) } }; // 30s idle
            var settings = UrAfkSettings.Defaults with
            {
                MasterEnabled = true, ThresholdMinutes = 10, LeadSeconds = 0,
                EnabledAccountIds = new[] { "acct-1" },
            };
            var svc = new KeepActiveService(query, registry, new JitterBook(new ZeroJitter()),
                new OutcomeGrabber(), new PillController(), new InstantDelay(), new FixedClock(),
                () => settings, log: log.Add);
            await svc.RunCycleAsync(CancellationToken.None);
            Assert.Contains(log, l => l.Contains("cycle:") && l.Contains("due=n"));
        }
    }
}
