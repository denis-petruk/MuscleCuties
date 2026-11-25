using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>The planner's fixed limits, in one place so a change is a single edit.</summary>
public static class WorkoutPlanningConstants
{
    public const int HiitRequiredPriorWeeks = ActivityPrescriptionRule.HiitRequiredPriorWeeks;
    public const int MinimumConsistentWeeksForEstablished = ActivityExperienceRule.MinimumConsistentWeeksForEstablished;
    public const int EstablishedSessionsPerWeek = ActivityExperienceRule.EstablishedSessionsPerWeek;
    public const int ReturningInactivityDays = ActivityExperienceRule.ReturningInactivityDays;
    public const int HiitMinimumManageableSessionMinutes = ActivityPrescriptionRule.HiitMinimumManageableSessionMinutes;
    public const int HiitRequiredAerobicSessionsPerWeek = ActivityPrescriptionRule.HiitRequiredAerobicSessionsPerWeek;

    public static readonly WorkoutActivityDto[] SupportedHiitUnderlyingCardioActivities =
    [
        WorkoutActivityDto.Cycling,
        WorkoutActivityDto.Running,
        WorkoutActivityDto.Rowing,
        WorkoutActivityDto.Elliptical
    ];

    public static readonly WorkoutActivityDto[] StrengthActivities =
    [
        WorkoutActivityDto.GeneralStrength,
        WorkoutActivityDto.HighVolumeStrength,
        WorkoutActivityDto.StrengthHighIntensity
    ];

    /// <summary>True when the activity may carry the HIIT prescription.</summary>
    public static bool IsSupportedHiitUnderlyingCardioActivity(WorkoutActivityDto activity) =>
        activity is WorkoutActivityDto.Cycling or WorkoutActivityDto.Running or
            WorkoutActivityDto.Rowing or WorkoutActivityDto.Elliptical;

    /// <summary>True when the activity is a strength session.</summary>
    public static bool IsStrengthActivity(WorkoutActivityDto activity) =>
        activity is WorkoutActivityDto.GeneralStrength or WorkoutActivityDto.HighVolumeStrength or
            WorkoutActivityDto.StrengthHighIntensity;
}
