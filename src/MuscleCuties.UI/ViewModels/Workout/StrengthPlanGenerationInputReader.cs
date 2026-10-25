using System.Globalization;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

internal static class StrengthPlanGenerationInputReader
{
    private const decimal MinimumAverageSessions = 0m;
    private const int NoElapsedDays = 0;
    public const int DefaultConsistentWeeks = 24;
    public const decimal DefaultAverageSessions = 3m;
    public const int DefaultDaysSinceLastSession = 2;
    public const int DefaultWarmupMinutes = 5;
    public const int DefaultCooldownMinutes = 5;

    public static bool TryRead(
        WorkoutActivityDto? activity,
        string consistentWeeks,
        string averageSessionsPerWeek,
        string daysSinceLastSession,
        string warmupMinutes,
        string cooldownMinutes,
        DateOnly localDate,
        out ActivityTrainingHistoryInputV020 history,
        out int warmup,
        out int cooldown,
        out string failureMessageKey)
    {
        history = null!;
        warmup = DefaultWarmupMinutes;
        cooldown = DefaultCooldownMinutes;
        failureMessageKey = string.Empty;

        if (activity is null)
        {
            failureMessageKey = "Workout_Generation_EnterExperience";
            return false;
        }

        var weeks = DefaultConsistentWeeks;
        if (!string.IsNullOrWhiteSpace(consistentWeeks) &&
            (!int.TryParse(consistentWeeks, NumberStyles.None, CultureInfo.InvariantCulture, out weeks) || weeks < 0))
        {
            failureMessageKey = "Workout_Generation_EnterExperience";
            return false;
        }

        var averageSessions = DefaultAverageSessions;
        if (!string.IsNullOrWhiteSpace(averageSessionsPerWeek) &&
            (!decimal.TryParse(averageSessionsPerWeek, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out averageSessions) ||
             averageSessions < MinimumAverageSessions))
        {
            failureMessageKey = "Workout_Generation_EnterExperience";
            return false;
        }

        var daysAgo = DefaultDaysSinceLastSession;
        if (!string.IsNullOrWhiteSpace(daysSinceLastSession) &&
            (!int.TryParse(daysSinceLastSession, NumberStyles.None, CultureInfo.InvariantCulture, out daysAgo) ||
             daysAgo < NoElapsedDays || daysAgo > localDate.DayNumber))
        {
            failureMessageKey = "Workout_Generation_EnterLastSession";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(warmupMinutes) &&
            (!int.TryParse(warmupMinutes, NumberStyles.None, CultureInfo.InvariantCulture, out warmup) || warmup < 0))
        {
            failureMessageKey = "Workout_Generation_EnterSessionTiming";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(cooldownMinutes) &&
            (!int.TryParse(cooldownMinutes, NumberStyles.None, CultureInfo.InvariantCulture, out cooldown) || cooldown < 0))
        {
            failureMessageKey = "Workout_Generation_EnterSessionTiming";
            return false;
        }

        history = new ActivityTrainingHistoryInputV020(
            activity.Value.ToString(), weeks, averageSessions,
            localDate.AddDays(-daysAgo), true);
        return true;
    }
}
