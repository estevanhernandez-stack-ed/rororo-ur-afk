using System.Collections.ObjectModel;
using Labs626.UrAfk.Core;
using Labs626.UrAfk.PluginHost;

namespace Labs626.UrAfk.UI;

public sealed class MainViewModel : ViewModelBase
{
    private readonly SettingsStore _store;
    private readonly PillController _pill;
    private UrAfkSettings _settings;
    private string _footerText = "Connecting…";
    private long? _lastMaxIdle;
    private int _fires;

    public MainViewModel(KeepActiveService service, PillController pill,
        AccountRegistry registry, SettingsStore store)
    {
        _store = store;
        _pill = pill;
        _settings = store.Load();
        Pill = new PillViewModel(pill, () => _settings,
            requestSkip: service.RequestSkip,
            getMaster: () => _settings.MasterEnabled,
            setMaster: v => MasterEnabled = v,
            savePosition: (x, y) => Persist(_settings with { PillX = x, PillY = y }));

        service.GrabHappened += _ => { _fires++; Pill.UpdateStats(_lastMaxIdle, _fires); };

        foreach (var a in registry.Snapshot()) AddRow(a);
        registry.AccountAdded += (_, a) => OnUi(() => AddRow(a));
        registry.AccountRemoved += (_, a) => OnUi(() =>
        {
            var row = Accounts.FirstOrDefault(r => r.AccountId == a.AccountId);
            if (row is not null) Accounts.Remove(row);
        });

        service.StatusUpdated += statuses => OnUi(() =>
        {
            foreach (var s in statuses)
                Accounts.FirstOrDefault(r => r.AccountId == s.AccountId)
                    ?.Update(s.SecondsSinceActivity, s.LastKeptAt);

            // Pill stats: worst idle across the enabled set (all, when none enabled).
            var enabled = _settings.EnabledAccountIds;
            var pool = statuses.Where(s => enabled.Count == 0 || enabled.Contains(s.AccountId)).ToList();
            _lastMaxIdle = pool.Count > 0 ? pool.Max(s => s.SecondsSinceActivity) : null;
            Pill.UpdateStats(_lastMaxIdle, _fires);
        });
    }

    public ObservableCollection<AccountRowViewModel> Accounts { get; } = new();
    public PillViewModel Pill { get; }

    /// <summary>The settings snapshot KeepActiveService reads each cycle.</summary>
    public UrAfkSettings CurrentSettings => _settings;

    public bool MasterEnabled
    {
        get => _settings.MasterEnabled;
        set
        {
            Persist(_settings with { MasterEnabled = value });
            OnPropertyChanged();
            Pill.RefreshMaster();
            // Instant pill feedback — the next poll cycle can be seconds away,
            // and a toggle that answers nothing feels dead. Connection-error
            // states stay untouched; the loop owns those.
            if (_pill.Current.Kind is not (PillStateKind.Disconnected or PillStateKind.ConsentRevoked))
            {
                if (value) _pill.SetWatching(Accounts.Count);
                else _pill.SetOff();
            }
        }
    }

    public int ThresholdMinutes
    {
        get => _settings.ThresholdMinutes;
        set { Persist(_settings with { ThresholdMinutes = value }); OnPropertyChanged(); }
    }

    public int LeadSeconds
    {
        get => _settings.LeadSeconds;
        set { Persist(_settings with { LeadSeconds = value }); OnPropertyChanged(); }
    }

    public PillMode PillModeSetting
    {
        get => _settings.PillMode;
        set { Persist(_settings with { PillMode = value }); OnPropertyChanged(); }
    }

    public PillCorner PillCornerSetting
    {
        get => _settings.PillCorner;
        // Picking a corner snaps the pill back — it clears any dragged position.
        set { Persist(_settings with { PillCorner = value, PillX = null, PillY = null }); OnPropertyChanged(); }
    }

    public double PillSizeSetting
    {
        get => _settings.PillScale;
        set { Persist(_settings with { PillScale = value }); OnPropertyChanged(); }
    }

    public bool SoundOnGrab
    {
        get => _settings.SoundOnGrab;
        set { Persist(_settings with { SoundOnGrab = value }); OnPropertyChanged(); }
    }

    public string FooterText
    {
        get => _footerText;
        set => SetField(ref _footerText, value);
    }

    private void AddRow(AccountRegistry.AccountInfo a)
        => Accounts.Add(new AccountRowViewModel(a.AccountId, a.DisplayName,
            _settings.EnabledAccountIds.Contains(a.AccountId), PersistRowEnabled));

    private void PersistRowEnabled(string accountId, bool enabled)
    {
        var ids = _settings.EnabledAccountIds.ToHashSet();
        if (enabled) ids.Add(accountId); else ids.Remove(accountId);
        Persist(_settings with { EnabledAccountIds = ids });
    }

    private void Persist(UrAfkSettings next)
    {
        _settings = next;
        _store.Save(next);
        Pill.NotifySettingsChanged();
    }
}
