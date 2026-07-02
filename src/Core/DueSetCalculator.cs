namespace Labs626.UrAfk.Core;

public readonly record struct DueCandidate(string AccountId, string DisplayName, int Pid, long SecondsSinceActivity);

public static class DueSetCalculator
{
    /// <summary>Enabled accounts whose idle meets threshold + their jitter,
    /// most-idle first (closest to the ~20-min timeout gets jumped first).</summary>
    public static IReadOnlyList<DueCandidate> Compute(
        IReadOnlyList<DueCandidate> candidates,
        IReadOnlySet<string> enabledAccountIds,
        int thresholdSeconds,
        JitterBook jitter)
        => candidates
            .Where(c => enabledAccountIds.Contains(c.AccountId))
            .Where(c => c.SecondsSinceActivity >= thresholdSeconds + jitter.GetOrAssign(c.AccountId))
            .OrderByDescending(c => c.SecondsSinceActivity)
            .ToList();
}
