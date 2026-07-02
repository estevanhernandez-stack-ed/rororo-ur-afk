using Labs626.UrAfk.Core;
using Xunit;
using System;
using System.IO;

namespace Labs626.UrAfk.Tests;

public class SettingsStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"urafk-settings-{Guid.NewGuid():N}.json");

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        var s = new SettingsStore(TempPath()).Load();
        Assert.False(s.MasterEnabled);
        Assert.Equal(15, s.ThresholdMinutes);
        Assert.Equal(60, s.PollSeconds);
        Assert.Equal(90, s.JitterMaxSeconds);
        Assert.Equal(3, s.LeadSeconds);
        Assert.Equal(PillMode.Always, s.PillMode);
        Assert.False(s.SoundOnGrab);
        Assert.Equal(0x77u, s.SkipHotkeyVk);
        Assert.Empty(s.EnabledAccountIds);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var path = TempPath();
        try
        {
            var store = new SettingsStore(path);
            store.Save(store.Load() with
            {
                MasterEnabled = true,
                ThresholdMinutes = 12,
                EnabledAccountIds = new[] { "acct-1", "acct-2" },
            });

            var loaded = new SettingsStore(path).Load();
            Assert.True(loaded.MasterEnabled);
            Assert.Equal(12, loaded.ThresholdMinutes);
            Assert.Equal(2, loaded.EnabledAccountIds.Count);
            Assert.Contains("acct-1", loaded.EnabledAccountIds);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, "{ not json ][");
            var s = new SettingsStore(path).Load();
            Assert.False(s.MasterEnabled);
            Assert.Equal(15, s.ThresholdMinutes);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Load_ClampsBadValues()
    {
        var path = TempPath();
        try
        {
            var store = new SettingsStore(path);
            store.Save(store.Load() with { ThresholdMinutes = 0, LeadSeconds = 99, PollSeconds = -5 });
            var s = new SettingsStore(path).Load();
            Assert.Equal(15, s.ThresholdMinutes);  // non-positive → default
            Assert.Equal(10, s.LeadSeconds);       // clamp to 0..10
            Assert.Equal(60, s.PollSeconds);       // non-positive → default
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
