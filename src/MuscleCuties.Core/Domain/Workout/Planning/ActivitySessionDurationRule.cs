using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record ActivitySessionDurationPolicy(
    int MinimumMinutes,
    int MaximumMinutes,
    int IncrementMinutes,
    int? DefaultMinutes,
    bool RequiresExplicitChoice);

public enum ActivitySessionDurationSelectionIssue
{
    UnsupportedActivity,
    BelowMinimum,
    AboveMaximum,
    InvalidIncrement
}

/// <summary>Defines the user preference duration choices for each activity in one place.</summary>
public static class ActivitySessionDurationRule
{
    public const int StandardMinimumMinutes = 30;
    public const int StandardMaximumMinutes = 90;
    public const int StandardIncrementMinutes = 15;
    public const int StandardDefaultMinutes = 60;
    public const int ThirtyMinutes = 30;
    public const int FortyFiveMinutes = 45;
    public const int SixtyMinutes = 60;
    public const int SeventyFiveMinutes = 75;
    public const int NinetyMinutes = 90;
    public const int GentleYogaMinimumMinutes = 10;
    public const int GentleYogaMaximumMinutes = 20;
    public const int GentleYogaIncrementMinutes = 5;
    public const int GentleYogaDefaultMinutes = 15;
    public const int GentleMobilityMinimumMinutes = 5;
    public const int GentleMobilityMaximumMinutes = 15;
    public const int GentleMobilityIncrementMinutes = 5;
    public const int GentleMobilityDefaultMinutes = 10;
    public const int GentleRecoveryMinimumMinutes = 5;
    public const int GentleRecoveryMaximumMinutes = 20;
    public const int GentleRecoveryIncrementMinutes = 5;
    public const int GentleRecoveryDefaultMinutes = 10;
    public const int SwimmingMinimumMinutes = 25;
    public const int SwimmingMaximumMinutes = 30;
    public const int SwimmingIncrementMinutes = 5;
    public const int SwimmingDefaultMinutes = 30;
    public const int PositiveDurationMinimumMinutes = 1;
    public const int ValidIncrementRemainder = 0;
    public const int FirstChoiceIndex = 0;
    public const int MaximumSupportedDurationMinutes = int.MaxValue;

    private const int QuizChoiceThirtyMinutes = 1;
    private const int QuizChoiceFortyFiveMinutes = 2;
    private const int QuizChoiceSixtyMinutes = 3;
    private const int QuizChoiceSeventyFiveMinutes = 4;
    private const int QuizChoiceNinetyMinutes = 5;

    private static readonly ActivitySessionDurationPolicy StandardPolicy = new(
        StandardMinimumMinutes,
        StandardMaximumMinutes,
        StandardIncrementMinutes,
        StandardDefaultMinutes,
        false);

    private static readonly ActivitySessionDurationPolicy GentleYogaPolicy = new(
        GentleYogaMinimumMinutes,
        GentleYogaMaximumMinutes,
        GentleYogaIncrementMinutes,
        GentleYogaDefaultMinutes,
        false);

    private static readonly ActivitySessionDurationPolicy GentleMobilityPolicy = new(
        GentleMobilityMinimumMinutes,
        GentleMobilityMaximumMinutes,
        GentleMobilityIncrementMinutes,
        GentleMobilityDefaultMinutes,
        false);

    private static readonly ActivitySessionDurationPolicy GentleRecoveryPolicy = new(
        GentleRecoveryMinimumMinutes,
        GentleRecoveryMaximumMinutes,
        GentleRecoveryIncrementMinutes,
        GentleRecoveryDefaultMinutes,
        false);

    private static readonly ActivitySessionDurationPolicy SwimmingPolicy = new(
        SwimmingMinimumMinutes,
        SwimmingMaximumMinutes,
        SwimmingIncrementMinutes,
        SwimmingDefaultMinutes,
        false);

    public static bool IsStrengthActivity(WorkoutActivityType activity) => activity is
        WorkoutActivityType.GeneralStrength or
        WorkoutActivityType.HighVolumeStrength or
        WorkoutActivityType.StrengthHighIntensity;

