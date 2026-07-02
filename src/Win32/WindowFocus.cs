using Labs626.UrAfk.Core;
using Labs626.UrAfk.PluginHost;

namespace Labs626.UrAfk.Win32;

public sealed class WindowFocus : IWindowFocus
{
    public (bool ok, string? error) Focus(int pid) => Win32Focus.AttachAndFocus(pid);
}
