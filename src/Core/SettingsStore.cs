using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Labs626.UrAfk.Core;

/// <summary>JSON settings at %LOCALAPPDATA%\626labs.ur-afk\settings.json. Missing or
/// corrupt file yields defaults; out-of-range values are clamped on load so a
/// hand-edited file can't produce a zero threshold or a negative poll.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public SettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "626labs.ur-afk", "settings.json");
    }

    public UrAfkSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return UrAfkSettings.Defaults;
            var loaded = JsonSerializer.Deserialize<UrAfkSettings>(
                File.ReadAllText(_filePath), JsonOptions);
            return loaded is null ? UrAfkSettings.Defaults : Clamp(loaded);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return UrAfkSettings.Defaults;
        }
    }

    public void Save(UrAfkSettings settings)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(tmp, _filePath, overwrite: true);
    }

    private static UrAfkSettings Clamp(UrAfkSettings s)
    {
        var d = UrAfkSettings.Defaults;
        return s with
        {
            ThresholdMinutes = s.ThresholdMinutes <= 0 ? d.ThresholdMinutes : s.ThresholdMinutes,
            PollSeconds = s.PollSeconds <= 0 ? d.PollSeconds : s.PollSeconds,
            JitterMaxSeconds = s.JitterMaxSeconds < 0 ? d.JitterMaxSeconds : s.JitterMaxSeconds,
            LeadSeconds = Math.Clamp(s.LeadSeconds, 0, 10),
            EnabledAccountIds = s.EnabledAccountIds ?? Array.Empty<string>(),
        };
    }
}
