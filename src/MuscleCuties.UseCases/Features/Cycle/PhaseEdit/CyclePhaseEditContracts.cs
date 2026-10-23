using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.UseCases.Features.Cycle.PhaseEdit;

public sealed record CyclePhaseEditRequest(DateTime Date, CyclePhase Phase, string? Note);

public interface ICyclePhaseEditUseCase
{
    Task<bool> SaveAsync(CyclePhaseEditRequest request, CancellationToken cancellationToken = default);
}
