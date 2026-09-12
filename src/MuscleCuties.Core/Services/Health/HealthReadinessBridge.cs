using MuscleCuties.Core.Models.Entities.Workout.Planning;

namespace MuscleCuties.Core.Services.Health;

public interface IHealthReadinessBridge
{
    Task EnrichAsync(int userId, DailyReadinessLog log);
}

public sealed class HealthReadinessBridge : IHealthReadinessBridge
{
    private readonly IHealthSyncService _healthSync;

    public HealthReadinessBridge(IHealthSyncService healthSync)
    {
        _healthSync = healthSync;
    }

    public async Task EnrichAsync(int userId, DailyReadinessLog log)
    {
        var summary = await _healthSync.GetCachedWeeklySummaryAsync(userId);
        if (summary is null)
            return;

        if (summary.HasMovementData)
        {
            log.Steps7dAvg = summary.AverageSteps;
            log.StepsYesterday = summary.AverageSteps;
        }

        if (summary.HasSleepData)
        {
            log.Sleep3dAvg = summary.AverageSleepHours;
        }
    }
}
