namespace Labs626.UrAfk.Core;

public static class GrabOutcomeExtensions
{
    /// <summary>True when the Space key-down actually reached the target, i.e. the
    /// account registered activity. Drift and a rejected release are diagnostic
    /// detail on an otherwise successful grab, not reasons to re-fire early, so
    /// every caller that asks "did we keep this account alive?" asks through here
    /// rather than comparing against <see cref="GrabOutcome.Jumped"/> alone. A new
    /// Jumped* variant that forgets to update this is the bug this exists to stop.</summary>
    public static bool IsJump(this GrabOutcome outcome) => outcome
        is GrabOutcome.Jumped
        or GrabOutcome.JumpedAfterDrift
        or GrabOutcome.JumpedReleaseFailed;
}
