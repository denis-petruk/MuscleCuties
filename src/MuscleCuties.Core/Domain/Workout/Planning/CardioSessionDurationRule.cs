using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>
/// Per-modality limits for continuous cardio: how long a cardio-only day runs and the
/// longest session the modality may ever be scheduled for.
/// </summary>
public static class CardioSessionDurationRule
{
    /// <summary>Owner rule: cardio or recovery that follows a strength session is capped hard at 30 minutes.</summary>
    public const int PostStrengthCardioOrRecoveryMaximumMinutes = 30;

    public const int CyclingStandaloneDayMinutes = 60;
    public const int CyclingMaximumSessionMinutes = 120;
    public const int SwimmingStandaloneDayMinutes = 60;
    public const int SwimmingMaximumSessionMinutes = 60;
    public const int RunningBeginnerStandaloneDayMinutes = 30;
    public const int RunningEstablishedStandaloneDayMinutes = 45;
    public const int RunningMaximumSessionMinutes = 60;
    public const int WalkingOrRowingStandaloneDayBeginnerMinutes = 30;
    public const int WalkingOrRowingStandaloneDayEstablishedMinutes = 45;
    public const int WalkingOrRowingMaximumSessionMinutes = 90;

    public static decimal StandaloneDayMinutes(WorkoutActivityType activity, ActivityExperience experience) =>
        activity switch
        {
            WorkoutActivityType.Cycling => CyclingStandaloneDayMinutes,
            WorkoutActivityType.Swimming => SwimmingStandaloneDayMinutes,
            WorkoutActivityType.Running => experience == ActivityExperience.Established
                ? RunningEstablishedStandaloneDayMinutes
                : RunningBeginnerStandaloneDayMinutes,
            _ => experience == ActivityExperience.Established
                ? WalkingOrRowingStandaloneDayEstablishedMinutes
                : WalkingOrRowingStandaloneDayBeginnerMinutes
        };

    public static decimal MaximumSessionMinutes(WorkoutActivityType activity) =>
        activity switch
        {
            WorkoutActivityType.Cycling => CyclingMaximumSessionMinutes,
            WorkoutActivityType.Swimming => SwimmingMaximumSessionMinutes,
            WorkoutActivityType.Running => RunningMaximumSessionMinutes,
            _ => WalkingOrRowingMaximumSessionMinutes
        };
}
