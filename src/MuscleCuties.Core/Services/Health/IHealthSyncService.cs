namespace MuscleCuties.Core.Services.Health;

public interface IHealthSyncService
{
    Task<HealthSyncStatus> GetStatusAsync(int userId);
    Task<bool> ShouldShowPromptAsync(int userId);
    Task DismissPromptAsync(int userId);
    Task<HealthSyncResult> SyncAsync(int userId, HealthDataSource source, CancellationToken cancellationToken = default);
    Task<HealthWeeklySummary?> GetCachedWeeklySummaryAsync(int userId);
}
