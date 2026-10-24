using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public static class WorkoutPreferencesRule
{
    public const int MinimumDaysPerWeek = WeekPlanRule.MinimumDaysPerWeek;
    public const int MaximumDaysPerWeek = WeekPlanRule.MaximumDaysPerWeek;

    public const int MinimumActivitySessionDurationMinutes =
        ActivitySessionDurationRule.StandardMinimumMinutes;
    public const int MaximumActivitySessionDurationMinutes =
        ActivitySessionDurationRule.StandardMaximumMinutes;
    public const int ActivitySessionDurationIncrementMinutes =
        ActivitySessionDurationRule.StandardIncrementMinutes;
    public const int DefaultActivitySessionDurationMinutes =
        ActivitySessionDurationRule.StandardDefaultMinutes;

    public static bool RequiresSessionDuration(WorkoutActivityType activity) =>
        ActivitySessionDurationRule.RequiresUserPreference(activity);

    public static bool RequiresExplicitSessionDuration(WorkoutActivityType activity) =>
        ActivitySessionDurationRule.RequiresExplicitChoice(activity);

    public static int? DefaultSessionDurationMinutes(WorkoutActivityType activity) =>
        ActivitySessionDurationRule.DefaultMinutesFor(activity);

    public static bool IsValidSessionDuration(WorkoutActivityType activity, int minutes) =>
        ActivitySessionDurationRule.IsValidSelection(activity, minutes);

    public static IReadOnlyList<int> SessionDurationChoices(WorkoutActivityType activity) =>
        ActivitySessionDurationRule.ChoicesFor(activity);

    public static bool RequiresExplicitWeeklyFrequency(WorkoutActivityType activity) =>
        ActivityFrequencyPreferenceRule.RequiresExplicitWeeklyFrequency(activity);

    public static bool IsValidRequestedWeeklyFrequency(int sessionsPerWeek) =>
        ActivityFrequencyPreferenceRule.IsValidRequestedFrequency(sessionsPerWeek);

    public static IReadOnlyList<int> WeeklyFrequencyChoices() =>
        ActivityFrequencyPreferenceRule.FrequencyChoices();

    public static bool IsValid(
        IReadOnlyList<int>? activityTypes,
        IReadOnlyList<int>? availableWeekdays,
        IReadOnlyDictionary<int, int>? activitySessionDurationsMinutes,
        IReadOnlyDictionary<int, int>? requestedSessionsPerWeek = null)
    {
        if (activityTypes is not { Count: > 0 } ||
            activityTypes.Any(value => !Enum.IsDefined(typeof(WorkoutActivityType), value)) ||
            activityTypes.Contains((int)WorkoutActivityType.Other) ||
            activityTypes.Distinct().Count() != activityTypes.Count ||
            availableWeekdays is not { Count: >= MinimumDaysPerWeek and <= MaximumDaysPerWeek } ||
            availableWeekdays.Any(value => !Enum.IsDefined(typeof(DayOfWeek), value)) ||
            availableWeekdays.Distinct().Count() != availableWeekdays.Count ||
            activitySessionDurationsMinutes is null)
            return false;

        var activitiesRequiringDuration = activityTypes
            .Select(value => (WorkoutActivityType)value)
            .Where(RequiresSessionDuration)
            .Select(activity => (int)activity)
            .ToArray();

        if (activitySessionDurationsMinutes.Count != activitiesRequiringDuration.Length ||
            !activitySessionDurationsMinutes.Keys.All(activitiesRequiringDuration.Contains) ||
            !activitySessionDurationsMinutes.All(pair =>
                IsValidSessionDuration((WorkoutActivityType)pair.Key, pair.Value)))
            return false;

        var activitiesRequiringFrequency = activityTypes
            .Select(value => (WorkoutActivityType)value)
            .Where(RequiresExplicitWeeklyFrequency)
            .Select(activity => (int)activity)
            .ToArray();
        if (activitiesRequiringFrequency.Length == 0)
            return requestedSessionsPerWeek is null || requestedSessionsPerWeek.Count == 0;

        return requestedSessionsPerWeek is not null &&
               requestedSessionsPerWeek.Count == activitiesRequiringFrequency.Length &&
               requestedSessionsPerWeek.Keys.All(activitiesRequiringFrequency.Contains) &&
               requestedSessionsPerWeek.All(pair => IsValidRequestedWeeklyFrequency(pair.Value));
    }

    public static bool IsValidEquipmentLevel(string? equipmentLevel) =>
        equipmentLevel is null ||
        Enum.TryParse<Equipment>(equipmentLevel, true, out var equipment) && Enum.IsDefined(equipment);
}
