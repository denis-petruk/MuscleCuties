using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Cycle.DailyLog;

public sealed record CycleDailyLogRequest(
    DateTime Date,
    bool? IsPeriodStart,
    bool? IsPeriodEnd,
    FlowIntensityDto? FlowIntensity,
    IReadOnlyList<SymptomTypeDto> Symptoms,
    CyclePhaseDto? PhaseOverride = null,
    string? Note = null);

public sealed record CycleDailyLogSnapshot(
    DateTime Date,
    bool IsPeriodStarted,
    bool IsPeriodEnded,
    FlowIntensityDto? FlowIntensity,
    IReadOnlyList<SymptomTypeDto> Symptoms);

public interface ICycleDailyLogUseCase
{
    Task<CycleDailyLogSnapshot?> LoadAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<bool> SaveAsync(CycleDailyLogRequest request, CancellationToken cancellationToken = default);
}
