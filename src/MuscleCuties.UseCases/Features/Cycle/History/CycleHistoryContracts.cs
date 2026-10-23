namespace MuscleCuties.UseCases.Features.Cycle.History;

public sealed record CycleObservation(Guid Id, DateOnly StartDate, DateOnly? EndDate, int CycleLength);
public sealed record CycleSymptom(Guid Id, Guid CycleId, DateOnly Date, string Type, int Severity, string? Notes);
public sealed record CycleHistorySnapshot(IReadOnlyList<CycleObservation> Cycles, IReadOnlyList<CycleSymptom> Symptoms);
public sealed record CycleManualPhaseLog(string Phase, DateOnly LocalDate);
public sealed record CyclePredictionInputs(IReadOnlyList<CycleObservation> Cycles, int? ProfileCycleLength, CycleManualPhaseLog? ManualPhaseLog = null);
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
public sealed record SaveCycleObservation(Guid UserId, Guid Id, DateOnly StartDate, DateOnly? EndDate);
public sealed record SaveCycleSymptom(Guid UserId, Guid Id, Guid CycleId, DateOnly Date, string Type, int Severity, string? Notes);

public sealed class CycleHistoryIdConflictException : Exception
{
    public CycleHistoryIdConflictException() : base("The supplied record ID cannot be used.") { }
}

public interface ICycleHistoryStore
{
    Task<CycleHistorySnapshot> LoadAsync(Guid userId, DateOnly from, DateOnly through, CancellationToken cancellationToken);
    Task<CyclePredictionInputs> LoadPredictionInputsAsync(Guid userId, DateOnly today, CancellationToken cancellationToken);
    Task<CycleObservation?> SaveCycleAsync(SaveCycleObservation request, CancellationToken cancellationToken);
    Task<bool> DeleteCycleAsync(Guid userId, Guid cycleId, CancellationToken cancellationToken);
    Task<CycleSymptom?> SaveSymptomAsync(SaveCycleSymptom request, CancellationToken cancellationToken);
    Task<bool> DeleteSymptomAsync(Guid userId, Guid symptomId, CancellationToken cancellationToken);
}
