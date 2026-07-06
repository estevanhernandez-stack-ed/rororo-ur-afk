using System.IO;
using Grpc.Core;
using Labs626.UrAfk.PluginHost;

namespace Labs626.UrAfk.Core;

public sealed record AccountStatus(string AccountId, string DisplayName,
    long SecondsSinceActivity, DateTimeOffset? LastKeptAt);

/// <summary>The engine: poll GetAccountActivity, compute the due set, act on it
/// sequentially with a pre-grab countdown, re-roll jitter after each jump.
/// GetAccountActivity is the single source of truth — no local idle timers.</summary>
public sealed class KeepActiveService
{
    private readonly IHostActivityQuery _query;
    private readonly AccountRegistry _registry;
    private readonly JitterBook _jitter;
    private readonly IGrabExecutor _grabber;
    private readonly PillController _pill;
    private readonly IDelay _delay;
    private readonly IClock _clock;
    private readonly Func<UrAfkSettings> _settings;
    private readonly Dictionary<string, DateTimeOffset> _lastKeptAt = new();
    private int _skipRequested; // 0/1; consumed by the countdown in progress

    public event Action<IReadOnlyList<AccountStatus>>? StatusUpdated;
    /// <summary>A grab landed; payload is the account's display name.</summary>
    public event Action<string>? GrabHappened;

    public KeepActiveService(IHostActivityQuery query, AccountRegistry registry,
        JitterBook jitter, IGrabExecutor grabber, PillController pill,
        IDelay delay, IClock clock, Func<UrAfkSettings> settings)
    {
        _query = query;
        _registry = registry;
        _jitter = jitter;
        _grabber = grabber;
        _pill = pill;
        _delay = delay;
        _clock = clock;
        _settings = settings;
    }

    public void RequestSkip() => Interlocked.Exchange(ref _skipRequested, 1);

    public async Task RunCycleAsync(CancellationToken ct)
    {
        var settings = _settings();

        IReadOnlyList<AccountIdleInfo> idle;
        try
        {
            idle = await _query.QueryAsync(ct).ConfigureAwait(false);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.PermissionDenied)
        {
            _pill.SetConsentRevoked();
            return;
        }
        catch (Exception ex) when (ex is RpcException or IOException)
        {
            _pill.SetDisconnected();
            return;
        }

        // Join idle info to live pids; publish readouts regardless of master state.
        var running = new Dictionary<string, AccountRegistry.AccountInfo>();
        foreach (var a in _registry.Snapshot()) running[a.AccountId] = a; // last-wins; never throws on a transient duplicate AccountId (relaunch race)
        var candidates = idle
            .Where(i => running.ContainsKey(i.AccountId))
            .Select(i => new DueCandidate(i.AccountId, running[i.AccountId].DisplayName,
                running[i.AccountId].Pid, EffectiveIdleSeconds(i)))
            .ToList();

        StatusUpdated?.Invoke(candidates
            .Select(c => new AccountStatus(c.AccountId, c.DisplayName, c.SecondsSinceActivity,
                _lastKeptAt.TryGetValue(c.AccountId, out var at) ? at : null))
            .ToList());

        if (!settings.MasterEnabled)
        {
            _pill.SetOff();
            return;
        }

        _pill.SetWatching(candidates.Count);

        var due = DueSetCalculator.Compute(candidates,
            settings.EnabledAccountIds.ToHashSet(), settings.ThresholdMinutes * 60, _jitter);

        foreach (var target in due)
        {
            ct.ThrowIfCancellationRequested();

            if (await CountdownSkippedAsync(target, settings.LeadSeconds, ct).ConfigureAwait(false))
                continue;   // this account skipped this cycle; next account proceeds

            _pill.SetGrabbing(target.DisplayName);
            var outcome = await _grabber.ExecuteAsync(target, ct).ConfigureAwait(false);
            if (outcome == GrabOutcome.Jumped)
            {
                _jitter.Reroll(target.AccountId);
                _lastKeptAt[target.AccountId] = _clock.UtcNow;
                GrabHappened?.Invoke(target.DisplayName);
                // Confirmation beat: hold the ✓ state as the inter-account gap so
                // the fire stays visible after the ~1s grab itself.
                _pill.SetKept(target.DisplayName);
                await _delay.Wait(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
            }
            else
            {
                await _delay.Wait(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false); // inter-account gap
            }
        }

        _pill.SetWatching(candidates.Count);
    }

    /// <summary>
    /// The host only credits activity to the account whose window is FOREGROUND
    /// when its sampler ticks — and a grab flips focus for barely a second, so
    /// the synthetic Space usually goes uncredited and the host's idle clock
    /// keeps climbing right through a successful grab. Left alone, that means
    /// re-firing every poll cycle forever (observed live: fire at 10m, again at
    /// 11m). We know we just kept the account alive, so our own grab acts as a
    /// local activity floor: effective idle = min(host idle, time since our
    /// last successful grab). Real user input still wins (host value smaller).
    /// </summary>
    private long EffectiveIdleSeconds(AccountIdleInfo info)
    {
        if (!_lastKeptAt.TryGetValue(info.AccountId, out var kept)) return info.SecondsSinceActivity;
        var sinceKept = (long)(_clock.UtcNow - kept).TotalSeconds;
        if (sinceKept < 0) sinceKept = 0;
        return Math.Min(info.SecondsSinceActivity, sinceKept);
    }

    /// <summary>Render the pre-grab countdown; true when the user skipped.</summary>
    private async Task<bool> CountdownSkippedAsync(DueCandidate target, int leadSeconds, CancellationToken ct)
    {
        for (var remaining = leadSeconds; remaining > 0; remaining--)
        {
            _pill.SetPreGrab(target.DisplayName, remaining);
            if (Interlocked.Exchange(ref _skipRequested, 0) == 1) return true;
            await _delay.Wait(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        }
        // Consume a skip that landed on the final tick.
        return Interlocked.Exchange(ref _skipRequested, 0) == 1 && leadSeconds > 0;
    }

    public async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RunCycleAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch { /* one bad cycle never kills the loop */ }
            try { await _delay.Wait(TimeSpan.FromSeconds(_settings().PollSeconds), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }
}
