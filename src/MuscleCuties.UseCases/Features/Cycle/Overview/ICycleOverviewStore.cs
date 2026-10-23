namespace MuscleCuties.UseCases.Features.Cycle.Overview;

public interface ICycleOverviewStore
{
    Task<CycleOverviewSnapshot?> LoadAsync(DateTime rangeStart, DateTime rangeEnd,
        CancellationToken cancellationToken);
}
