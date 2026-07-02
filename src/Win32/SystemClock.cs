using Labs626.UrAfk.Core;

namespace Labs626.UrAfk.Win32;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
