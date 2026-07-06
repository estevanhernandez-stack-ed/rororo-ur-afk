using Labs626.UrAfk.Core;

namespace Labs626.UrAfk.UI;

public sealed class PillViewModel : ViewModelBase
{
    private readonly Func<UrAfkSettings> _settings;
    private readonly Func<bool>? _getMaster;
    private readonly Action<double, double>? _savePosition;
    private readonly Action<double>? _saveScale;
    private readonly Action<bool>? _savePinned;
    private PillSnapshot _snapshot;
    private string _stats = string.Empty;
    private bool _isExpanded;
    private double? _scalePreview;
    private long? _lastMaxIdle;
    private int _lastFires;

    public PillViewModel(PillController pill, Func<UrAfkSettings> settings,
        Action? requestSkip = null, Func<bool>? getMaster = null, Action<bool>? setMaster = null,
        Action<double, double>? savePosition = null,
        Action<double>? saveScale = null, Action<bool>? savePinned = null)
    {
        _settings = settings;
        _getMaster = getMaster;
        _savePosition = savePosition;
        _saveScale = saveScale;
        _savePinned = savePinned;
        _isExpanded = settings().PillPinnedExpanded;
        TogglePinCommand = new RelayCommand(() =>
        {
            var pinned = !Pinned;
            _savePinned?.Invoke(pinned);
            if (pinned && !_isExpanded) SetExpanded(true);
            OnPropertyChanged(nameof(Pinned));
        });
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

    // ---------- v0.4: size, drag position, stats ----------

    public double Scale => _scalePreview ?? _settings().PillScale;

    /// <summary>Live value while the resize grip drags; persisted on release.</summary>
    public void PreviewScale(double scale)
    {
        _scalePreview = Math.Clamp(scale, 0.75, 2.0);
        OnPropertyChanged(nameof(Scale));
    }

    public void CommitScale()
    {
        if (_scalePreview is not { } final) return;
        _scalePreview = null;
        _saveScale?.Invoke(final);
    }

    // ---------- v0.5: expanded mode + pin ----------

    /// <summary>Click the pill body to keep the quick controls out (and gain the
    /// next-grab estimate). Session-scoped unless pinned.</summary>
    public bool IsExpanded => _isExpanded;

    public bool Pinned => _settings().PillPinnedExpanded;

    public System.Windows.Input.ICommand TogglePinCommand { get; private set; } = null!;

    public void SetExpanded(bool expanded) => OnUi(() =>
    {
        if (_isExpanded == expanded) return;
        _isExpanded = expanded;
        OnPropertyChanged(nameof(IsExpanded));
        ComposeStats(); // the next-grab estimate rides expanded mode
    });

    public void ToggleExpanded() => SetExpanded(!_isExpanded);

    public double? X => _settings().PillX;
    public double? Y => _settings().PillY;
    public bool HasCustomPosition => X is not null && Y is not null;

    /// <summary>Persist a drag-end position; the corner preset stays overridden
    /// until the user picks a corner in settings again.</summary>
    public void SavePosition(double x, double y) => _savePosition?.Invoke(x, y);

    /// <summary>"idle 9m · 4 grabs" line under the state text; empty = hidden.</summary>
    public string Stats
    {
        get => _stats;
        private set
        {
            if (_stats == value) return;
            _stats = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Called by MainViewModel from StatusUpdated / GrabHappened.</summary>
    public void UpdateStats(long? maxIdleSeconds, int fires) => OnUi(() =>
    {
        _lastMaxIdle = maxIdleSeconds;
        _lastFires = fires;
        ComposeStats();
    });

    private void ComposeStats()
    {
        var parts = new List<string>(3);
        if (_lastMaxIdle is { } idle) parts.Add($"idle {FormatIdle(idle)}");
        if (_lastFires > 0 || parts.Count > 0) parts.Add($"{_lastFires} grab{(_lastFires == 1 ? "" : "s")}");
        if (_isExpanded && NextDueSeconds(_lastMaxIdle) is { } next)
            parts.Add(next <= 0 ? "due now" : $"next ~{FormatIdle(next)}");
        Stats = string.Join(" · ", parts);
    }

    /// <summary>Rough time until the soonest account crosses the threshold —
    /// jitter deliberately excluded, hence the ~. Null when off/unknown.</summary>
    internal long? NextDueSeconds(long? maxIdleSeconds)
    {
        if (maxIdleSeconds is not { } idle) return null;
        if (_getMaster?.Invoke() != true) return null;
        return _settings().ThresholdMinutes * 60L - idle;
    }

    internal static string FormatIdle(long seconds) => seconds switch
    {
        < 60 => $"{seconds}s",
        < 3600 => $"{seconds / 60}m",
        _ => $"{seconds / 3600}h {seconds % 3600 / 60}m",
    };

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
        OnPropertyChanged(nameof(Scale));
    });
}
