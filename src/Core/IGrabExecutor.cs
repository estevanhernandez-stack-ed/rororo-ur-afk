namespace Labs626.UrAfk.Core;

public interface IGrabExecutor
{
    Task<GrabOutcome> ExecuteAsync(DueCandidate target, CancellationToken ct);
}
