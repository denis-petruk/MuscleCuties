using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Infrastructure.Profile.HealthSync;
using MuscleCuties.UseCases.Features.Profile.HealthSync;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

namespace MuscleCuties.Core.Tests;

public sealed class ProfileHealthSyncStoreTests
{
    [Fact]
    public async Task AppleHealthSummary_Strips_NonStep_Metrics_Before_Caching()
    {
        var provider = new TestProvider
        {
            Summary = new HealthWeeklySummary(
                HealthDataSource.AppleHealth,
                DateTime.Today.AddDays(-7),
                DateTime.Today,
                8400,
                7.6,
                76,
                58,
                0,
                DateTime.UtcNow,
                9100,
                7.8,
                420,
                7.7)
        };
        var healthSync = new HealthSyncService([provider], new MemoryTokenStorage());
        var result = await healthSync.SyncAsync(55, HealthDataSource.AppleHealth);
        var status = await healthSync.GetStatusAsync(55);

        Assert.True(result.IsConnected);
        Assert.Contains("8,400 avg steps", status.SummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sleep", status.SummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RHR", status.SummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("active", status.SummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Summary);
        Assert.False(result.Summary.HasSleepData);
        Assert.Equal(0, result.Summary.RestingHeartRate);
        Assert.Null(result.Summary.AverageActiveEnergyKcal);

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentProfileUserIdProvider>(new StubCurrentProfileUserIdProvider(55));
        services.AddSingleton<IHealthSyncService>(healthSync);
        using var providerScope = services.BuildServiceProvider();

        var store = new LocalProfileHealthSyncStore(providerScope.GetRequiredService<IServiceScopeFactory>());
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal("Refresh steps", loaded.ConnectionBadgeText);
        Assert.True(loaded.ReadinessScore > 0);
        Assert.Contains("Synced steps", loaded.ReadinessExplanationText, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubCurrentProfileUserIdProvider(int userId) : ICurrentProfileUserIdProvider
    {
        public Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(userId);
    }

    private sealed class TestProvider : IHealthDataProvider, IHealthAuthorizationProvider
    {
        public bool Authorized { get; set; } = true;

        public HealthWeeklySummary? Summary { get; set; }

        public HealthDataSource Source => HealthDataSource.AppleHealth;

        public string DisplayName => "Apple Health";

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> RequestReadAuthorizationAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Authorized);

        public Task<HealthWeeklySummary?> ReadWeeklySummaryAsync(DateTime today,
            CancellationToken cancellationToken = default) => Task.FromResult(Summary);
    }

    private sealed class MemoryTokenStorage : ITokenStorage
    {
        private readonly Dictionary<string, string> _values = new();

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
