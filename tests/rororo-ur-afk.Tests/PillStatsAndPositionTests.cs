using System.IO;
using System.Text.Json;
using Labs626.UrAfk.Core;
using Labs626.UrAfk.UI;
using Xunit;

namespace Labs626.UrAfk.Tests;

/// <summary>v0.4: pill stats line, idle formatting, and settings back-compat
/// for the new scale/position fields.</summary>
public class PillStatsAndPositionTests
{
    [Theory]
    [InlineData(46, "46s")]
    [InlineData(59, "59s")]
    [InlineData(60, "1m")]
    [InlineData(540, "9m")]
    [InlineData(3599, "59m")]
    [InlineData(3600, "1h 0m")]
    [InlineData(7380, "2h 3m")]
    public void FormatIdle_Humanizes(long seconds, string expected)
        => Assert.Equal(expected, PillViewModel.FormatIdle(seconds));

    [Fact]
    public void UpdateStats_ComposesLine_AndHidesWhenEmpty()
    {
        var vm = new PillViewModel(new PillController(), () => UrAfkSettings.Defaults);

        vm.UpdateStats(540, 4);
        Assert.Equal("idle 9m · 4 grabs", vm.Stats);

        vm.UpdateStats(46, 1);
        Assert.Equal("idle 46s · 1 grab", vm.Stats);

        vm.UpdateStats(null, 0);
        Assert.Equal(string.Empty, vm.Stats);

        vm.UpdateStats(null, 2);
        Assert.Equal("2 grabs", vm.Stats);
    }

    [Fact]
    public void OldSettingsJson_LoadsWithScaleAndPositionDefaults()
    {
        // A v0.3 settings file has none of the new fields — defaults must apply.
        const string v03Json = """
        {
          "masterEnabled": true,
          "thresholdMinutes": 13,
          "pollSeconds": 60,
          "jitterMaxSeconds": 90,
          "leadSeconds": 3,
          "pillMode": "Always",
          "pillCorner": "BottomRight",
          "soundOnGrab": false,
          "skipHotkeyVk": 119,
          "enabledAccountIds": []
        }
        """;
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };
        var s = JsonSerializer.Deserialize<UrAfkSettings>(v03Json, opts);

        Assert.NotNull(s);
        Assert.Equal(1.0, s!.PillScale);
        Assert.Null(s.PillX);
        Assert.Null(s.PillY);
    }

    [Fact]
    public void ScaleAndPosition_RoundTripThroughStore()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urafk-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var store = new SettingsStore(Path.Combine(dir, "settings.json"));
        try
        {
            store.Save(UrAfkSettings.Defaults with { PillScale = 1.5, PillX = 120.5, PillY = 44.0 });
            var back = store.Load();
            Assert.Equal(1.5, back.PillScale);
            Assert.Equal(120.5, back.PillX);
            Assert.Equal(44.0, back.PillY);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
