using Labs626.UrAfk.Core;
using Xunit;

namespace Labs626.UrAfk.Tests;

public class PillControllerTests
{
    [Fact]
    public void Transitions_ProduceExactCopy()
    {
        var pill = new PillController();
        var seen = new List<PillSnapshot>();
        pill.Changed += s => seen.Add(s);

        pill.SetWatching(6);
        pill.SetPreGrab("Este", 3);
        pill.SetPreGrab("Este", 2);
        pill.SetGrabbing("Este");
        pill.SetWatching(1);
        pill.SetOff();

        Assert.Equal("Active · watching 6 accounts", seen[0].Text);
        Assert.Equal(PillStateKind.Watching, seen[0].Kind);
        Assert.Equal("Grabbing Este in 3…", seen[1].Text);
        Assert.Equal("Grabbing Este in 2…", seen[2].Text);
        Assert.Equal(PillStateKind.PreGrab, seen[2].Kind);
        Assert.Equal("Keeping Este active…", seen[3].Text);
        Assert.Equal(PillStateKind.Grabbing, seen[3].Kind);
        Assert.Equal("Active · watching 1 account", seen[4].Text);
        Assert.Equal("Keep-active off", seen[5].Text);
    }

    [Fact]
    public void DisconnectedAndConsent_States()
    {
        var pill = new PillController();
        pill.SetDisconnected();
        Assert.Equal(PillStateKind.Disconnected, pill.Current.Kind);
        Assert.Equal("Disconnected — is RoRoRo running?", pill.Current.Text);

        pill.SetConsentRevoked();
        Assert.Equal("Consent revoked — re-grant in RoRoRo → Plugins", pill.Current.Text);
    }
}
