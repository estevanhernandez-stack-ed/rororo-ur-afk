namespace Labs626.UrAfk.Core;

public enum PillMode { Always, PreGrabOnly, Off }
public enum PillCorner { TopLeft, TopRight, BottomLeft, BottomRight }

public sealed record UrAfkSettings(
    bool MasterEnabled,
    int ThresholdMinutes,
    int PollSeconds,
    int JitterMaxSeconds,
    int LeadSeconds,
    PillMode PillMode,
    PillCorner PillCorner,
    bool SoundOnGrab,
    uint SkipHotkeyVk,
    IReadOnlyCollection<string> EnabledAccountIds)
{
    public static UrAfkSettings Defaults => new(
        MasterEnabled: false,
        ThresholdMinutes: 15,
        PollSeconds: 60,
        JitterMaxSeconds: 90,
        LeadSeconds: 3,
        PillMode: PillMode.Always,
        PillCorner: PillCorner.BottomRight,
        SoundOnGrab: false,
        SkipHotkeyVk: 0x77, // F8
        EnabledAccountIds: Array.Empty<string>());
}
