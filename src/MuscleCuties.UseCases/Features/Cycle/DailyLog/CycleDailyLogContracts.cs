using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Cycle.DailyLog;

/// <summary>A day's phase and flow to record.</summary>
public sealed record CycleDailyLogRequest(
    DateTime Date,
    bool? IsPeriodStart,
    bool? IsPeriodEnd,
    FlowIntensityDto? FlowIntensity,
    IReadOnlyList<SymptomTypeDto> Symptoms,
    CyclePhaseDto? PhaseOverride = null,
    string? Note = null);

/// <summary>The day as stored, with the phase the projection would otherwise show.</summary>
public sealed record CycleDailyLogSnapshot(
    DateTime Date,
    bool IsPeriodStarted,
    bool IsPeriodEnded,
    FlowIntensityDto? FlowIntensity,
    IReadOnlyList<SymptomTypeDto> Symptoms);

/// <summary>Records a day's phase and flow.</summary>
public interface ICycleDailyLogUseCase
{
    Task<CycleDailyLogSnapshot?> LoadAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<bool> SaveAsync(CycleDailyLogRequest request, CancellationToken cancellationToken = default);
}
