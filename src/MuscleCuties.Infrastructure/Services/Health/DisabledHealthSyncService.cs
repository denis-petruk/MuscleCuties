namespace MuscleCuties.Core.Services.Health;

public sealed class DisabledHealthSyncService : IHealthSyncService
{
    // TODO(DEFERRED-NATIVE-HEALTH): This is an intentional V1 boundary. Enable a native adapter only after explicit owner authorization, permission design and device review.
    public HealthDataSource DeviceHealthSource =>
        OperatingSystem.IsAndroid() ? HealthDataSource.HealthConnect : HealthDataSource.AppleHealth;

    public string DeviceHealthDisplayName => DeviceHealthSource.ToDisplayName();
    public bool IsAvailable => false;

    public Task<HealthSyncStatus> GetStatusAsync(int userId)
    {
        return Task.FromResult(new HealthSyncStatus(
            null,
            false,
            true,
            null,
            "Health import is unavailable in this release."));
    }

    public Task<bool> ShouldShowPromptAsync(int userId)
    {
        return Task.FromResult(false);
    }

    public Task DismissPromptAsync(int userId)
    {
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(int userId, CancellationToken cancellationToken = default)
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
            "Health import is unavailable in this release."));
    }

    public Task<HealthWeeklySummary?> GetCachedWeeklySummaryAsync(int userId)
    {
        return Task.FromResult<HealthWeeklySummary?>(null);
    }
}
