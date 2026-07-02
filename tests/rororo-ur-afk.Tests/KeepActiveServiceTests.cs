using Grpc.Core;
using Labs626.UrAfk.Core;
using Labs626.UrAfk.PluginHost;
using Xunit;

namespace Labs626.UrAfk.Tests;

public class KeepActiveServiceTests
{
    private sealed class FakeQuery : IHostActivityQuery
    {
        public IReadOnlyList<AccountIdleInfo> Next = Array.Empty<AccountIdleInfo>();
        public Exception? Throw;
        public Task<IReadOnlyList<AccountIdleInfo>> QueryAsync(CancellationToken ct)
            => Throw is null ? Task.FromResult(Next) : Task.FromException<IReadOnlyList<AccountIdleInfo>>(Throw);
    }

    private sealed class FakeGrabber : IGrabExecutor
    {
        public readonly List<string> Grabbed = new();
        public GrabOutcome Outcome = GrabOutcome.Jumped;
        public Task<GrabOutcome> ExecuteAsync(DueCandidate target, CancellationToken ct)
        { Grabbed.Add(target.AccountId); return Task.FromResult(Outcome); }
    }

    private sealed class InstantDelay : IDelay
    {
        public Task Wait(TimeSpan d, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class ZeroJitter : IJitterSource { public int NextJitterSeconds() => 0; }

    private sealed class Harness
    {
        public readonly FakeQuery Query = new();
        public readonly AccountRegistry Registry = new();
        public readonly JitterBook Jitter = new(new ZeroJitter());
        public readonly FakeGrabber Grabber = new();
        public readonly PillController Pill = new();
        public readonly FixedClock Clock = new();
        public UrAfkSettings Settings = UrAfkSettings.Defaults with
        {
            MasterEnabled = true,
            LeadSeconds = 0,   // most tests skip the countdown
            EnabledAccountIds = new[] { "acct-1", "acct-2" },
        };

        public KeepActiveService Build() => new(
            Query, Registry, Jitter, Grabber, Pill,
            new InstantDelay(), Clock, () => Settings);
    }

    [Fact]
    public async Task MasterOff_PollsForReadouts_ButNeverActs()
    {
        var h = new Harness();
        h.Settings = h.Settings with { MasterEnabled = false };
        h.Registry.OnLaunched(100, 1L, "One", "acct-1");
        h.Query.Next = new[] { new AccountIdleInfo("acct-1", 5000) };

        IReadOnlyList<AccountStatus>? statuses = null;
        var svc = h.Build();
        svc.StatusUpdated += s => statuses = s;

        await svc.RunCycleAsync(CancellationToken.None);

        Assert.NotNull(statuses);                    // readouts still flow
        Assert.Empty(h.Grabber.Grabbed);             // but no acting
        Assert.Equal(PillStateKind.Off, h.Pill.Current.Kind);
    }

    [Fact]
    public async Task DueAccounts_GrabbedSequentially_MostIdleFirst_JitterRerolled()
    {
        var h = new Harness();
        h.Registry.OnLaunched(100, 1L, "One", "acct-1");
        h.Registry.OnLaunched(200, 2L, "Two", "acct-2");
        h.Query.Next = new[]
        {
            new AccountIdleInfo("acct-1", 1000),
            new AccountIdleInfo("acct-2", 2000),
        };

        await h.Build().RunCycleAsync(CancellationToken.None);

        Assert.Equal(new[] { "acct-2", "acct-1" }, h.Grabber.Grabbed);
        Assert.Equal(PillStateKind.Watching, h.Pill.Current.Kind);
    }

    [Fact]
    public async Task AccountWithoutLivePid_IsNotActedOn()
    {
        var h = new Harness();
        h.Registry.OnLaunched(100, 1L, "One", "acct-1");   // acct-2 not running
        h.Query.Next = new[]
        {
            new AccountIdleInfo("acct-1", 1000),
            new AccountIdleInfo("acct-2", 9000),
        };

        await h.Build().RunCycleAsync(CancellationToken.None);

        Assert.Equal(new[] { "acct-1" }, h.Grabber.Grabbed);
    }

    [Fact]
    public async Task PermissionDenied_SetsConsentRevoked_NoActing()
    {
        var h = new Harness();
        h.Query.Throw = new RpcException(new Status(StatusCode.PermissionDenied, "revoked"));

        await h.Build().RunCycleAsync(CancellationToken.None);

        Assert.Equal(PillStateKind.ConsentRevoked, h.Pill.Current.Kind);
        Assert.Empty(h.Grabber.Grabbed);
    }

    [Fact]
    public async Task HostUnavailable_SetsDisconnected()
    {
        var h = new Harness();
        h.Query.Throw = new RpcException(new Status(StatusCode.Unavailable, "pipe gone"));

        await h.Build().RunCycleAsync(CancellationToken.None);

        Assert.Equal(PillStateKind.Disconnected, h.Pill.Current.Kind);
    }

    [Fact]
    public async Task Skip_DuringCountdown_SkipsThatAccount_NextStillProcessed()
    {
        var h = new Harness();
        h.Settings = h.Settings with { LeadSeconds = 2 };
        h.Registry.OnLaunched(100, 1L, "One", "acct-1");
        h.Registry.OnLaunched(200, 2L, "Two", "acct-2");
        h.Query.Next = new[]
        {
            new AccountIdleInfo("acct-1", 2000),
            new AccountIdleInfo("acct-2", 1000),
        };

        var svc = h.Build();
        // request the skip as soon as the first countdown tick renders
        svc.RequestSkip();

        await svc.RunCycleAsync(CancellationToken.None);

        Assert.Equal(new[] { "acct-2" }, h.Grabber.Grabbed);   // acct-1 skipped
    }

    [Fact]
    public async Task DuplicateAccountId_TwoLivePids_DoesNotThrow_ActsOnce()
    {
        var h = new Harness();
        // relaunch race: old pid's exit event hasn't landed, so two live pids share one AccountId
        h.Registry.OnLaunched(100, 1L, "One", "acct-1");
        h.Registry.OnLaunched(200, 1L, "One", "acct-1");
        h.Query.Next = new[] { new AccountIdleInfo("acct-1", 5000) };

        await h.Build().RunCycleAsync(CancellationToken.None); // must NOT throw

        Assert.Single(h.Grabber.Grabbed);
        Assert.Equal("acct-1", h.Grabber.Grabbed[0]);
    }

    [Fact]
    public async Task Jumped_RecordsLastKeptAt()
    {
        var h = new Harness();
        h.Registry.OnLaunched(100, 1L, "One", "acct-1");
        h.Query.Next = new[] { new AccountIdleInfo("acct-1", 1000) };

        IReadOnlyList<AccountStatus>? statuses = null;
        var svc = h.Build();
        svc.StatusUpdated += s => statuses = s;

        await svc.RunCycleAsync(CancellationToken.None);   // grab happens
        h.Query.Next = new[] { new AccountIdleInfo("acct-1", 5) };
        await svc.RunCycleAsync(CancellationToken.None);   // next poll reports it

        var row = Assert.Single(statuses!);
        Assert.Equal(h.Clock.UtcNow, row.LastKeptAt);
    }
}
