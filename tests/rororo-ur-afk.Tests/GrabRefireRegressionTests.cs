using Labs626.UrAfk.Core;
using Labs626.UrAfk.PluginHost;
using Xunit;

namespace Labs626.UrAfk.Tests;

/// <summary>
/// Regression for the re-fire bug observed live on v0.3.0: the host only
/// credits activity to the FOREGROUND account at sampler ticks, so a grab's
/// sub-second focus flip usually goes uncredited and the host idle keeps
/// climbing through a successful grab — fire at 10m, fire again at 11m,
/// forever. The service's own grab now acts as a local activity floor.
/// </summary>
public class GrabRefireRegressionTests
{
    private sealed class FakeQuery : IHostActivityQuery
    {
        public IReadOnlyList<AccountIdleInfo> Next = Array.Empty<AccountIdleInfo>();
        public Task<IReadOnlyList<AccountIdleInfo>> QueryAsync(CancellationToken ct)
            => Task.FromResult(Next);
    }

    private sealed class FakeGrabber : IGrabExecutor
    {
        public readonly List<string> Grabbed = new();
        public Task<GrabOutcome> ExecuteAsync(DueCandidate target, CancellationToken ct)
        { Grabbed.Add(target.AccountId); return Task.FromResult(GrabOutcome.Jumped); }
    }

    private sealed class InstantDelay : IDelay
    {
        public Task Wait(TimeSpan d, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class MutableClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class ZeroJitter : IJitterSource { public int NextJitterSeconds() => 0; }

    private static (KeepActiveService svc, FakeQuery query, FakeGrabber grabber, MutableClock clock, PillController pill)
        Build()
    {
        var registry = new AccountRegistry();
        registry.OnLaunched(100, 1L, "Este", "acct-1");
        var query = new FakeQuery();
        var grabber = new FakeGrabber();
        var clock = new MutableClock();
        var pill = new PillController();
        var settings = UrAfkSettings.Defaults with
        {
            MasterEnabled = true,
            ThresholdMinutes = 10,
            LeadSeconds = 0,
            EnabledAccountIds = new[] { "acct-1" },
        };
        var svc = new KeepActiveService(query, registry, new JitterBook(new ZeroJitter()),
            grabber, pill, new InstantDelay(), clock, () => settings);
        return (svc, query, grabber, clock, pill);
    }

    [Fact]
    public async Task HostIdleNeverResets_GrabDoesNotRefireNextCycle()
    {
        var (svc, query, grabber, clock, _) = Build();

        // Cycle 1: 10 minutes idle — due, grabs.
        query.Next = new[] { new AccountIdleInfo("acct-1", 600) };
        await svc.RunCycleAsync(CancellationToken.None);
        Assert.Single(grabber.Grabbed);

        // Cycle 2, one poll later: the host never credited our Space, so it
        // reports 11 minutes. Our grab 60s ago floors the effective idle — no re-fire.
        clock.UtcNow = clock.UtcNow.AddSeconds(60);
        query.Next = new[] { new AccountIdleInfo("acct-1", 660) };
        await svc.RunCycleAsync(CancellationToken.None);
        Assert.Single(grabber.Grabbed);
    }

    [Fact]
    public async Task AfterFloorExpires_AccountBecomesDueAgain()
    {
        var (svc, query, grabber, clock, _) = Build();

        query.Next = new[] { new AccountIdleInfo("acct-1", 600) };
        await svc.RunCycleAsync(CancellationToken.None);
        Assert.Single(grabber.Grabbed);

        // 10+ minutes after OUR grab, with the host still claiming huge idle:
        // the floor itself has aged past the threshold — grab again. This is
        // the legitimate steady-state cadence (one grab per threshold window).
        clock.UtcNow = clock.UtcNow.AddSeconds(601);
        query.Next = new[] { new AccountIdleInfo("acct-1", 1300) };
        await svc.RunCycleAsync(CancellationToken.None);
        Assert.Equal(2, grabber.Grabbed.Count);
    }

    [Fact]
    public async Task RealUserInput_StillWins_WhenHostValueIsSmaller()
    {
        var (svc, query, grabber, clock, _) = Build();

        query.Next = new[] { new AccountIdleInfo("acct-1", 600) };
        await svc.RunCycleAsync(CancellationToken.None);
        Assert.Single(grabber.Grabbed);

        // The user actually played: host reports tiny idle. min() takes the
        // host's value — nothing due, and readouts show the fresh number.
        clock.UtcNow = clock.UtcNow.AddSeconds(60);
        query.Next = new[] { new AccountIdleInfo("acct-1", 5) };
        IReadOnlyList<AccountStatus>? statuses = null;
        svc.StatusUpdated += s => statuses = s;
        await svc.RunCycleAsync(CancellationToken.None);

        Assert.Single(grabber.Grabbed);
        Assert.Equal(5, statuses!.Single().SecondsSinceActivity);
    }

    [Fact]
    public async Task Readouts_ShowFlooredIdle_AfterGrab()
    {
        var (svc, query, grabber, clock, _) = Build();

        query.Next = new[] { new AccountIdleInfo("acct-1", 600) };
        await svc.RunCycleAsync(CancellationToken.None);

        // Rows and pill stats must not claim "idle 11m" right after we kept it
        // alive — the floor applies to readouts too.
        clock.UtcNow = clock.UtcNow.AddSeconds(60);
        query.Next = new[] { new AccountIdleInfo("acct-1", 660) };
        IReadOnlyList<AccountStatus>? statuses = null;
        svc.StatusUpdated += s => statuses = s;
        await svc.RunCycleAsync(CancellationToken.None);

        Assert.Equal(60, statuses!.Single().SecondsSinceActivity);
    }
}
