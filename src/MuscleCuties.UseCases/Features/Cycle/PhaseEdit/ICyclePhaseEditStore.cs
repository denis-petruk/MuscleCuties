namespace MuscleCuties.UseCases.Features.Cycle.PhaseEdit;

public interface ICyclePhaseEditStore
{
    Task<bool> SaveAsync(CyclePhaseEditRequest request, CancellationToken cancellationToken);
}
