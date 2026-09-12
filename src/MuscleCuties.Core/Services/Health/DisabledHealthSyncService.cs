namespace MuscleCuties.Core.Services.Health;

public sealed class DisabledHealthSyncService : IHealthSyncService
{
    public HealthDataSource DeviceHealthSource =>
        OperatingSystem.IsAndroid() ? HealthDataSource.HealthConnect : HealthDataSource.AppleHealth;

    public string DeviceHealthDisplayName => DeviceHealthSource.ToDisplayName();

    public Task<HealthSyncStatus> GetStatusAsync(int userId)
    {
        return Task.FromResult(new HealthSyncStatus(
            null,
            false,
            true,
            null,
            "Health sync is paused for diagnostics."));
    }

    public Task<bool> ShouldShowPromptAsync(int userId)
    {
        return Task.FromResult(false);
    }

    public Task DismissPromptAsync(int userId)
    {
        return Task.CompletedTask;
    }

    public Task<HealthSyncResult> SyncAsync(
        int userId,
        HealthDataSource source,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HealthSyncResult(
            source,
            false,
            null,
            "Health sync is paused for diagnostics."));
    }

    public Task<HealthWeeklySummary?> GetCachedWeeklySummaryAsync(int userId)
    {
        return Task.FromResult<HealthWeeklySummary?>(null);
    }
}
