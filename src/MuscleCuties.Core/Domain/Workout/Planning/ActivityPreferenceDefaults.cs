using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Conservative starting preferences; the scheduler still applies eligibility and recovery limits.</summary>
public static class ActivityPreferenceDefaults
{
    public const int OptionalSessionsPerWeek = 1;
    public const int ClimbingBaselineSessionsPerWeek = 1;

    public static int? PolicySessionsPerWeek(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.HighVolumeStrength => ActivityPrescriptionRule.HighVolumeStrengthSessionsPerWeek,
        WorkoutActivityType.StrengthHighIntensity =>
            ActivityPrescriptionRule.StrengthHighIntensityMaximumSessionsPerWeekInitially,
        WorkoutActivityType.Cycling or WorkoutActivityType.Running or WorkoutActivityType.Walking or
            WorkoutActivityType.Rowing or WorkoutActivityType.Elliptical or WorkoutActivityType.Swimming =>
            ActivityPrescriptionRule.BeginnerCardioSessionsPerWeek,
        WorkoutActivityType.Hiit => ActivityPrescriptionRule.HiitMaximumSessionsPerWeek,
        WorkoutActivityType.RockClimbing => ClimbingBaselineSessionsPerWeek,
        WorkoutActivityType.Yoga or WorkoutActivityType.Mobility or WorkoutActivityType.GentleRecovery =>
            OptionalSessionsPerWeek,
        _ => null
    };

    public static int? DurationMinutes(WorkoutActivityType activity) =>
        WorkoutPreferencesRule.RequiresSessionDuration(activity)
            ? WorkoutPreferencesRule.DefaultSessionDurationMinutes(activity)
            : null;

    public static int? SessionsPerWeek(WorkoutActivityType activity, int availableWeekdays) =>
        activity switch
        {
            WorkoutActivityType.Yoga or WorkoutActivityType.Mobility or WorkoutActivityType.GentleRecovery =>
                Math.Min(ActivitySessionDurationRule.RecoveryTargetMaxLimit, Math.Max(0, availableWeekdays)),
            _ => !WorkoutPreferencesRule.RequiresExplicitWeeklyFrequency(activity)
                ? null
                : OptionalSessionsPerWeek
        };
}
