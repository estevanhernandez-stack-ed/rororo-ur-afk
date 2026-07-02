namespace Labs626.UrAfk.Core;

public interface IWindowFocus { (bool ok, string? error) Focus(int pid); }
public interface IKeystrokeSender { bool TapSpace(); }
public interface IFocusRestorer { nint CaptureForeground(); bool RestoreForeground(nint hwnd); }
public interface IForegroundPidProbe { int GetForegroundPid(); }
public interface IDelay { Task Wait(TimeSpan duration, CancellationToken ct); }
public interface IClock { DateTimeOffset UtcNow { get; } }
