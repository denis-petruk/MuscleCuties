namespace MuscleCuties.UseCases.Features.Cycle.Overview;

public sealed class CycleOverviewUseCase(ICycleOverviewStore store) : ICycleOverviewUseCase
{
    public Task<CycleOverviewSnapshot?> LoadAsync(DateTime rangeStart, DateTime rangeEnd,
        CancellationToken cancellationToken = default)
    {
        if (rangeStart == default || rangeEnd == default || rangeEnd.Date < rangeStart.Date)
            throw new ArgumentOutOfRangeException(nameof(rangeEnd), "Choose a valid cycle calendar range.");
        return store.LoadAsync(rangeStart.Date, rangeEnd.Date, cancellationToken);
    }
}
