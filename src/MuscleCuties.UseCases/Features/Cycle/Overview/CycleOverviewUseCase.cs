namespace MuscleCuties.UseCases.Features.Cycle.Overview;

/// <summary>Builds the overview from the stored history and the projection.</summary>
public sealed class CycleOverviewUseCase(ICycleOverviewStore store) : ICycleOverviewUseCase
{
    /// <summary>Loads the cycle overview snapshot for the requested range.</summary>
    public Task<CycleOverviewSnapshot?> LoadAsync(DateTime rangeStart, DateTime rangeEnd,
        CancellationToken cancellationToken = default)
    {
        if (rangeStart == default || rangeEnd == default || rangeEnd.Date < rangeStart.Date)
            throw new ArgumentOutOfRangeException(nameof(rangeEnd), "Choose a valid cycle calendar range.");
        return store.LoadAsync(rangeStart.Date, rangeEnd.Date, cancellationToken);
    }
}
