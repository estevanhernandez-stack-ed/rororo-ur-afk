using Labs626.UrAfk.Core;

namespace Labs626.UrAfk.Win32;

public sealed class SystemDelay : IDelay
{
    public Task Wait(TimeSpan duration, CancellationToken ct) => Task.Delay(duration, ct);
}
