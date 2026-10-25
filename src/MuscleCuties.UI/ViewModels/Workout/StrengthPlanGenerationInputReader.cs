using System.Globalization;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

internal static class StrengthPlanGenerationInputReader
{
    private const decimal MinimumAverageSessions = 0m;
    private const int NoElapsedDays = 0;
    public const int DefaultWarmupMinutes = 5;
    public const int DefaultCooldownMinutes = 5;

    public static bool TryRead(
        WorkoutActivityDto? activity,
        string consistentWeeks,
        string averageSessionsPerWeek,
        string daysSinceLastSession,
        string warmupMinutes,
        string cooldownMinutes,
        bool isSelfReported,
        DateOnly localDate,
        out ActivityTrainingHistoryInputV020? history,
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

        int? weeks = null;
        if (!string.IsNullOrWhiteSpace(consistentWeeks))
        {
            if (!int.TryParse(consistentWeeks, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedWeeks) ||
                parsedWeeks < 0)
            {
                failureMessageKey = "Workout_Generation_EnterExperience";
                return false;
            }

            weeks = parsedWeeks;
        }

        decimal? averageSessions = null;
        if (!string.IsNullOrWhiteSpace(averageSessionsPerWeek))
        {
            if ((!decimal.TryParse(averageSessionsPerWeek, NumberStyles.AllowDecimalPoint,
                     CultureInfo.CurrentCulture, out var parsedAverageSessions) &&
                 !decimal.TryParse(averageSessionsPerWeek, NumberStyles.AllowDecimalPoint,
                     CultureInfo.InvariantCulture, out parsedAverageSessions)) ||
                parsedAverageSessions < MinimumAverageSessions)
            {
                failureMessageKey = "Workout_Generation_EnterExperience";
                return false;
            }

            averageSessions = parsedAverageSessions;
        }

        int? daysAgo = null;
        if (!string.IsNullOrWhiteSpace(daysSinceLastSession))
        {
            if (!int.TryParse(daysSinceLastSession, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedDaysAgo) ||
                parsedDaysAgo < NoElapsedDays || parsedDaysAgo > localDate.DayNumber)
            {
                failureMessageKey = "Workout_Generation_EnterLastSession";
                return false;
            }

            daysAgo = parsedDaysAgo;
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

        history = weeks is null && averageSessions is null && daysAgo is null
            ? null
            : new ActivityTrainingHistoryInputV020(
                activity.Value.ToString(), weeks, averageSessions,
                daysAgo is { } elapsedDays ? localDate.AddDays(-elapsedDays) : null,
                isSelfReported);
        return true;
    }
}
