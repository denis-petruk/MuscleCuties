namespace MuscleCuties.UseCases.Features.Cycle.DailyLog;

public interface ICycleDailyLogStore
{
    Task<CycleDailyLogSnapshot?> LoadAsync(DateTime date, CancellationToken cancellationToken);
    Task<bool> SaveAsync(CycleDailyLogRequest request, CancellationToken cancellationToken);
}
