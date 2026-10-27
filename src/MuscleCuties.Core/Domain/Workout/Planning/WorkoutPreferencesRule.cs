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

    public static int? DefaultSessionDurationMinutes(WorkoutActivityType activity, bool isDemandingYoga) =>
        ActivitySessionDurationRule.DefaultMinutesFor(activity, isDemandingYoga);

    public static bool IsValidSessionDuration(WorkoutActivityType activity, int minutes) =>
        activity == WorkoutActivityType.Yoga
            ? ActivitySessionDurationRule.IsValidSelection(activity, minutes) ||
              ActivitySessionDurationRule.IsValidSelection(activity, minutes, isDemandingYoga: true)
            : ActivitySessionDurationRule.IsValidSelection(activity, minutes);

    public static IReadOnlyList<int> SessionDurationChoices(WorkoutActivityType activity) =>
        ActivitySessionDurationRule.ChoicesFor(activity);

    public static IReadOnlyList<int> SessionDurationChoices(WorkoutActivityType activity, bool isDemandingYoga) =>
        ActivitySessionDurationRule.ChoicesFor(activity, isDemandingYoga);

    public static bool RequiresExplicitWeeklyFrequency(WorkoutActivityType activity) =>
        ActivityFrequencyPreferenceRule.RequiresExplicitWeeklyFrequency(activity);

    public static bool IsValidRequestedWeeklyFrequency(int sessionsPerWeek) =>
        ActivityFrequencyPreferenceRule.IsValidRequestedFrequency(sessionsPerWeek);

    public static IReadOnlyList<int> WeeklyFrequencyChoices() =>
        ActivityFrequencyPreferenceRule.FrequencyChoices();

    public static bool IsStrengthStyle(WorkoutActivityType activity) => activity is
        WorkoutActivityType.GeneralStrength or
        WorkoutActivityType.HighVolumeStrength or
        WorkoutActivityType.StrengthHighIntensity;

    public static bool HasAtLeastOneStrengthStyle(IReadOnlyCollection<int>? activityTypes) =>
        activityTypes is { Count: > 0 } &&
        activityTypes.Any(value => Enum.IsDefined(typeof(WorkoutActivityType), value) &&
            IsStrengthStyle((WorkoutActivityType)value));

    public static bool HasAtLeastOneStrengthStyle(IReadOnlyCollection<WorkoutActivityType>? activities) =>
        activities is { Count: > 0 } &&
        activities.Any(IsStrengthStyle);

    public static bool HasMinimumDistinctWeekdays(IReadOnlyCollection<int>? availableWeekdays) =>
        availableWeekdays is { Count: >= MinimumDaysPerWeek and <= MaximumDaysPerWeek } &&
        availableWeekdays.All(value => Enum.IsDefined(typeof(DayOfWeek), value)) &&
        availableWeekdays.Distinct().Count() == availableWeekdays.Count;

    public static bool HasMinimumDistinctWeekdays(IReadOnlyCollection<DayOfWeek>? availableWeekdays) =>
        availableWeekdays is { Count: >= MinimumDaysPerWeek and <= MaximumDaysPerWeek } &&
        availableWeekdays.All(day => Enum.IsDefined(typeof(DayOfWeek), day)) &&
        availableWeekdays.Distinct().Count() == availableWeekdays.Count;

    public static bool IsValid(
        IReadOnlyList<int>? activityTypes,
        IReadOnlyList<int>? availableWeekdays,
        IReadOnlyDictionary<int, int>? activitySessionDurationsMinutes,
        IReadOnlyDictionary<int, int>? requestedSessionsPerWeek = null)
    {
        if (activityTypes is not { Count: > 0 } ||
            activityTypes.Any(value => !Enum.IsDefined(typeof(WorkoutActivityType), value)) ||
            activityTypes.Contains((int)WorkoutActivityType.Other) ||
            !HasAtLeastOneStrengthStyle(activityTypes) ||
            activityTypes.Distinct().Count() != activityTypes.Count ||
            !HasMinimumDistinctWeekdays(availableWeekdays) ||
            activitySessionDurationsMinutes is null)
            return false;

        var activitiesRequiringDuration = activityTypes
            .Select(value => (WorkoutActivityType)value)
            .Where(RequiresSessionDuration)
            .Select(activity => (int)activity)
            .ToArray();

        if (!activitiesRequiringDuration.All(activitySessionDurationsMinutes.ContainsKey) ||
            !activitySessionDurationsMinutes.All(pair =>
                activityTypes.Contains(pair.Key) &&
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

    // Equipment is part of applied workout preferences and may only change with a plan.
    public static bool PreservesAppliedEquipment(string? requested, string? applied) =>
        requested is null || string.Equals(requested, applied, StringComparison.OrdinalIgnoreCase);

    public static bool IsValidEquipmentLevel(string? equipmentLevel) =>
        equipmentLevel is null ||
        Enum.TryParse<Equipment>(equipmentLevel, true, out var equipment) && Enum.IsDefined(equipment);
}
