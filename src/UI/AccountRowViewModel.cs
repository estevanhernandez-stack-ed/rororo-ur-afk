using Labs626.UrAfk.Core;

namespace Labs626.UrAfk.UI;

public sealed class AccountRowViewModel : ViewModelBase
{
    private readonly Action<string, bool> _persistEnabled;
    private long? _idleSeconds;
    private DateTimeOffset? _lastKeptAt;
    private bool _enabled;

    public AccountRowViewModel(string accountId, string displayName, bool enabled,
        Action<string, bool> persistEnabled)
    {
        AccountId = accountId;
        DisplayName = displayName;
        _enabled = enabled;
        _persistEnabled = persistEnabled;
    }

    public string AccountId { get; }
    public string DisplayName { get; }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (SetField(ref _enabled, value)) _persistEnabled(AccountId, value);
        }
    }

    public string IdleText => _idleSeconds switch
    {
        null => "—",
        < 60 => $"idle {_idleSeconds}s",
        < 3600 => $"idle {_idleSeconds / 60}m",
        _ => $"idle {_idleSeconds / 3600}h{_idleSeconds % 3600 / 60}m",
    };

    public string LastKeptText => _lastKeptAt is null
        ? ""
        : $"kept active {(int)Math.Max(0, (DateTimeOffset.UtcNow - _lastKeptAt.Value).TotalMinutes)}m ago";

    public void Update(long idleSeconds, DateTimeOffset? lastKeptAt)
    {
        _idleSeconds = idleSeconds;
        _lastKeptAt = lastKeptAt;
        OnPropertyChanged(nameof(IdleText));
        OnPropertyChanged(nameof(LastKeptText));
    }
}
