using Labs626.UrAfk.Core;

namespace Labs626.UrAfk.UI;

public sealed class PillViewModel : ViewModelBase
{
    private readonly Func<UrAfkSettings> _settings;
    private readonly Func<bool>? _getMaster;
    private PillSnapshot _snapshot;

    public PillViewModel(PillController pill, Func<UrAfkSettings> settings,
        Action? requestSkip = null, Func<bool>? getMaster = null, Action<bool>? setMaster = null)
    {
        _settings = settings;
        _getMaster = getMaster;
        SkipCommand = new RelayCommand(() => requestSkip?.Invoke());
        ToggleMasterCommand = new RelayCommand(() =>
        {
            if (getMaster is not null && setMaster is not null) setMaster(!getMaster());
        });
        OpenMainCommand = new RelayCommand(() => OpenMainRequested?.Invoke());
        _snapshot = pill.Current;
        pill.Changed += s => OnUi(() =>
        {
            _snapshot = s;
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(Kind));
            OnPropertyChanged(nameof(Visible));
        });
    }

    public string Text => _snapshot.Text;
    public PillStateKind Kind => _snapshot.Kind;

    // ---------- v0.3: quick controls on the floating pill ----------

    /// <summary>Raised by the pill's expand button; App surfaces the main window.</summary>
    public event Action? OpenMainRequested;

    public System.Windows.Input.ICommand SkipCommand { get; }
    public System.Windows.Input.ICommand ToggleMasterCommand { get; }
    public System.Windows.Input.ICommand OpenMainCommand { get; }

    /// <summary>Mirror of the master toggle for the pill's on/off button visuals.</summary>
    public bool MasterEnabled => _getMaster?.Invoke() ?? false;

    /// <summary>Called by MainViewModel when the master toggle flips, from either surface.</summary>
    public void RefreshMaster() => OnUi(() => OnPropertyChanged(nameof(MasterEnabled)));

    public bool Visible => _settings().PillMode switch
    {
        PillMode.Off => false,
        PillMode.PreGrabOnly => _snapshot.Kind is PillStateKind.PreGrab or PillStateKind.Grabbing,
        _ => true,
    };

    /// <summary>Which screen corner the floating pill window should sit in.
    /// Read live from settings, same pattern as <see cref="Visible"/>.</summary>
    public PillCorner Corner => _settings().PillCorner;

    /// <summary>Called by MainViewModel.Persist after every settings save so
    /// Visible (depends on PillMode) and Corner (depends on PillCorner) refresh
    /// immediately — not just on the next PillController transition.</summary>
    public void NotifySettingsChanged() => OnUi(() =>
    {
        OnPropertyChanged(nameof(Visible));
        OnPropertyChanged(nameof(Corner));
    });
}
