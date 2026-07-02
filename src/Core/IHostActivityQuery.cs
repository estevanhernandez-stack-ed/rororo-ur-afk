namespace Labs626.UrAfk.Core;

/// <summary>The keep-active loop's only view of the host's idle data.
/// Implementations may throw Grpc.Core.RpcException (PermissionDenied on
/// revoked consent; Unavailable/Internal when the host is gone).</summary>
public interface IHostActivityQuery
{
    Task<IReadOnlyList<AccountIdleInfo>> QueryAsync(CancellationToken ct);
}
