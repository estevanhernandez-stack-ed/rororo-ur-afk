namespace Labs626.UrAfk.Core;

/// <summary>Per-account timing jitter. Timing-only desync: shifts WHEN a jump
/// fires (threshold + jitter), never adds input. Re-rolled after each jump so
/// batch-launched accounts drift apart over cycles.</summary>
public sealed class JitterBook
{
    private readonly IJitterSource _source;
    private readonly Dictionary<string, int> _byAccount = new();

    public JitterBook(IJitterSource source) => _source = source;

    public int GetOrAssign(string accountId)
    {
        if (!_byAccount.TryGetValue(accountId, out var v))
        {
            v = _source.NextJitterSeconds();
            _byAccount[accountId] = v;
        }
        return v;
    }

    public void Reroll(string accountId) => _byAccount[accountId] = _source.NextJitterSeconds();

    public void Forget(string accountId) => _byAccount.Remove(accountId);
}
