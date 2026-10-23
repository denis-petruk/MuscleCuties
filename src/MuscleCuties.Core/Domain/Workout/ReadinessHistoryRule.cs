using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout;

public sealed record ReadinessHistoryObservation(
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

public static class ReadinessHistoryRule
{
    public static DailyInputs? Build(
        IReadOnlyList<ReadinessHistoryObservation>? history,
        DateOnly targetDate,
        DailyInputs? todayInputs = null)
    {
        if (history is null || history.Count == 0)
            return todayInputs is null ? null : WithoutHistory(targetDate, todayInputs);

        var validLogs = history
            .Where(log => log is not null && log.Date != default && log.Date <= targetDate)
            .OrderBy(log => log.Date)
            .ToList();

        if (validLogs.Count == 0 && todayInputs is null)
            return null;

        var todayLog = validLogs.LastOrDefault(log => log.Date == targetDate);
        if (todayLog is null && todayInputs is null)
            return null;

        var targetDayNumber = targetDate.DayNumber;
        var threeDaysBefore = DateOnly.FromDayNumber(Math.Max(DateOnly.MinValue.DayNumber, targetDayNumber - 3));
        var yesterdayDate = targetDayNumber > DateOnly.MinValue.DayNumber
            ? DateOnly.FromDayNumber(targetDayNumber - 1)
            : DateOnly.MinValue;
        var sleepWindow = validLogs
            .Where(log => log.Date >= threeDaysBefore && log.Date < targetDate)
            .Where(log => double.IsFinite(log.SleepHours) && log.SleepHours > 0)
            .Select(log => log.SleepHours)
            .ToList();
        var sleep3dAvg = sleepWindow.Count > 0 ? sleepWindow.Average() : 0d;

        var sevenDaysBefore = DateOnly.FromDayNumber(Math.Max(DateOnly.MinValue.DayNumber, targetDayNumber - 7));
        var stepsWindow = validLogs
            .Where(log => log.Date >= sevenDaysBefore && log.Date < targetDate)
            .Where(log => log.StepsYesterday > 0)
            .Select(log => log.StepsYesterday)
            .ToList();
        var steps7dAvg = stepsWindow.Count > 0 ? (int)Math.Round(stepsWindow.Average()) : 0;

        var yesterdayLog = targetDayNumber > DateOnly.MinValue.DayNumber
            ? validLogs.LastOrDefault(log => log.Date == yesterdayDate)
            : null;
        var previousDayLoad = todayInputs?.PreviousDayLoad ??
                              (yesterdayLog?.PreviousDayLoad.HasValue == true
                                  ? new WorkoutLoad(WorkoutLoadKind.AggregateOnly, yesterdayLog.PreviousDayLoad.Value)
                                  : null);

        var sleepHours = todayInputs?.SleepHours ?? todayLog?.SleepHours ?? 0;
        var energy = todayInputs?.Energy ?? (todayLog?.Energy is >= 1 and <= 5 ? todayLog.Energy : 1);
        var pain = todayInputs?.Pain ?? (todayLog?.Pain is >= 0 and <= 3 ? todayLog.Pain : 3);
        var bloating = todayInputs?.Bloating ?? todayLog?.Bloating ?? false;
        var weightKg = todayInputs?.WeightKg ?? todayLog?.WeightKg;
        var sleepQuality = todayInputs?.SleepQuality ??
                           (todayLog?.SleepQuality is >= 1 and <= 5 ? (SleepQuality)todayLog.SleepQuality.Value : null);
        var soreness = todayInputs?.Soreness ??
                       (todayLog?.Soreness is >= 0 and <= 3 ? (SorenessLevel)todayLog.Soreness.Value : null);

        return DailyInputs.CreateSafe(targetDate, sleepHours, sleep3dAvg,
            yesterdayLog?.StepsYesterday ?? 0, steps7dAvg, energy, pain, bloating, weightKg,
            todayInputs?.RestingHeartRate, todayInputs?.AverageActiveEnergyKcal,
            sleepQuality, soreness, previousDayLoad);
    }

    private static DailyInputs WithoutHistory(DateOnly targetDate, DailyInputs todayInputs)
    {
        return DailyInputs.CreateSafe(targetDate, todayInputs.SleepHours,
            0, 0, 0,
            todayInputs.Energy, todayInputs.Pain, todayInputs.Bloating,
            todayInputs.WeightKg, todayInputs.RestingHeartRate,
            todayInputs.AverageActiveEnergyKcal, todayInputs.SleepQuality,
            todayInputs.Soreness);
    }
}
