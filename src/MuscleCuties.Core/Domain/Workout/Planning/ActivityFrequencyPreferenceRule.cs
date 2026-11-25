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

    /// <summary>True when the activity's weekly frequency must be chosen rather than derived.</summary>
    public static bool RequiresExplicitWeeklyFrequency(WorkoutActivityType activity) =>
        ExplicitPreferenceActivities.Contains(activity);

    /// <summary>True when the requested sessions per week is a value the flow offers.</summary>
    public static bool IsValidRequestedFrequency(int sessionsPerWeek) =>
        sessionsPerWeek is >= MinimumSessionsPerWeek and <= MaximumSessionsPerWeek;

    /// <summary>The weekly frequencies the flow offers for an activity.</summary>
    public static IReadOnlyList<int> FrequencyChoices() =>
        Enumerable.Range(MinimumSessionsPerWeek,
            MaximumSessionsPerWeek - MinimumSessionsPerWeek + 1).ToArray();
}
