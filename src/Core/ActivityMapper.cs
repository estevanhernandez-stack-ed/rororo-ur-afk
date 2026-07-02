using ROROROblox.PluginContract;

namespace Labs626.UrAfk.Core;

public static class ActivityMapper
{
    public static IReadOnlyList<AccountIdleInfo> Map(AccountActivityList list)
        => list.Items
            .Select(i => new AccountIdleInfo(i.AccountId, i.SecondsSinceActivity))
            .ToList();
}