    public static bool TryResolveStrengthQuizSessionDurationMinutes(int mappedValue, out int durationMinutes)
    {
        durationMinutes = mappedValue switch
        {
            QuizChoiceThirtyMinutes or ThirtyMinutes => ThirtyMinutes,
            QuizChoiceFortyFiveMinutes or FortyFiveMinutes => FortyFiveMinutes,
            QuizChoiceSixtyMinutes or SixtyMinutes => SixtyMinutes,
            QuizChoiceSeventyFiveMinutes or SeventyFiveMinutes => SeventyFiveMinutes,
            QuizChoiceNinetyMinutes or NinetyMinutes => NinetyMinutes,
            _ => ValidIncrementRemainder
        };
        return durationMinutes > ValidIncrementRemainder;
    }

    public static int ResolveStrengthQuizSessionDurationMinutes(int mappedValue) =>
        TryResolveStrengthQuizSessionDurationMinutes(mappedValue, out var minutes)
            ? minutes
            : StandardDefaultMinutes;

    public static ActivitySessionDurationPolicy? ForUserPreference(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.GeneralStrength or WorkoutActivityType.HighVolumeStrength or
            WorkoutActivityType.StrengthHighIntensity or WorkoutActivityType.Cycling or
            WorkoutActivityType.Running or WorkoutActivityType.Walking or WorkoutActivityType.Rowing or
            WorkoutActivityType.Elliptical => StandardPolicy,
        WorkoutActivityType.Swimming => SwimmingPolicy,
        WorkoutActivityType.Yoga => GentleYogaPolicy,
        WorkoutActivityType.Mobility => GentleMobilityPolicy,
        WorkoutActivityType.GentleRecovery => GentleRecoveryPolicy,
        _ => null
    };

    public static bool RequiresUserPreference(WorkoutActivityType activity) =>
        ForUserPreference(activity) is not null;

    public static bool RequiresExplicitChoice(WorkoutActivityType activity) =>
        ForUserPreference(activity)?.RequiresExplicitChoice == true;

    public static int? DefaultMinutesFor(WorkoutActivityType activity) =>
        ForUserPreference(activity)?.DefaultMinutes;

    public static bool IsValidSelection(WorkoutActivityType activity, int minutes)
    {
        return GetSelectionIssue(activity, minutes) is null;
    }

    public static ActivitySessionDurationSelectionIssue? GetSelectionIssue(
        WorkoutActivityType activity,
        int minutes)
    {
        var policy = ForUserPreference(activity);
        if (policy is null)
            return ActivitySessionDurationSelectionIssue.UnsupportedActivity;
        if (minutes < policy.MinimumMinutes)
            return ActivitySessionDurationSelectionIssue.BelowMinimum;
        if (minutes > policy.MaximumMinutes)
            return ActivitySessionDurationSelectionIssue.AboveMaximum;

        return policy.RequiresExplicitChoice ||
               (minutes - policy.MinimumMinutes) % policy.IncrementMinutes == ValidIncrementRemainder
            ? null
            : ActivitySessionDurationSelectionIssue.InvalidIncrement;
    }

    public static bool IsValidScheduledDuration(WorkoutActivityType activity, decimal minutes)
    {
        if (decimal.Truncate(minutes) != minutes || minutes > int.MaxValue || minutes < int.MinValue)
            return false;

        return IsValidSelection(activity, decimal.ToInt32(minutes));
    }

    public static IReadOnlyList<int> ChoicesFor(WorkoutActivityType activity)
    {
        var policy = ForUserPreference(activity);
        if (policy is null || policy.RequiresExplicitChoice)
            return [];

        var count = (policy.MaximumMinutes - policy.MinimumMinutes) / policy.IncrementMinutes;
        return Enumerable.Range(FirstChoiceIndex, count + 1)
            .Select(index => policy.MinimumMinutes + index * policy.IncrementMinutes)
            .ToArray();
    }
}
