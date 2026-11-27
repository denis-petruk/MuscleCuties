namespace MuscleCuties.UseCases.Features.Cycle.History;

using MuscleCuties.Core.Domain.Cycle;
using CoreCycleObservation = MuscleCuties.Core.Domain.Cycle.CycleObservation;

/// <summary>Reads and writes cycle history, recalculating lengths when a start changes.</summary>
public sealed class CycleHistoryUseCase(ICycleHistoryStore store)
{
    private static readonly DateOnly MinimumDate = new(1970, 1, 1);
    private static readonly DateOnly MaximumDate = new(2100, 12, 31);
    private static readonly DateOnly PredictionMaximumDate = new(2100, 1, 1);
    private const int UnknownCycleLength = 0;

    /// <summary>The stored history replayed in order, which the prediction depends on.</summary>
    public Task<CycleHistorySnapshot> LoadAsync(Guid userId, DateOnly from, DateOnly through, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || from < MinimumDate || through > MaximumDate || through < from)
            throw new ArgumentOutOfRangeException(nameof(from), "A valid owner and supported local date range are required.");
        return store.LoadAsync(userId, from, through, cancellationToken);
    }

    /// <summary>Predicts the next phase from the stored history, and says so plainly when there is too little of it.</summary>
    public async Task<CyclePredictionProjection> ProjectPredictionAsync(Guid userId, DateOnly today, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || today < MinimumDate || today > PredictionMaximumDate)
            throw new ArgumentOutOfRangeException(nameof(today), "A valid owner and supported local date are required.");

        var inputs = await store.LoadPredictionInputsAsync(userId, today, cancellationToken).ConfigureAwait(false);
        var todayDateTime = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        MuscleCuties.Core.Domain.Cycle.CyclePhaseObservation? manualPhaseLog = null;
        if (inputs.ManualPhaseLog is { } loggedPhase &&
            !int.TryParse(loggedPhase.Phase, out _) &&
            Enum.TryParse<MuscleCuties.Core.Models.Enums.Cycle.CyclePhase>(loggedPhase.Phase, true, out var phase) &&
            Enum.IsDefined(phase))
        {
            manualPhaseLog = new(phase, loggedPhase.LocalDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified));
        }

        var history = inputs.Cycles
            .OrderBy(cycle => cycle.StartDate)
            .Select(cycle => ToCoreObservation(cycle, cycle.CycleLength))
            .ToList();
        if (manualPhaseLog is { } anchor && history.Count == 0)
        {
            // The onboarding quiz phase is a captured observation, not a floating label: with no cycle
            // start recorded yet it anchors the first cycle so day counting begins on the selected phase.
            var impliedStart = CyclePhaseRule.GetImpliedCycleStart(
                anchor.Phase,
                DateOnly.FromDateTime(anchor.LoggedAt),
                inputs.ProfileCycleLength ?? CycleLengthLimits.Default);
            history.Add(new CoreCycleObservation(
                impliedStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                null,
                UnknownCycleLength));
        }

        var latestCycle = history.LastOrDefault();
        var activeCycle = latestCycle is not null &&
                          (latestCycle.EndDate is null || latestCycle.EndDate > todayDateTime)
            ? latestCycle
            : null;
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

    /// <summary>Converts a stored cycle into the domain observation the prediction rules read.</summary>
    private static MuscleCuties.Core.Domain.Cycle.CycleObservation ToCoreObservation(CycleObservation cycle, int cycleLength) =>
        new(cycle.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            cycle.EndDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), cycleLength);

    /// <summary>The date of a timestamp, or nothing when it has none.</summary>
    private static DateOnly? ToDateOnly(DateTime? date) => date is null ? null : DateOnly.FromDateTime(date.Value);

    /// <summary>Stores a cycle start or end, recalculating the affected cycle's length.</summary>
    public Task<CycleObservation?> SaveCycleAsync(SaveCycleObservation request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UserId == Guid.Empty || request.Id == Guid.Empty || request.StartDate < MinimumDate || request.StartDate > MaximumDate ||
            request.EndDate is { } end && (end < request.StartDate || end > MaximumDate))
            throw new ArgumentOutOfRangeException(nameof(request), "Cycle dates must be ordered supported local calendar dates.");
        return store.SaveCycleAsync(request, cancellationToken);
    }

    /// <summary>Stores a symptom against a cycle, refusing one whose cycle is not hers.</summary>
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

    /// <summary>Deletes one symptom record, reporting whether it existed.</summary>
    public Task<bool> DeleteSymptomAsync(Guid userId, Guid symptomId, CancellationToken cancellationToken = default) =>
        userId == Guid.Empty || symptomId == Guid.Empty
            ? Task.FromResult(false)
            : store.DeleteSymptomAsync(userId, symptomId, cancellationToken);
}
