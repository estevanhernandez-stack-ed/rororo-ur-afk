using Labs626.UrAfk.PluginHost;
using Xunit;

namespace Labs626.UrAfk.Tests;

public class AccountRegistryTests
{
    [Fact]
    public void ResolveByPid_KnownAndUnknown()
    {
        var reg = new AccountRegistry();
        reg.OnLaunched(1234, 99L, "Este", "acct-1");

        var hit = reg.ResolveByPid(1234);
        Assert.NotNull(hit);
        Assert.Equal("acct-1", hit!.AccountId);
        Assert.Null(reg.ResolveByPid(9999));
    }

    [Fact]
    public void OnExited_RemovesAndRaises()
    {
        var reg = new AccountRegistry();
        reg.OnLaunched(1234, 99L, "Este", "acct-1");
        AccountRegistry.AccountInfo? removed = null;
        reg.AccountRemoved += (_, info) => removed = info;

        reg.OnExited(1234);

        Assert.Null(reg.ResolveByPid(1234));
        Assert.Equal("acct-1", removed!.AccountId);
    }
}
