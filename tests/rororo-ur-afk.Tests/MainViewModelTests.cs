using System.IO;
using Labs626.UrAfk.Core;
using Labs626.UrAfk.PluginHost;
using Labs626.UrAfk.UI;
using Xunit;

namespace Labs626.UrAfk.Tests;

public class MainViewModelTests
{
    private sealed class NullQuery : IHostActivityQuery
    {
        public Task<IReadOnlyList<AccountIdleInfo>> QueryAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AccountIdleInfo>>(Array.Empty<AccountIdleInfo>());
    }
    private sealed class NullGrabber : IGrabExecutor
    {
        public Task<GrabOutcome> ExecuteAsync(DueCandidate t, CancellationToken ct)
            => Task.FromResult(GrabOutcome.Jumped);
    }
    private sealed class InstantDelay : IDelay
    {
        public Task Wait(TimeSpan d, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch; }
    private sealed class ZeroJitter : IJitterSource { public int NextJitterSeconds() => 0; }

    private static (MainViewModel vm, AccountRegistry reg, SettingsStore store, string path) Build()
    {
        var path = Path.Combine(Path.GetTempPath(), $"urafk-vm-{Guid.NewGuid():N}.json");
        var store = new SettingsStore(path);
        var reg = new AccountRegistry();
        var pill = new PillController();
        var svc = new KeepActiveService(new NullQuery(), reg, new JitterBook(new ZeroJitter()),
            new NullGrabber(), pill, new InstantDelay(), new FixedClock(), () => store.Load());
        return (new MainViewModel(svc, pill, reg, store), reg, store, path);
    }

    [Fact]
    public void Rows_FollowRegistryAddRemove()
    {
        var (vm, reg, _, path) = Build();
        try
        {
            reg.OnLaunched(100, 1L, "One", "acct-1");
            reg.OnLaunched(200, 2L, "Two", "acct-2");
            Assert.Equal(2, vm.Accounts.Count);

            reg.OnExited(100);
            Assert.Single(vm.Accounts);
            Assert.Equal("acct-2", vm.Accounts[0].AccountId);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void MasterToggle_PersistsToSettings()
    {
        var (vm, _, store, path) = Build();
        try
        {
            vm.MasterEnabled = true;
            Assert.True(store.Load().MasterEnabled);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void RowEnabledToggle_PersistsAccountId()
    {
        var (vm, reg, store, path) = Build();
        try
        {
            reg.OnLaunched(100, 1L, "One", "acct-1");
            vm.Accounts[0].Enabled = true;
            Assert.Contains("acct-1", store.Load().EnabledAccountIds);

            vm.Accounts[0].Enabled = false;
            Assert.DoesNotContain("acct-1", store.Load().EnabledAccountIds);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
