using Labs626.UrAfk.Core;
using Labs626.UrAfk.PluginHost;

namespace Labs626.UrAfk.Win32;

public sealed class ForegroundPidProbe : IForegroundPidProbe
{
    public int GetForegroundPid() => (int)ForegroundWatcher.GetForegroundProcessId();
}
