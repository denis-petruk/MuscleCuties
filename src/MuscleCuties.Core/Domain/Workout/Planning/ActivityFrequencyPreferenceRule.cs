using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Defines activities whose weekly frequency must be selected rather than inferred.</summary>
public static class ActivityFrequencyPreferenceRule
{
    public const int MinimumSessionsPerWeek = 1;
    public const int MaximumSessionsPerWeek = WorkoutPreferencesRule.MaximumDaysPerWeek;

    private static readonly WorkoutActivityType[] ExplicitPreferenceActivities =
    [
        WorkoutActivityType.Yoga,
        WorkoutActivityType.Mobility,
        WorkoutActivityType.GentleRecovery
    ];

    public static bool RequiresExplicitWeeklyFrequency(WorkoutActivityType activity) =>
        ExplicitPreferenceActivities.Contains(activity);

    public static bool IsValidRequestedFrequency(int sessionsPerWeek) =>
        sessionsPerWeek is >= MinimumSessionsPerWeek and <= MaximumSessionsPerWeek;

    public static IReadOnlyList<int> FrequencyChoices() =>
        Enumerable.Range(MinimumSessionsPerWeek,
            MaximumSessionsPerWeek - MinimumSessionsPerWeek + 1).ToArray();
}
