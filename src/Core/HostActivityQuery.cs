using Labs626.UrAfk.PluginHost;

namespace Labs626.UrAfk.Core;

/// <summary>Thin adapter: PluginClient's gRPC call → domain records. All logic
/// lives in ActivityMapper (pure, tested); this class is transport glue.</summary>
public sealed class HostActivityQuery : IHostActivityQuery
{
    private readonly PluginClient _client;
    internal HostActivityQuery(PluginClient client) => _client = client;

    public async Task<IReadOnlyList<AccountIdleInfo>> QueryAsync(CancellationToken ct)
        => ActivityMapper.Map(await _client.GetAccountActivityAsync(ct).ConfigureAwait(false));
}
