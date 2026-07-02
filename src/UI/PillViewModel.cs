using Labs626.UrAfk.Core;

namespace Labs626.UrAfk.UI;

public sealed class PillViewModel : ViewModelBase
{
    private readonly Func<UrAfkSettings> _settings;
    private PillSnapshot _snapshot;

    public PillViewModel(PillController pill, Func<UrAfkSettings> settings)
    {
        _settings = settings;
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
