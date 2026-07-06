using Labs626.UrAfk.Core;
using Labs626.UrAfk.PluginHost;
using Xunit;

namespace Labs626.UrAfk.Tests;

/// <summary>v0.3: the post-grab confirmation beat and the pre-grab F8 hint.</summary>
public class KeptStateTests
{
    private sealed class FakeQuery : IHostActivityQuery
    {
        public IReadOnlyList<AccountIdleInfo> Next = Array.Empty<AccountIdleInfo>();
        public Task<IReadOnlyList<AccountIdleInfo>> QueryAsync(CancellationToken ct)
            => Task.FromResult(Next);
    }

    private sealed class FakeGrabber : IGrabExecutor
    {
        public GrabOutcome Outcome = GrabOutcome.Jumped;
        public Task<GrabOutcome> ExecuteAsync(DueCandidate target, CancellationToken ct)
            => Task.FromResult(Outcome);
    }

    private sealed class RecordingDelay : IDelay
    {
        public readonly List<TimeSpan> Waits = new();
        public Task Wait(TimeSpan d, CancellationToken ct) { Waits.Add(d); return Task.CompletedTask; }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class ZeroJitter : IJitterSource { public int NextJitterSeconds() => 0; }

    [Fact]
    public async Task Jumped_ShowsKeptConfirmation_HeldThreeSeconds_ThenWatching()
    {
        var pill = new PillController();
        var seen = new List<PillSnapshot>();
        pill.Changed += s => seen.Add(s);

        var registry = new AccountRegistry();
        registry.OnLaunched(100, 1L, "Este", "acct-1");
        var query = new FakeQuery { Next = new[] { new AccountIdleInfo("acct-1", 5000) } };
        var delay = new RecordingDelay();
        var settings = UrAfkSettings.Defaults with
        {
            MasterEnabled = true,
            LeadSeconds = 0,
            EnabledAccountIds = new[] { "acct-1" },
        };
        var svc = new KeepActiveService(query, registry, new JitterBook(new ZeroJitter()),
            new FakeGrabber(), pill, delay, new FixedClock(), () => settings);

        await svc.RunCycleAsync(CancellationToken.None);

        var kept = Assert.Single(seen, s => s.Kind == PillStateKind.Kept);
        Assert.Equal("✓ Kept Este active", kept.Text);
        // The confirmation is held as the post-grab gap (3s, vs the 1s non-jump gap).
        Assert.Contains(TimeSpan.FromSeconds(3), delay.Waits);
        // Cycle still lands back on Watching.
        Assert.Equal(PillStateKind.Watching, pill.Current.Kind);
        // Ordering: Kept comes after Grabbing, before the final Watching.
        var kinds = seen.Select(s => s.Kind).ToList();
        Assert.True(kinds.IndexOf(PillStateKind.Kept) > kinds.IndexOf(PillStateKind.Grabbing));
    }

    [Fact]
    public void PreGrab_MentionsTheSkipKey()
    {
        var pill = new PillController();
        pill.SetPreGrab("Este", 5);
        Assert.Equal(PillStateKind.PreGrab, pill.Current.Kind);
        Assert.Contains("F8 skips", pill.Current.Text);
    }
}
