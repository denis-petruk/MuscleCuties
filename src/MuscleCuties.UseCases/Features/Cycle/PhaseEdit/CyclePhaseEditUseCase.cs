using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Cycle.PhaseEdit;

public sealed class CyclePhaseEditUseCase(ICyclePhaseEditStore store) : ICyclePhaseEditUseCase
{
    public Task<CyclePhaseEditResult> SaveAsync(CyclePhaseEditRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            CycleQuickLogRule.Validate(new CycleQuickLogInput(request.Date.Date,
                false, false, null, [], CycleContractMapper.ToCore(request.Phase), request.Note));
        }
        catch (ArgumentException)
        {
            return Task.FromResult(CyclePhaseEditResult.InvalidInput);
        }

        return store.SaveAsync(request with { Date = request.Date.Date }, cancellationToken);
    }
}
