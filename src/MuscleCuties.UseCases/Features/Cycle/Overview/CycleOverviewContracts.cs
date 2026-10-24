using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Cycle.Overview;

public sealed record CycleHistoryItem(int Id, DateTime StartDate, DateTime? EndDate, int CycleLength);

public sealed record CyclePhaseEntry(
    int Id,
    int? CycleLogId,
    CyclePhaseDto Phase,
    FlowIntensityDto? FlowIntensity,
    DateTime LoggedAt,
    string? Note,
    DateTime CreatedAt);

public sealed record CycleSymptomEntry(
    int Id,
    int CycleLogId,
    DateTime Date,
    SymptomTypeDto SymptomType,
    int Severity,
    string? Notes);

public sealed record CyclePredictionSummary(
    bool HasActiveCycle,
    DateTime? CurrentCycleStartDate,
    int CurrentDay,
    int PredictedCycleLength,
    CyclePhaseDto CurrentPhase,
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

public sealed record CyclePhaseProjectionInputDto(
    DateTime Date,
    DateTime Today,
    int CycleLength,
    DateTime? CycleStartDate = null,
    int? CycleDayOverride = null,
    DateTime? CycleDayOverrideDate = null,
    CyclePhaseDto CurrentPhase = CyclePhaseDto.Unknown,
    bool HasActiveCycle = false,
    CyclePhaseDto? LoggedPhase = null,
    DateTime? LoggedAt = null);

public sealed record CyclePhaseProjectionOutputDto(int NormalizedCycleLength, int CycleDay, CyclePhaseDto Phase);
public sealed record CyclePhaseCountdownInputDto(int CycleDay, int CycleLength, CyclePhaseDto CurrentPhase);
public sealed record CyclePhaseCountdownOutputDto(int DaysRemaining);
public sealed record NextCyclePhaseInputDto(CyclePhaseDto CurrentPhase);
public sealed record NextCyclePhaseOutputDto(CyclePhaseDto NextPhase);

public interface ICyclePhaseProjectionUseCase
{
    CyclePhaseProjectionOutputDto Project(CyclePhaseProjectionInputDto input);
    CyclePhaseProjectionOutputDto ProjectDashboardCalendar(CyclePhaseProjectionInputDto input);
    CyclePhaseCountdownOutputDto GetDaysRemaining(CyclePhaseCountdownInputDto input);
    NextCyclePhaseOutputDto GetNextPhase(NextCyclePhaseInputDto input);
}

public interface ICycleOverviewUseCase
{
    Task<CycleOverviewSnapshot?> LoadAsync(DateTime rangeStart, DateTime rangeEnd,
        CancellationToken cancellationToken = default);
}
