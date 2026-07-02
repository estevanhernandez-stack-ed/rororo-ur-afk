namespace Labs626.UrAfk.Core;

public enum GrabOutcome { Jumped, SkippedFocusFailed, SkippedVerifyFailed, InputRejected }

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

    public GrabExecutor(IFocusRestorer restorer, IWindowFocus focus,
        IForegroundPidProbe probe, IKeystrokeSender keys, IDelay delay, TimeSpan settle)
    {
        _restorer = restorer;
        _focus = focus;
        _probe = probe;
        _keys = keys;
        _delay = delay;
        _settle = settle;
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

            return _keys.TapSpace() ? GrabOutcome.Jumped : GrabOutcome.InputRejected;
        }
        finally
        {
            if (previous != 0) _restorer.RestoreForeground(previous);
        }
    }
}
