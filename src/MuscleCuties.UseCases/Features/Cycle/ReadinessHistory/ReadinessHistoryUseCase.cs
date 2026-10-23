namespace MuscleCuties.UseCases.Features.Cycle.ReadinessHistory;

public sealed class ReadinessHistoryUseCase(IReadinessHistoryStore store) : IReadinessHistoryUseCase
{
    public Task<IReadOnlyList<ReadinessCheckInEntry>> LoadAsync(
        DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        if (end < start)
            throw new ArgumentOutOfRangeException(nameof(end), "Choose a valid check-in range.");
        return store.LoadAsync(start, end, cancellationToken);
    }
}
