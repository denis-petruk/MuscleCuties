using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Cycle.PhaseEdit;

public sealed record CyclePhaseEditRequest(DateTime Date, CyclePhaseDto Phase, string? Note);

public interface ICyclePhaseEditUseCase
{
    Task<bool> SaveAsync(CyclePhaseEditRequest request, CancellationToken cancellationToken = default);
}
