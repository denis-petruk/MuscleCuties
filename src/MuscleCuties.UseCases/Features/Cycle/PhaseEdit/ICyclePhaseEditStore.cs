namespace MuscleCuties.UseCases.Features.Cycle.PhaseEdit;

/// <summary>The persistence a phase correction needs.</summary>
public interface ICyclePhaseEditStore
{
    Task<CyclePhaseEditResult> SaveAsync(CyclePhaseEditRequest request, CancellationToken cancellationToken);
}
