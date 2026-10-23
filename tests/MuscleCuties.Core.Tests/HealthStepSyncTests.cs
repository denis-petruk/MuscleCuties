using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Health;

namespace MuscleCuties.Core.Tests;

public sealed class HealthStepSyncTests
{
    private const int FirstAccountId = 41;
    private const int SecondAccountId = 42;
    private const int SampleAverageSteps = 6400;
    private const int SampleYesterdaySteps = 7100;

    [Fact]
    public async Task Denied_Authorization_Does_Not_Read_Or_Cache_Steps()
    {
        var provider = new StepProvider { AuthorizationCompleted = false, Summary = SampleSummary() };
        var storage = new MemoryStorage();
        using var service = new HealthSyncService([provider], storage);

        var result = await service.SyncAsync(FirstAccountId, HealthDataSource.AppleHealth);

        Assert.False(result.IsConnected);
        Assert.False(provider.ReadCalled);
        Assert.False((await service.GetStatusAsync(FirstAccountId)).IsConnected);
        Assert.Null(await service.GetCachedWeeklySummaryAsync(FirstAccountId));
    }

    [Fact]
    public async Task No_Data_Leaves_Connection_Disconnected()
    {
        var provider = new StepProvider();
        using var service = new HealthSyncService([provider], new MemoryStorage());

        var result = await service.SyncAsync(FirstAccountId, HealthDataSource.AppleHealth);

        Assert.False(result.IsConnected);
        Assert.True(provider.ReadCalled);
        Assert.Null(await service.GetCachedWeeklySummaryAsync(FirstAccountId));
    }

    [Fact]
    public async Task Revoked_Read_Access_Purges_Previously_Cached_Steps()
    {
        var provider = new StepProvider { Summary = SampleSummary() };
        using var service = new HealthSyncService([provider], new MemoryStorage());
        Assert.True((await service.SyncAsync(FirstAccountId, HealthDataSource.AppleHealth)).IsConnected);

        // HealthKit does not reveal a read denial; it returns no readable steps.
        provider.Summary = null;
        var result = await service.SyncAsync(FirstAccountId, HealthDataSource.AppleHealth);

        Assert.False(result.IsConnected);
        Assert.False((await service.GetStatusAsync(FirstAccountId)).IsConnected);
        Assert.Null(await service.GetCachedWeeklySummaryAsync(FirstAccountId));
    }

    [Fact]
    public async Task Disconnect_Removes_Only_Selected_Accounts_Cache_And_State()
    {
        var provider = new StepProvider { Summary = SampleSummary() };
        var storage = new MemoryStorage();
        using var service = new HealthSyncService([provider], storage);
        await service.SyncAsync(FirstAccountId, HealthDataSource.AppleHealth);
        await service.SyncAsync(SecondAccountId, HealthDataSource.AppleHealth);

        await service.DisconnectAsync(FirstAccountId);

        Assert.False((await service.GetStatusAsync(FirstAccountId)).IsConnected);
        Assert.Null(await service.GetCachedWeeklySummaryAsync(FirstAccountId));
        Assert.True((await service.GetStatusAsync(SecondAccountId)).IsConnected);
        Assert.NotNull(await service.GetCachedWeeklySummaryAsync(SecondAccountId));
        Assert.DoesNotContain(storage.Keys, key => key.EndsWith($"_{FirstAccountId}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Old_Multi_Metric_Cache_Is_Discarded_Before_Use()
    {
        var storage = new MemoryStorage();
        await storage.SetAsync($"health_sync_state_{FirstAccountId}",
            "{\"selectedSource\":0,\"isConnected\":true,\"promptDismissed\":true}");
        await storage.SetAsync($"health_sync_weekly_summary_{FirstAccountId}", "legacy");
        using var service = new HealthSyncService([new StepProvider()], storage);

        Assert.False((await service.GetStatusAsync(FirstAccountId)).IsConnected);
        Assert.Null(await service.GetCachedWeeklySummaryAsync(FirstAccountId));
        Assert.Empty(storage.Keys);
    }

    private static HealthWeeklySummary SampleSummary() =>
        new(HealthDataSource.AppleHealth, DateTime.Today.AddDays(-6), DateTime.Today,
            SampleAverageSteps, 0, 0, 0, 0, DateTime.UtcNow, SampleYesterdaySteps);

    private sealed class StepProvider : IHealthDataProvider, IHealthAuthorizationProvider
    {
        public bool AuthorizationCompleted { get; set; } = true;
        public bool ReadCalled { get; private set; }
        public HealthWeeklySummary? Summary { get; set; }
        public HealthDataSource Source => HealthDataSource.AppleHealth;
        public string DisplayName => "Apple Health";
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> RequestReadAuthorizationAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(AuthorizationCompleted);
        public Task<HealthWeeklySummary?> ReadWeeklySummaryAsync(DateTime today,
            CancellationToken cancellationToken = default)
        {
            ReadCalled = true;
            return Task.FromResult(Summary);
        }
    }

    private sealed class MemoryStorage : ITokenStorage
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
        public IEnumerable<string> Keys => _values.Keys;
        public Task<string?> GetAsync(string key) => Task.FromResult(_values.GetValueOrDefault(key));
        public Task SetAsync(string key, string value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }
        public void Remove(string key) => _values.Remove(key);
        public void RemoveAll() => _values.Clear();
    }
}
