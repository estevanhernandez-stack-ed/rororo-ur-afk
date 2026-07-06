namespace Labs626.UrAfk.Core;

/// <summary>The pill's single source of truth. UI (header pill + floating pill)
/// binds to Changed/Current; KeepActiveService drives the transitions.</summary>
public sealed class PillController
{
    public event Action<PillSnapshot>? Changed;

    public PillSnapshot Current { get; private set; } = new(PillStateKind.Off, "Keep-active off");

    private void Set(PillStateKind kind, string text)
    {
        Current = new PillSnapshot(kind, text);
        Changed?.Invoke(Current);
    }

    public void SetOff() => Set(PillStateKind.Off, "Keep-active off");

    public void SetWatching(int accountCount)
        => Set(PillStateKind.Watching,
            $"Active · watching {accountCount} account{(accountCount == 1 ? "" : "s")}");

    public void SetPreGrab(string displayName, int secondsLeft)
        => Set(PillStateKind.PreGrab, $"Grabbing {displayName} in {secondsLeft}… · F8 skips");

    public void SetGrabbing(string displayName)
        => Set(PillStateKind.Grabbing, $"Keeping {displayName} active…");

    /// <summary>Post-grab confirmation beat — held ~3s by KeepActiveService so
    /// the fire is visible even if you only glance at the pill afterwards.</summary>
    public void SetKept(string displayName)
        => Set(PillStateKind.Kept, $"✓ Kept {displayName} active");

    public void SetDisconnected()
        => Set(PillStateKind.Disconnected, "Disconnected — is RoRoRo running?");

    public void SetConsentRevoked()
        => Set(PillStateKind.ConsentRevoked, "Consent revoked — re-grant in RoRoRo → Plugins");
}
