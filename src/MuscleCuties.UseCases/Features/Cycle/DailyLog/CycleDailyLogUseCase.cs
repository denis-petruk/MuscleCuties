using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Cycle.DailyLog;

/// <summary>Records a day's log under the account's lock, keeping one row per day.</summary>
public sealed class CycleDailyLogUseCase(ICycleDailyLogStore store) : ICycleDailyLogUseCase
{
    /// <summary>The stored log for that day, or null when she has not logged it.</summary>
    public Task<CycleDailyLogSnapshot?> LoadAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        return store.LoadAsync(date.Date, cancellationToken);
    }

    /// <summary>Stores the day's phase and flow, replacing that day's row so one day keeps one log.</summary>
    public Task<bool> SaveAsync(CycleDailyLogRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var flowIntensity = request.FlowIntensity is { } requestedFlow
            ? CycleContractMapper.ToCore(requestedFlow)
            : (MuscleCuties.Core.Models.Enums.Cycle.FlowIntensity?)null;
        var symptoms = request.Symptoms.Select(CycleContractMapper.ToCore).ToArray();
        var phaseOverride = request.PhaseOverride is { } requestedPhase
            ? CycleContractMapper.ToCore(requestedPhase)
            : (MuscleCuties.Core.Models.Enums.Cycle.CyclePhase?)null;
        CycleQuickLogRule.Validate(new CycleQuickLogInput(request.Date.Date,
            request.IsPeriodStart, request.IsPeriodEnd, flowIntensity,
            symptoms, phaseOverride, request.Note));
        return store.SaveAsync(request with { Date = request.Date.Date }, cancellationToken);
    }
}
