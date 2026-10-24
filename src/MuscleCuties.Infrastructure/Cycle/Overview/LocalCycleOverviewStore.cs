using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.UseCases.Features.Cycle.Contracts;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

namespace MuscleCuties.Infrastructure.Cycle.Overview;

public sealed class LocalCycleOverviewStore(IServiceScopeFactory scopeFactory) : ICycleOverviewStore
{
    public async Task<CycleOverviewSnapshot?> LoadAsync(
        DateTime rangeStart, DateTime rangeEnd, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var user = scope.ServiceProvider.GetRequiredService<ICurrentProfileUserIdProvider>();
        var userId = await user.GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
        if (userId <= 0)
            return null;

        var cycle = scope.ServiceProvider.GetRequiredService<ICycleService>();
        var prediction = await cycle.GetPredictionAsync(userId).ConfigureAwait(false);
        var phaseLogs = await cycle.GetPhaseLogsForCalendarRangeAsync(userId, rangeStart, rangeEnd)
            .ConfigureAwait(false);
        var symptoms = await cycle.GetSymptomsByDateRangeAsync(userId, rangeStart, rangeEnd)
            .ConfigureAwait(false);
        var history = await cycle.GetCycleHistoryAsync(userId).ConfigureAwait(false);

        if (await user.GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false) != userId)
            return null;

        return new CycleOverviewSnapshot(
            new CyclePredictionSummary(prediction.HasActiveCycle, prediction.CurrentCycleStartDate,
                prediction.CurrentDay, prediction.PredictedCycleLength,
                CycleContractMapper.ToDto(prediction.CurrentPhase),
                prediction.PredictedNextPeriodDate, prediction.DaysUntilPeriod,
                prediction.PredictedOvulationDate, prediction.FertileWindowStartDate,
                prediction.FertileWindowEndDate, prediction.PredictionSource),
            phaseLogs.Select(log => new CyclePhaseEntry(log.Id, log.CycleLogId,
                CycleContractMapper.ToDto(log.Phase),
                log.FlowIntensity is { } flow ? CycleContractMapper.ToDto(flow) : null,
                log.LoggedAt, log.Note, log.CreatedAt)).ToArray(),
            symptoms.Select(log => new CycleSymptomEntry(log.Id, log.CycleLogId, log.Date,
                CycleContractMapper.ToDto(log.SymptomType), log.Severity, log.Notes)).ToArray(),
            history.Select(log => new CycleHistoryItem(log.Id, log.StartDate, log.EndDate,
                log.CycleLength)).ToArray());
    }
}
