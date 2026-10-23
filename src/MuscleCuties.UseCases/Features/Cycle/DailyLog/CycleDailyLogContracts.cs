using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.UseCases.Features.Cycle.DailyLog;

public sealed record CycleDailyLogRequest(
    DateTime Date,
    bool? IsPeriodStart,
    bool? IsPeriodEnd,
    FlowIntensity? FlowIntensity,
    IReadOnlyList<SymptomType> Symptoms,
    CyclePhase? PhaseOverride = null,
    string? Note = null);

public sealed record CycleDailyLogSnapshot(
    DateTime Date,
    bool IsPeriodStarted,
    bool IsPeriodEnded,
    FlowIntensity? FlowIntensity,
    IReadOnlyList<SymptomType> Symptoms);

public interface ICycleDailyLogUseCase
{
    Task<CycleDailyLogSnapshot?> LoadAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<bool> SaveAsync(CycleDailyLogRequest request, CancellationToken cancellationToken = default);
}
