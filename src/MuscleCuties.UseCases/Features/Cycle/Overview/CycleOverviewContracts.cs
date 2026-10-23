using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.UseCases.Features.Cycle.Overview;

public sealed record CycleHistoryItem(int Id, DateTime StartDate, DateTime? EndDate, int CycleLength);

public sealed record CyclePhaseEntry(
    int Id,
    int? CycleLogId,
    CyclePhase Phase,
    FlowIntensity? FlowIntensity,
    DateTime LoggedAt,
    string? Note,
    DateTime CreatedAt);

public sealed record CycleSymptomEntry(
    int Id,
    int CycleLogId,
    DateTime Date,
    SymptomType SymptomType,
    int Severity,
    string? Notes);

public sealed record CyclePredictionSummary(
    bool HasActiveCycle,
    DateTime? CurrentCycleStartDate,
    int CurrentDay,
    int PredictedCycleLength,
    CyclePhase CurrentPhase,
    DateTime? PredictedNextPeriodDate,
    int DaysUntilPeriod,
    DateTime? PredictedOvulationDate,
    DateTime? FertileWindowStartDate,
    DateTime? FertileWindowEndDate,
    string PredictionSource);

public sealed record CycleOverviewSnapshot(
    CyclePredictionSummary Prediction,
    IReadOnlyList<CyclePhaseEntry> PhaseLogs,
    IReadOnlyList<CycleSymptomEntry> Symptoms,
    IReadOnlyList<CycleHistoryItem> History);

public interface ICycleOverviewUseCase
{
    Task<CycleOverviewSnapshot?> LoadAsync(DateTime rangeStart, DateTime rangeEnd,
        CancellationToken cancellationToken = default);
}
