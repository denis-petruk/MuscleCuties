namespace MuscleCuties.UseCases.Features.Cycle.History;

using MuscleCuties.Core.Domain.Cycle;

public sealed class CycleHistoryUseCase(ICycleHistoryStore store)
{
    private static readonly DateOnly MinimumDate = new(1970, 1, 1);
    private static readonly DateOnly MaximumDate = new(2100, 12, 31);
    private static readonly DateOnly PredictionMaximumDate = new(2100, 1, 1);
    private const int UnknownCycleLength = 0;

    public Task<CycleHistorySnapshot> LoadAsync(Guid userId, DateOnly from, DateOnly through, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || from < MinimumDate || through > MaximumDate || through < from)
            throw new ArgumentOutOfRangeException(nameof(from), "A valid owner and supported local date range are required.");
        return store.LoadAsync(userId, from, through, cancellationToken);
    }

    public async Task<CyclePredictionProjection> ProjectPredictionAsync(Guid userId, DateOnly today, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || today < MinimumDate || today > PredictionMaximumDate)
            throw new ArgumentOutOfRangeException(nameof(today), "A valid owner and supported local date are required.");

        var inputs = await store.LoadPredictionInputsAsync(userId, today, cancellationToken).ConfigureAwait(false);
        var orderedCycles = inputs.Cycles.OrderBy(cycle => cycle.StartDate).ToList();
        var history = orderedCycles.Select(cycle => ToCoreObservation(cycle, cycle.CycleLength)).ToList();
        var latest = inputs.Cycles.OrderByDescending(cycle => cycle.StartDate).FirstOrDefault();
        var latestIndex = latest is null ? -1 : orderedCycles.FindLastIndex(cycle => cycle.StartDate == latest.StartDate);
        var activeCycle = latest is not null && (latest.EndDate is null || latest.EndDate > today)
            ? history[latestIndex]
            : null;
        var todayDateTime = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        MuscleCuties.Core.Domain.Cycle.CyclePhaseObservation? manualPhaseLog = null;
        if (inputs.ManualPhaseLog is { } loggedPhase &&
            !int.TryParse(loggedPhase.Phase, out _) &&
            Enum.TryParse<MuscleCuties.Core.Models.Enums.Cycle.CyclePhase>(loggedPhase.Phase, true, out var phase) &&
            Enum.IsDefined(phase))
        {
            manualPhaseLog = new(phase, loggedPhase.LocalDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified));
        }
        var estimate = CyclePredictionDomainRule.CreatePrediction(activeCycle, history, inputs.ProfileCycleLength, todayDateTime, manualPhaseLog);
        return new CyclePredictionProjection(
            today,
            estimate.HasActiveCycle,
            ToDateOnly(estimate.CurrentCycleStartDate),
            estimate.CurrentDay,
            estimate.PredictedCycleLength,
            estimate.CurrentPhase.ToString(),
            ToDateOnly(estimate.PredictedNextPeriodDate),
            estimate.DaysUntilPeriod,
            ToDateOnly(estimate.PredictedOvulationDate),
            ToDateOnly(estimate.FertileWindowStartDate),
            ToDateOnly(estimate.FertileWindowEndDate),
            estimate.PredictionSource);
    }

    private static MuscleCuties.Core.Domain.Cycle.CycleObservation ToCoreObservation(CycleObservation cycle, int cycleLength) =>
        new(cycle.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            cycle.EndDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), cycleLength);

    private static DateOnly? ToDateOnly(DateTime? date) => date is null ? null : DateOnly.FromDateTime(date.Value);

    public Task<CycleObservation?> SaveCycleAsync(SaveCycleObservation request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UserId == Guid.Empty || request.Id == Guid.Empty || request.StartDate < MinimumDate || request.StartDate > MaximumDate ||
            request.EndDate is { } end && (end < request.StartDate || end > MaximumDate))
            throw new ArgumentOutOfRangeException(nameof(request), "Cycle dates must be ordered supported local calendar dates.");
        return store.SaveCycleAsync(request, cancellationToken);
    }

    public Task<CycleSymptom?> SaveSymptomAsync(SaveCycleSymptom request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UserId == Guid.Empty || request.Id == Guid.Empty || request.CycleId == Guid.Empty || request.Date < MinimumDate || request.Date > MaximumDate)
            throw new ArgumentOutOfRangeException(nameof(request), "Symptom fields must be within supported bounds.");
        var normalizedType = CycleSymptomRule.ValidateAndNormalizeType(request.Type);
        CycleSymptomRule.ValidateSeverity(request.Severity);
        CycleSymptomRule.ValidateNotes(request.Notes);
        return store.SaveSymptomAsync(request with { Type = normalizedType }, cancellationToken);
    }

    public Task<bool> DeleteCycleAsync(Guid userId, Guid cycleId, CancellationToken cancellationToken = default) =>
        userId == Guid.Empty || cycleId == Guid.Empty
            ? Task.FromResult(false)
            : store.DeleteCycleAsync(userId, cycleId, cancellationToken);

    public Task<bool> DeleteSymptomAsync(Guid userId, Guid symptomId, CancellationToken cancellationToken = default) =>
        userId == Guid.Empty || symptomId == Guid.Empty
            ? Task.FromResult(false)
            : store.DeleteSymptomAsync(userId, symptomId, cancellationToken);
}
