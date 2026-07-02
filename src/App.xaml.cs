using System.Windows;
using Labs626.UrAfk.Core;
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

        _service = new KeepActiveService(
            new HostActivityQuery(_client), registry,
            new JitterBook(new RandomJitterSource(settings.JitterMaxSeconds)),
            grabber, pill, new SystemDelay(), new Win32.SystemClock(),
            () => vm?.CurrentSettings ?? settings);

        vm = new MainViewModel(_service, pill, registry, store);

        _service.GrabHappened += () =>
        {
            if ((vm?.CurrentSettings ?? settings).SoundOnGrab) System.Media.SystemSounds.Exclamation.Play();
        };

        _hotkey = new SkipHotkeyService(settings.SkipHotkeyVk);
        _hotkey.SkipPressed += () => _service.RequestSkip();
        _hotkey.Start();

        _window = new MainWindow { DataContext = vm };
        _floatingPill = new FloatingPillWindow { DataContext = vm.Pill };
        _tray = new TrayService(_window, vm);

        _client.HostLost += () => pill.SetDisconnected();

        _window.Show();
        _floatingPill.Show();

        _loopCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                await _client.ConnectAsync(_loopCts.Token);
                Dispatcher.Invoke(() => vm.FooterText = $"Connected — RoRoRo {_client.HostVersion}");
                await _service.RunLoopAsync(_loopCts.Token);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    vm.FooterText = $"Not connected: {ex.Message}";
                    pill.SetDisconnected();
                });
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _loopCts?.Cancel(); } catch { }
        try { _hotkey?.Dispose(); } catch { }
        try { _tray?.Dispose(); } catch { }
        try { _client?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3)); } catch { }
        base.OnExit(e);
    }
}
