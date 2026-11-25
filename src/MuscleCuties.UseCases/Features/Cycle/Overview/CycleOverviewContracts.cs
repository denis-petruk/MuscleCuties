using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Cycle.Overview;

/// <summary>One past cycle as the overview lists it.</summary>
public sealed record CycleHistoryItem(int Id, DateTime StartDate, DateTime? EndDate, int CycleLength);

/// <summary>One day's phase in the overview, logged or projected.</summary>
public sealed record CyclePhaseEntry(
    int Id,
    int? CycleLogId,
    CyclePhaseDto Phase,
    FlowIntensityDto? FlowIntensity,
    DateTime LoggedAt,
    string? Note,
    DateTime CreatedAt);

/// <summary>One symptom shown on the overview.</summary>
public sealed record CycleSymptomEntry(
    int Id,
    int CycleLogId,
    DateTime Date,
    SymptomTypeDto SymptomType,
    int Severity,
    string? Notes);

/// <summary>The prediction shown on the overview, with where it came from.</summary>
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
    string PredictionSource,
    DateTime? CachedAsOfDate = null);

/// <summary>The whole overview payload: history, phases and the prediction.</summary>
public sealed record CycleOverviewSnapshot(
    CyclePredictionSummary Prediction,
    IReadOnlyList<CyclePhaseEntry> PhaseLogs,
    IReadOnlyList<CycleSymptomEntry> Symptoms,
    IReadOnlyList<CycleHistoryItem> History);

/// <summary>The inputs a phase projection needs, sent by the client when it asks for one.</summary>
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

/// <summary>The projected phase for a day.</summary>
public sealed record CyclePhaseProjectionOutputDto(int NormalizedCycleLength, int CycleDay, CyclePhaseDto Phase);
/// <summary>The inputs for a countdown to the next phase.</summary>
public sealed record CyclePhaseCountdownInputDto(int CycleDay, int CycleLength, CyclePhaseDto CurrentPhase);
/// <summary>Days until the phase changes.</summary>
public sealed record CyclePhaseCountdownOutputDto(int DaysRemaining);
/// <summary>The inputs for asking what follows the current phase.</summary>
public sealed record NextCyclePhaseInputDto(CyclePhaseDto CurrentPhase);
/// <summary>The phase that follows, wrapping after the last.</summary>
public sealed record NextCyclePhaseOutputDto(CyclePhaseDto NextPhase);

/// <summary>The phase calculations the client asks for directly.</summary>
public interface ICyclePhaseProjectionUseCase
{
    CyclePhaseProjectionOutputDto Project(CyclePhaseProjectionInputDto input);
    CyclePhaseProjectionOutputDto ProjectDashboardCalendar(CyclePhaseProjectionInputDto input);
    CyclePhaseCountdownOutputDto GetDaysRemaining(CyclePhaseCountdownInputDto input);
    NextCyclePhaseOutputDto GetNextPhase(NextCyclePhaseInputDto input);
}

/// <summary>The overview read.</summary>
public interface ICycleOverviewUseCase
{
    Task<CycleOverviewSnapshot?> LoadAsync(DateTime rangeStart, DateTime rangeEnd,
        CancellationToken cancellationToken = default);
}
