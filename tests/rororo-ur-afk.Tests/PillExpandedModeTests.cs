using System.IO;
using Labs626.UrAfk.Core;
using Labs626.UrAfk.UI;
using Xunit;

namespace Labs626.UrAfk.Tests;

/// <summary>v0.5: click-to-expand, pin persistence, and the next-grab estimate.</summary>
public class PillExpandedModeTests
{
    private static UrAfkSettings Settings(bool pinned = false, bool master = true, int thresholdMin = 10)
        => UrAfkSettings.Defaults with
        {
            MasterEnabled = master,
            ThresholdMinutes = thresholdMin,
            PillPinnedExpanded = pinned,
        };

    [Fact]
    public void Expanded_AddsNextDueEstimate_CollapsedDoesNot()
    {
        var settings = Settings();
        var vm = new PillViewModel(new PillController(), () => settings,
            getMaster: () => settings.MasterEnabled);

        vm.UpdateStats(480, 2); // 8m idle, threshold 10m → due in ~2m
        Assert.Equal("idle 8m · 2 grabs", vm.Stats);

        vm.SetExpanded(true);
        Assert.Equal("idle 8m · 2 grabs · next ~2m", vm.Stats);

        vm.SetExpanded(false);
        Assert.Equal("idle 8m · 2 grabs", vm.Stats);
    }

    [Fact]
    public void Expanded_PastThreshold_SaysDueNow()
    {
        var settings = Settings();
        var vm = new PillViewModel(new PillController(), () => settings,
            getMaster: () => settings.MasterEnabled);
        vm.SetExpanded(true);
        vm.UpdateStats(660, 1); // 11m idle vs 10m threshold
        Assert.EndsWith("due now", vm.Stats);
    }

    [Fact]
    public void NextDue_HiddenWhenMasterOff()
    {
        var settings = Settings(master: false);
        var vm = new PillViewModel(new PillController(), () => settings,
            getMaster: () => settings.MasterEnabled);
        vm.SetExpanded(true);
        vm.UpdateStats(480, 0);
        Assert.DoesNotContain("next", vm.Stats);
    }

    [Fact]
    public void PinnedSetting_StartsExpanded_AndPinCommandPersists()
    {
        var settings = Settings(pinned: true);
        var vm = new PillViewModel(new PillController(), () => settings,
            getMaster: () => settings.MasterEnabled);
        Assert.True(vm.IsExpanded);

        // Unpin persists false; expanded state stays until the user collapses it.
        bool? saved = null;
        var vm2 = new PillViewModel(new PillController(), () => settings,
            getMaster: () => settings.MasterEnabled,
            savePinned: p => saved = p);
        vm2.TogglePinCommand.Execute(null);
        Assert.False(saved);
    }

    [Fact]
    public void PinnedExpanded_RoundTripsThroughSettingsJson()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urafk-pin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var store = new SettingsStore(Path.Combine(dir, "settings.json"));
        try
        {
            store.Save(UrAfkSettings.Defaults with { PillPinnedExpanded = true });
            Assert.True(store.Load().PillPinnedExpanded);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
