using Labs626.UrAfk.Core;
using ROROROblox.PluginContract;
using Xunit;

namespace Labs626.UrAfk.Tests;

public class ActivityMapperTests
{
    [Fact]
    public void Map_ProjectsIdAndSeconds()
    {
        var list = new AccountActivityList();
        list.Items.Add(new AccountActivity
        {
            AccountId = "acct-1",
            LastActivityUnixMs = 1_700_000_000_000,
            SecondsSinceActivity = 300,
        });

        var mapped = ActivityMapper.Map(list);

        var item = Assert.Single(mapped);
        Assert.Equal("acct-1", item.AccountId);
        Assert.Equal(300, item.SecondsSinceActivity);
    }

    [Fact]
    public void Map_Empty_ReturnsEmpty()
    {
        Assert.Empty(ActivityMapper.Map(new AccountActivityList()));
    }
}
