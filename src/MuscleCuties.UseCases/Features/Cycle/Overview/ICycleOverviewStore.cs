namespace MuscleCuties.UseCases.Features.Cycle.Overview;

/// <summary>The persistence the overview needs.</summary>
public interface ICycleOverviewStore
{
    Task<CycleOverviewSnapshot?> LoadAsync(DateTime rangeStart, DateTime rangeEnd,
        CancellationToken cancellationToken);
}
