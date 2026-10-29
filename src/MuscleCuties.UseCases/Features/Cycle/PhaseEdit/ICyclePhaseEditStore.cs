namespace MuscleCuties.UseCases.Features.Cycle.PhaseEdit;

public interface ICyclePhaseEditStore
{
    Task<CyclePhaseEditResult> SaveAsync(CyclePhaseEditRequest request, CancellationToken cancellationToken);
}
