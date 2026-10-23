namespace MuscleCuties.Core.Services.Health;

public interface IHealthSyncService
{
    HealthDataSource DeviceHealthSource { get; }
    string DeviceHealthDisplayName { get; }
    bool IsAvailable { get; }
    Task<HealthSyncStatus> GetStatusAsync(int userId);
    Task<bool> ShouldShowPromptAsync(int userId);
    Task DismissPromptAsync(int userId);
    Task DisconnectAsync(int userId, CancellationToken cancellationToken = default);

    Task<HealthSyncResult> SyncAsync(int userId, HealthDataSource source,
        CancellationToken cancellationToken = default);

    Task<HealthWeeklySummary?> GetCachedWeeklySummaryAsync(int userId);
}
