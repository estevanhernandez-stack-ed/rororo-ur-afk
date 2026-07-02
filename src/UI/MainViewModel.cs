using System.Collections.ObjectModel;
using Labs626.UrAfk.Core;
using Labs626.UrAfk.PluginHost;

namespace Labs626.UrAfk.UI;

public sealed class MainViewModel : ViewModelBase
{
    private readonly SettingsStore _store;
    private UrAfkSettings _settings;
    private string _footerText = "Connecting…";

    public MainViewModel(KeepActiveService service, PillController pill,
        AccountRegistry registry, SettingsStore store)
    {
        _store = store;
        _settings = store.Load();
        Pill = new PillViewModel(pill, () => _settings);

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
        });
    }

    public ObservableCollection<AccountRowViewModel> Accounts { get; } = new();
    public PillViewModel Pill { get; }

    /// <summary>The settings snapshot KeepActiveService reads each cycle.</summary>
    public UrAfkSettings CurrentSettings => _settings;

    public bool MasterEnabled
    {
        get => _settings.MasterEnabled;
        set { Persist(_settings with { MasterEnabled = value }); OnPropertyChanged(); }
    }

    public int ThresholdMinutes
    {
        get => _settings.ThresholdMinutes;
        set { Persist(_settings with { ThresholdMinutes = value }); OnPropertyChanged(); }
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
    }
}
