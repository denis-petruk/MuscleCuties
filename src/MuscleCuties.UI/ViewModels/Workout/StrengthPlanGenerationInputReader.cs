using System.Globalization;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

internal static class StrengthPlanGenerationInputReader
{
    private const decimal MinimumAverageSessions = 0m;
    private const int NoElapsedDays = 0;

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
        warmup = NoElapsedDays;
        cooldown = NoElapsedDays;
        failureMessageKey = string.Empty;

        if (activity is null ||
            !int.TryParse(consistentWeeks, NumberStyles.None, CultureInfo.InvariantCulture, out var weeks) ||
            !decimal.TryParse(averageSessionsPerWeek, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var averageSessions) ||
            averageSessions < MinimumAverageSessions)
        {
            failureMessageKey = "Workout_Generation_EnterExperience";
            return false;
        }

        if (!int.TryParse(daysSinceLastSession, NumberStyles.None, CultureInfo.InvariantCulture,
                out var daysAgo) ||
            daysAgo < NoElapsedDays || daysAgo > localDate.DayNumber)
        {
            failureMessageKey = "Workout_Generation_EnterLastSession";
            return false;
        }

        if (!int.TryParse(warmupMinutes, NumberStyles.None, CultureInfo.InvariantCulture, out warmup) ||
            !int.TryParse(cooldownMinutes, NumberStyles.None, CultureInfo.InvariantCulture, out cooldown))
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
