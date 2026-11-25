using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Reads her workout preferences, filling only what she actually chose and leaving the rest to the policy.</summary>
public static class WorkoutPreferencesRule
{
    public const int MinimumDaysPerWeek = 1;
    public const int MaximumDaysPerWeek = 7;
    private const int MinimumAdaptiveDaysPerWeek = 2;

    /// <summary>Clamps the chosen training days into the range the flow offers.</summary>
    public static int ClampDaysPerWeek(int daysPerWeek) =>
        Math.Clamp(daysPerWeek, MinimumAdaptiveDaysPerWeek, MaximumDaysPerWeek);

    public const int MinimumActivitySessionDurationMinutes =
        ActivitySessionDurationRule.StandardMinimumMinutes;
    public const int MaximumActivitySessionDurationMinutes =
        ActivitySessionDurationRule.StandardMaximumMinutes;
    public const int ActivitySessionDurationIncrementMinutes =
        ActivitySessionDurationRule.StandardIncrementMinutes;
    public const int DefaultActivitySessionDurationMinutes =
        ActivitySessionDurationRule.StandardDefaultMinutes;

    /// <summary>True when the activity needs a session length chosen.</summary>
    public static bool RequiresSessionDuration(WorkoutActivityType activity) =>
        ActivitySessionDurationRule.RequiresUserPreference(activity);

    /// <summary>True when the activity cannot use a default length.</summary>
    public static bool RequiresExplicitSessionDuration(WorkoutActivityType activity) =>
        ActivitySessionDurationRule.RequiresExplicitChoice(activity);

    /// <summary>The session length the activity assumes.</summary>
    public static int? DefaultSessionDurationMinutes(WorkoutActivityType activity) =>
        ActivitySessionDurationRule.DefaultMinutesFor(activity);

    /// <summary>The session length the activity assumes, allowing for demanding yoga.</summary>
    public static int? DefaultSessionDurationMinutes(WorkoutActivityType activity, bool isDemandingYoga) =>
        ActivitySessionDurationRule.DefaultMinutesFor(activity, isDemandingYoga);

    /// <summary>True when the length is one the activity offers.</summary>
    public static bool IsValidSessionDuration(WorkoutActivityType activity, int minutes) =>
        activity == WorkoutActivityType.Yoga
            ? ActivitySessionDurationRule.IsValidSelection(activity, minutes) ||
              ActivitySessionDurationRule.IsValidSelection(activity, minutes, isDemandingYoga: true)
            : ActivitySessionDurationRule.IsValidSelection(activity, minutes);

    /// <summary>The session lengths the activity offers.</summary>
    public static IReadOnlyList<int> SessionDurationChoices(WorkoutActivityType activity) =>
        ActivitySessionDurationRule.ChoicesFor(activity);

    /// <summary>The session lengths the activity offers, allowing for demanding yoga.</summary>
    public static IReadOnlyList<int> SessionDurationChoices(WorkoutActivityType activity, bool isDemandingYoga) =>
        ActivitySessionDurationRule.ChoicesFor(activity, isDemandingYoga);

    /// <summary>True when the activity's weekly frequency must be chosen.</summary>
    public static bool RequiresExplicitWeeklyFrequency(WorkoutActivityType activity) =>
        ActivityFrequencyPreferenceRule.RequiresExplicitWeeklyFrequency(activity);

    /// <summary>True when the chosen sessions per week is offered.</summary>
    public static bool IsValidRequestedWeeklyFrequency(int sessionsPerWeek) =>
        ActivityFrequencyPreferenceRule.IsValidRequestedFrequency(sessionsPerWeek);

    /// <summary>The weekly frequencies the flow offers.</summary>
    public static IReadOnlyList<int> WeeklyFrequencyChoices() =>
        ActivityFrequencyPreferenceRule.FrequencyChoices();

    /// <summary>True when the activity is a strength style.</summary>
    public static bool IsStrengthStyle(WorkoutActivityType activity) => activity is
        WorkoutActivityType.GeneralStrength or
        WorkoutActivityType.HighVolumeStrength or
        WorkoutActivityType.StrengthHighIntensity;

    /// <summary>True when the chosen activity ids include a strength style.</summary>
    public static bool HasAtLeastOneStrengthStyle(IReadOnlyCollection<int>? activityTypes) =>
        activityTypes is { Count: > 0 } &&
        activityTypes.Any(value => Enum.IsDefined(typeof(WorkoutActivityType), value) &&
            IsStrengthStyle((WorkoutActivityType)value));

    /// <summary>True when the chosen activities include a strength style.</summary>
    public static bool HasAtLeastOneStrengthStyle(IReadOnlyCollection<WorkoutActivityType>? activities) =>
        activities is { Count: > 0 } &&
        activities.Any(IsStrengthStyle);

    /// <summary>True when enough distinct weekdays were chosen.</summary>
    public static bool HasMinimumDistinctWeekdays(IReadOnlyCollection<int>? availableWeekdays) =>
        availableWeekdays is { Count: >= MinimumDaysPerWeek and <= MaximumDaysPerWeek } &&
        availableWeekdays.All(value => Enum.IsDefined(typeof(DayOfWeek), value)) &&
        availableWeekdays.Distinct().Count() == availableWeekdays.Count;

    /// <summary>True when enough distinct weekdays were chosen.</summary>
    public static bool HasMinimumDistinctWeekdays(IReadOnlyCollection<DayOfWeek>? availableWeekdays) =>
        availableWeekdays is { Count: >= MinimumDaysPerWeek and <= MaximumDaysPerWeek } &&
        availableWeekdays.All(day => Enum.IsDefined(day)) &&
        availableWeekdays.Distinct().Count() == availableWeekdays.Count;

    /// <summary>True when the preferences carry every value the planner needs.</summary>
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
    /// <summary>True when the applied equipment is the one the user asked for.</summary>
    public static bool PreservesAppliedEquipment(string? requested, string? applied) =>
        requested is null || string.Equals(requested, applied, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the equipment level is one the planner supports.</summary>
    public static bool IsValidEquipmentLevel(string? equipmentLevel) =>
        equipmentLevel is null ||
        Enum.TryParse<Equipment>(equipmentLevel, true, out var equipment) && Enum.IsDefined(equipment);
}
