namespace MuscleCuties.UseCases.Features.Cycle.ReadinessHistory;

public interface IReadinessHistoryStore
{
    Task<IReadOnlyList<ReadinessCheckInEntry>> LoadAsync(DateOnly start, DateOnly end,
        CancellationToken cancellationToken);
}
