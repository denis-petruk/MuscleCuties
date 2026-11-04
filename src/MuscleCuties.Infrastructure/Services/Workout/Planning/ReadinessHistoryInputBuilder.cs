using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

// Preserve the existing service-facing observation contract while rules move inward.
public sealed record ReadinessObservation(
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

public static class ReadinessHistoryInputBuilder
{
    public static DailyInputs? Build(
        IReadOnlyList<ReadinessObservation>? history,
        DateOnly targetDate,
        DailyInputs? todayInputs = null)
    {
        return ReadinessHistoryRule.Build(history?.Where(log => log is not null)
            .Select(log => new ReadinessHistoryObservation(
                log.Date, log.SleepHours, log.StepsYesterday, log.Energy, log.Pain,
                log.Bloating, log.WeightKg, log.SleepQuality, log.Soreness,
                log.PreviousDayLoad)).ToArray(), targetDate, todayInputs);
    }
}
