using Labs626.UrAfk.Core;
using Labs626.UrAfk.UI;
using Xunit;

namespace Labs626.UrAfk.Tests;

public class PillViewModelTests
{
    [Fact]
    public void FollowsControllerAndHonorsPillMode()
    {
        var pill = new PillController();
        var settings = UrAfkSettings.Defaults with { PillMode = PillMode.PreGrabOnly };
        var vm = new PillViewModel(pill, () => settings);

        pill.SetWatching(3);
        Assert.Equal("Active · watching 3 accounts", vm.Text);
        Assert.False(vm.Visible);                     // PreGrabOnly hides Watching

        pill.SetPreGrab("Este", 3);
        Assert.True(vm.Visible);                      // PreGrab always shows (unless Off)
        Assert.Equal(PillStateKind.PreGrab, vm.Kind);

        settings = settings with { PillMode = PillMode.Off };
        pill.SetPreGrab("Este", 2);
        Assert.False(vm.Visible);                     // Off hides everything
    }
}
