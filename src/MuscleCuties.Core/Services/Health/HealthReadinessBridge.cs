using MuscleCuties.Core.Models.Entities.Workout.Planning;

namespace MuscleCuties.Core.Services.Health;

public interface IHealthReadinessBridge
{
    Task<HealthWeeklySummary?> EnrichAsync(int userId, DailyReadinessLog log);
}

public sealed class HealthReadinessBridge : IHealthReadinessBridge
{
    private readonly IHealthSyncService _healthSync;
    private readonly IReadinessSyncOrchestrator? _orchestrator;

    public HealthReadinessBridge(IHealthSyncService healthSync)
        : this(healthSync, null)
    {
    }

    public HealthReadinessBridge(IHealthSyncService healthSync, IReadinessSyncOrchestrator? orchestrator)
    {
        _healthSync = healthSync;
        _orchestrator = orchestrator;
    }

    public async Task<HealthWeeklySummary?> EnrichAsync(int userId, DailyReadinessLog log)
    {
        var summary = _orchestrator is not null
            ? await _orchestrator.SyncIfStaleAndGetSummaryAsync(userId)
            : await _healthSync.GetCachedWeeklySummaryAsync(userId);
        if (summary is null)
            return null;

        if (summary.HasMovementData)
        {
            log.Steps7dAvg = summary.AverageSteps;
            log.StepsYesterday = summary.YesterdaySteps.GetValueOrDefault();
        }

        if (summary.HasSleepData)
        {
            log.Sleep3dAvg = summary.AverageSleep3dHours.GetValueOrDefault();
            if (log.SleepHours <= 0 && summary.LastNightSleepHours is > 0)
                log.SleepHours = summary.LastNightSleepHours.Value;
        }
        return summary;
    }
}
