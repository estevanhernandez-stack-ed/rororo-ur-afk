using System;
using System.Linq;
using Labs626.UrAfk.Core;
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

    [Fact]
    public void EveryJumpedOutcome_IsRecognisedAsAJump()
    {
        // GrabOutcomeExtensions.IsJump is the single place that answers "did the
        // account stay alive?". Adding a Jumped* variant and forgetting to list it
        // there would silently score a successful grab as a failed cycle, skipping
        // the jitter reroll and re-firing early. This closes that door by name.
        var missed = Enum.GetValues<GrabOutcome>()
            .Where(o => o.ToString().StartsWith("Jumped", StringComparison.Ordinal))
            .Where(o => !o.IsJump())
            .ToArray();

        Assert.Empty(missed);
    }
}
