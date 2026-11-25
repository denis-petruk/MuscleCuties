namespace MuscleCuties.UseCases.Features.Cycle.History;

/// <summary>One recorded cycle start or end.</summary>
public sealed record CycleObservation(Guid Id, DateOnly StartDate, DateOnly? EndDate, int CycleLength);
/// <summary>One symptom logged against a cycle.</summary>
public sealed record CycleSymptom(Guid Id, Guid CycleId, DateOnly Date, string Type, int Severity, string? Notes);
/// <summary>The history the prediction reads, in the order it must be replayed.</summary>
public sealed record CycleHistorySnapshot(IReadOnlyList<CycleObservation> Cycles, IReadOnlyList<CycleSymptom> Symptoms);
/// <summary>A day's phase she recorded by hand, which overrides the projection.</summary>
public sealed record CycleManualPhaseLog(string Phase, DateOnly LocalDate);
/// <summary>The minimal history a prediction needs.</summary>
public sealed record CyclePredictionInputs(IReadOnlyList<CycleObservation> Cycles, int? ProfileCycleLength, CycleManualPhaseLog? ManualPhaseLog = null);
/// <summary>A predicted phase for a future day, with the confidence the inputs allow.</summary>
public sealed record CyclePredictionProjection(
    DateOnly AsOfDate,
    bool HasActiveCycle,
    DateOnly? CurrentCycleStartDate,
    int CurrentDay,
    int PredictedCycleLength,
    string CurrentPhase,
    DateOnly? PredictedNextPeriodDate,
    int DaysUntilPeriod,
    DateOnly? PredictedOvulationDate,
    DateOnly? FertileWindowStartDate,
    DateOnly? FertileWindowEndDate,
    string PredictionSource);
/// <summary>A cycle to store, scoped to its owner.</summary>
public sealed record SaveCycleObservation(Guid UserId, Guid Id, DateOnly StartDate, DateOnly? EndDate);
/// <summary>A symptom to store, scoped to its owner.</summary>
public sealed record SaveCycleSymptom(Guid UserId, Guid Id, Guid CycleId, DateOnly Date, string Type, int Severity, string? Notes);

/// <summary>A conflicting id was reused for a cycle or symptom.</summary>
public sealed class CycleHistoryIdConflictException : Exception
{
    /// <summary>Reports that the supplied record id cannot be used for a new entry.</summary>
    public CycleHistoryIdConflictException() : base("The supplied record ID cannot be used.") { }
}

/// <summary>The cycle and symptom persistence.</summary>
public interface ICycleHistoryStore
{
    Task<CycleHistorySnapshot> LoadAsync(Guid userId, DateOnly from, DateOnly through, CancellationToken cancellationToken);
    Task<CyclePredictionInputs> LoadPredictionInputsAsync(Guid userId, DateOnly today, CancellationToken cancellationToken);
    Task<CycleObservation?> SaveCycleAsync(SaveCycleObservation request, CancellationToken cancellationToken);
    Task<bool> DeleteCycleAsync(Guid userId, Guid cycleId, CancellationToken cancellationToken);
    Task<CycleSymptom?> SaveSymptomAsync(SaveCycleSymptom request, CancellationToken cancellationToken);
    Task<bool> DeleteSymptomAsync(Guid userId, Guid symptomId, CancellationToken cancellationToken);
}
