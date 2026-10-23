using MuscleCuties.Core.Domain.Cycle;

namespace MuscleCuties.UseCases.Features.Cycle.PhaseEdit;

public sealed class CyclePhaseEditUseCase(ICyclePhaseEditStore store) : ICyclePhaseEditUseCase
{
    public Task<bool> SaveAsync(CyclePhaseEditRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CycleQuickLogRule.Validate(new CycleQuickLogInput(request.Date.Date,
            false, false, null, [], request.Phase, request.Note));
        return store.SaveAsync(request with { Date = request.Date.Date }, cancellationToken);
    }
}
