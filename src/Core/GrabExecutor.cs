namespace Labs626.UrAfk.Core;

public enum GrabOutcome
{
    Jumped, JumpedAfterDrift, JumpedReleaseFailed,   // the Space landed
    SkippedFocusFailed, SkippedVerifyFailed, InputRejected
}

/// <summary>One grab: capture the user's foreground, focus the target, settle,
/// VERIFY the foreground actually flipped to the target pid, tap Space, restore.
///
/// THE SAFETY INVARIANT (spec §6): input is never synthesized unless the
/// verified foreground window belongs to the target account's pid. A skipped
/// jump self-heals next cycle; a stray Space into the user's own app is never
/// acceptable. The user's window is restored on every path after capture.</summary>
public sealed class GrabExecutor : IGrabExecutor
{
    private readonly IFocusRestorer _restorer;
    private readonly IWindowFocus _focus;
    private readonly IForegroundPidProbe _probe;
    private readonly IKeystrokeSender _keys;
    private readonly IDelay _delay;
    private readonly TimeSpan _settle;
    private readonly TimeSpan _hold;

    public GrabExecutor(IFocusRestorer restorer, IWindowFocus focus,
        IForegroundPidProbe probe, IKeystrokeSender keys, IDelay delay, TimeSpan settle,
        TimeSpan hold)
    {
        _restorer = restorer;
        _focus = focus;
        _probe = probe;
        _keys = keys;
        _delay = delay;
        _settle = settle;
        _hold = hold;
    }

    public async Task<GrabOutcome> ExecuteAsync(DueCandidate target, CancellationToken ct)
    {
        var previous = _restorer.CaptureForeground();
        try
        {
            var (ok, _) = _focus.Focus(target.Pid);
            if (!ok) return GrabOutcome.SkippedFocusFailed;

            await _delay.Wait(_settle, ct).ConfigureAwait(false);

            if (_probe.GetForegroundPid() != target.Pid)
                return GrabOutcome.SkippedVerifyFailed;   // invariant: no keystroke

            if (!_keys.SpaceDown()) return GrabOutcome.InputRejected;

            // From here the target is holding Space and MUST get its key-up, on
            // every path including cancellation, hence the finally.
            var drifted = false;
            var released = true;
            try
            {
                await _delay.Wait(_hold, ct).ConfigureAwait(false);
            }
            finally
            {
                // The hold is the one stretch of wall time where the foreground can
                // move out from under a key we already pressed. Releasing blind would
                // send the up to whatever stole focus and leave Space stuck down on
                // the target, so re-verify and reclaim the window before releasing.
                drifted = _probe.GetForegroundPid() != target.Pid;
                if (drifted) _focus.Focus(target.Pid);
                released = _keys.SpaceUp();
            }

            // A rejected release is worse than a wandering foreground: the target
            // may still be holding Space. It outranks drift in the report.
            if (!released) return GrabOutcome.JumpedReleaseFailed;
            return drifted ? GrabOutcome.JumpedAfterDrift : GrabOutcome.Jumped;
        }
        finally
        {
            if (previous != 0) _restorer.RestoreForeground(previous);
        }
    }
}
