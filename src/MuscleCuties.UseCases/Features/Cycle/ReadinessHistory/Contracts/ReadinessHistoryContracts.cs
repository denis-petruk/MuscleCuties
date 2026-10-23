namespace MuscleCuties.UseCases.Features.Cycle.ReadinessHistory;

public sealed record ReadinessCheckInEntry(
    DateOnly Date,
    double SleepHours,
    int StepsYesterday,
    int Energy,
    int Pain,
    bool Bloating,
    double? WeightKg,
    int? SleepQuality,
    int? Soreness,
    double? PreviousDayLoad);

public interface IReadinessHistoryUseCase
{
    Task<IReadOnlyList<ReadinessCheckInEntry>> LoadAsync(DateOnly start, DateOnly end,
        CancellationToken cancellationToken = default);
}
