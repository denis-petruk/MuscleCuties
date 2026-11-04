using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.Registration;

internal static class RegistrationActivityDurationDefaults
{
    private const int DefaultFlexibleActivitySessionsPerWeek = 1;

    public static IReadOnlyDictionary<string, int> ForSelectedActivities(
        IReadOnlyCollection<int> activityTypes,
        int? strengthSessionDurationMinutes = null)
    {
        ArgumentNullException.ThrowIfNull(activityTypes);

        return activityTypes
            .Distinct()
            .Select(value => (WorkoutActivityType)value)
            .Where(WorkoutPreferencesRule.RequiresSessionDuration)
            .Where(activity => !WorkoutPreferencesRule.RequiresExplicitSessionDuration(activity))
            .Select(activity => (
                Activity: activity,
                DefaultMinutes: ActivitySessionDurationRule.IsStrengthActivity(activity) &&
                                strengthSessionDurationMinutes.HasValue &&
                                WorkoutPreferencesRule.IsValidSessionDuration(activity, strengthSessionDurationMinutes.Value)
                    ? strengthSessionDurationMinutes
                    : WorkoutPreferencesRule.DefaultSessionDurationMinutes(activity)))
            .Where(item => item.DefaultMinutes.HasValue)
            .ToDictionary(
                item => item.Activity.ToString(),
                item => item.DefaultMinutes!.Value,
                StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, int> ForSelectedActivityFrequencies(
        IReadOnlyCollection<int> activityTypes)
    {
        ArgumentNullException.ThrowIfNull(activityTypes);

        return activityTypes
            .Distinct()
            .Select(value => (WorkoutActivityType)value)
            .Where(WorkoutPreferencesRule.RequiresExplicitWeeklyFrequency)
            .ToDictionary(
                activity => activity.ToString(),
                _ => DefaultFlexibleActivitySessionsPerWeek,
                StringComparer.Ordinal);
    }
}
