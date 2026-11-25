namespace MuscleCuties.UseCases.Features.Cycle.DailyLog;

/// <summary>The persistence the daily cycle log needs.</summary>
public interface ICycleDailyLogStore
{
    Task<CycleDailyLogSnapshot?> LoadAsync(DateTime date, CancellationToken cancellationToken);
    Task<bool> SaveAsync(CycleDailyLogRequest request, CancellationToken cancellationToken);
}
