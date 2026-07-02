using Labs626.UrAfk.Win32;
using Xunit;

namespace Labs626.UrAfk.Tests;

public class KeystrokeStructTests
{
    [Fact]
    public void InputStructSize_MatchesWin32Canonical40Bytes_OnX64()
    {
        // ur-task shipped a keep-alive that silently no-oped through v0.2.2
        // because a trimmed union made cbSize 32 instead of 40. Lock it.
        Assert.Equal(40, KeystrokeSender.InputStructSize);
    }
}
