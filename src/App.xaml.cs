using System.Windows;
using System.Windows.Threading;
using Labs626.UrAfk.Core;
using Labs626.UrAfk.Diagnostics;
using Labs626.UrAfk.Theming;
using Labs626.UrAfk.PluginHost;
using Labs626.UrAfk.UI;
using Labs626.UrAfk.Win32;

namespace Labs626.UrAfk;

public partial class App : Application
{
    private const string PluginId = "626labs.ur-afk";

    private PluginClient? _client;
    private KeepActiveService? _service;
    private CancellationTokenSource? _loopCts;
    private MainWindow? _window;
    private FloatingPillWindow? _floatingPill;
    private TrayService? _tray;
    private SkipHotkeyService? _hotkey;
    private HostThemeService? _theme;
    private StartupWatchdog? _watchdog;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Evidence layer first — handlers, session header, and watchdog exist
        // before any construction step can crash or hang.
        RegisterExceptionEvidence();

        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "?";
        DiagLog.Write($"=== RoRoRo Ur AFK v{version} starting — pid {Environment.ProcessId}, " +
                      $"{Environment.OSVersion.VersionString}, .NET {Environment.Version} ===");
        _watchdog = new StartupWatchdog();

        // Manual-verify hook. Checked only when the variable is set.
        var testCrash = Environment.GetEnvironmentVariable("URAFK_TEST_CRASH");
        if (testCrash == "hang")
        {
            DiagLog.Write("URAFK_TEST_CRASH=hang — blocking OnStartup deliberately");
            Thread.Sleep(Timeout.Infinite); // windowless hang; the watchdog reports it
        }

        // Sync brushes to the RoRoRo host's active theme before any window
        // resolves resources, then keep following theme switches live.
        DiagLog.Write("startup: theme sync");
        _theme = new HostThemeService();
        _theme.Start();

        DiagLog.Write("startup: settings + registry");
        var store = new SettingsStore();
        var settings = store.Load();
        var registry = new AccountRegistry();
        var pill = new PillController();
        _client = new PluginClient(PluginId, registry);

        var grabber = new GrabExecutor(new FocusRestorer(), new WindowFocus(),
            new ForegroundPidProbe(), new KeystrokeSender(), new SystemDelay(),
            settle: TimeSpan.FromSeconds(1));

        // vm forward-reference: KeepActiveService needs a live-settings accessor at
        // construction time, but that accessor must read the VM (not the load-time
        // snapshot) once the VM exists. Declare vm first (definitely-assigned to
        // null) so the settings lambda can close over it, THEN construct the VM as
        // a plain assignment — `var vm = new MainViewModel(new KeepActiveService(...,
        // () => vm...` doesn't compile: vm can't appear in its own declaration's
        // initializer, even transitively through a closure.
        MainViewModel? vm = null;

        DiagLog.Write("startup: keep-active service");
        _service = new KeepActiveService(
            new HostActivityQuery(_client), registry,
            new JitterBook(new RandomJitterSource(settings.JitterMaxSeconds)),
            grabber, pill, new SystemDelay(), new Win32.SystemClock(),
            () => vm?.CurrentSettings ?? settings);

        vm = new MainViewModel(_service, pill, registry, store);

        _service.GrabHappened += name =>
        {
            // Evidence: "did it actually fire overnight?" is now answerable from the log.
            DiagLog.Write($"grab fired: kept {name} active");
            if ((vm?.CurrentSettings ?? settings).SoundOnGrab) System.Media.SystemSounds.Exclamation.Play();
        };

        DiagLog.Write("startup: hotkey");
        _hotkey = new SkipHotkeyService(settings.SkipHotkeyVk);
        _hotkey.SkipPressed += () => _service.RequestSkip();
        _hotkey.Start();

        DiagLog.Write("startup: windows + tray");
        _window = new MainWindow { DataContext = vm };
        _floatingPill = new FloatingPillWindow { DataContext = vm.Pill };
        _tray = new TrayService(_window, vm);
        vm.Pill.OpenMainRequested += () => Dispatcher.Invoke(() =>
        {
            _window.Show();
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
            _window.Activate();
        });

        _client.HostLost += () =>
        {
            DiagLog.Write("host connection lost — pill set to disconnected");
            pill.SetDisconnected();
        };

        _window.Show();
        _floatingPill.Show();

        DiagLog.Write("startup: connect loop dispatched");
        _loopCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                await _client.ConnectAsync(_loopCts.Token);
                DiagLog.Write($"connected — RoRoRo {_client.HostVersion}");
                Dispatcher.Invoke(() => vm.FooterText = $"Connected — RoRoRo {_client.HostVersion}");
                await _service.RunLoopAsync(_loopCts.Token);
            }
            catch (Exception ex)
            {
                DiagLog.Write($"connect/run loop failed: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    vm.FooterText = $"Not connected: {ex.Message}";
                    pill.SetDisconnected();
                });
            }
        });

        if (testCrash == "dispatcher")
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) =>
                throw new InvalidOperationException("URAFK_TEST_CRASH=dispatcher — deliberate test crash");
            timer.Start();
        }

        DiagLog.Write("startup: complete");
        _watchdog.MarkComplete();
    }

    /// <summary>
    /// Log-then-crash-loud evidence handlers (host philosophy: silent crash is
    /// worse than loud crash — never set Handled, just leave a trace). Handlers
    /// alone can't see liveness bugs — that's the StartupWatchdog's job.
    /// </summary>
    private void RegisterExceptionEvidence()
    {
        DispatcherUnhandledException += (_, args) =>
            DiagLog.Write($"FATAL (dispatcher): {args.Exception}");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            DiagLog.Write($"FATAL (appdomain, terminating={args.IsTerminating}): {args.ExceptionObject}");

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            DiagLog.Write($"UNOBSERVED task exception: {args.Exception}");
            args.SetObserved(); // behavior-preserving; evidence only
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _theme?.Dispose(); } catch { }
        try { _loopCts?.Cancel(); } catch { }
        try { _hotkey?.Dispose(); } catch { }
        try { _tray?.Dispose(); } catch { }
        try { _client?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3)); } catch { }
        // Absence of this line at the end of a session = crash or hang, not exit.
        DiagLog.Write($"exiting cleanly (code {e.ApplicationExitCode})");
        base.OnExit(e);
    }
}
