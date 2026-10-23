using MuscleCuties.Core.Domain.Cycle;

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
            CycleQuickLogRule.Validate(new CycleQuickLogInput(
                input.LocalDate.ToDateTime(TimeOnly.MinValue), false, false,
                input.FlowIntensity, [], input.Phase, input.Note));
        }
        catch (ArgumentException)
        {
            return Task.FromResult(new SaveCyclePhaseLogOutput(SaveCyclePhaseLogOutcomeKind.InvalidInput));
        }

        return store.UpsertAsync(input, cancellationToken);
    }
}
