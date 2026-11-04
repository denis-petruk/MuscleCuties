using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Cycle.PhaseLog;

public sealed class SaveCyclePhaseLogUseCase(ICyclePhaseLogStore store) : ISaveCyclePhaseLogUseCase
{
    public Task<SaveCyclePhaseLogOutput> ExecuteAsync(
        SaveCyclePhaseLogInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.OwnerId))
            return Task.FromResult(new SaveCyclePhaseLogOutput(SaveCyclePhaseLogOutcomeKind.InvalidInput));

        try
        {
            var phase = CycleContractMapper.ToCore(input.Phase);
            var flowIntensity = input.FlowIntensity is { } requestedFlow
                ? CycleContractMapper.ToCore(requestedFlow)
                : (MuscleCuties.Core.Models.Enums.Cycle.FlowIntensity?)null;
            CycleQuickLogRule.Validate(new CycleQuickLogInput(
                input.LocalDate.ToDateTime(TimeOnly.MinValue), false, false,
                flowIntensity, [], phase, input.Note));
        }
        catch (ArgumentException)
        {
            return Task.FromResult(new SaveCyclePhaseLogOutput(SaveCyclePhaseLogOutcomeKind.InvalidInput));
        }

        return store.UpsertAsync(input, cancellationToken);
    }
}
