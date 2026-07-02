using ROROROblox.PluginContract;
using Xunit;

namespace Labs626.UrAfk.Tests;

public class ContractSanityTests
{
    [Fact]
    public void ContractTypes_Resolve_IncludingActivityQuery()
    {
        // Proves the ProjectReference delivers the 0.3.0 surface (GetAccountActivity).
        Assert.NotNull(typeof(RoRoRoHost.RoRoRoHostClient));
        Assert.NotNull(typeof(AccountActivityList));
        Assert.NotNull(typeof(AccountActivity));
        Assert.NotNull(typeof(Empty));
    }
}
