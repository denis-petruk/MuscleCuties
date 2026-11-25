using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.UseCases.Features.Workout.Contracts;

/// <summary>Maps planned activities onto the wire DTO, so the planner's enums stay internal.</summary>
public static class WorkoutActivityContractMapper
{
    /// <summary>The domain activity for its wire value.</summary>
    public static WorkoutActivityType ToCore(WorkoutActivityDto activity) => activity switch
    {
        WorkoutActivityDto.StrengthHighIntensity => WorkoutActivityType.StrengthHighIntensity,
        WorkoutActivityDto.HighVolumeStrength => WorkoutActivityType.HighVolumeStrength,
        WorkoutActivityDto.RockClimbing => WorkoutActivityType.RockClimbing,
        WorkoutActivityDto.Yoga => WorkoutActivityType.Yoga,
        WorkoutActivityDto.Hiit => WorkoutActivityType.Hiit,
        WorkoutActivityDto.Cycling => WorkoutActivityType.Cycling,
        WorkoutActivityDto.Running => WorkoutActivityType.Running,
        WorkoutActivityDto.Swimming => WorkoutActivityType.Swimming,
        WorkoutActivityDto.GeneralStrength => WorkoutActivityType.GeneralStrength,
        WorkoutActivityDto.Walking => WorkoutActivityType.Walking,
        WorkoutActivityDto.Rowing => WorkoutActivityType.Rowing,
        WorkoutActivityDto.Elliptical => WorkoutActivityType.Elliptical,
        WorkoutActivityDto.Mobility => WorkoutActivityType.Mobility,
        WorkoutActivityDto.GentleRecovery => WorkoutActivityType.GentleRecovery,
        WorkoutActivityDto.RestDay => WorkoutActivityType.RestDay,
        WorkoutActivityDto.Other => WorkoutActivityType.Other,
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, "Unknown workout activity.")
    };

    /// <summary>The wire value for a domain activity.</summary>
    public static WorkoutActivityDto ToDto(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.StrengthHighIntensity => WorkoutActivityDto.StrengthHighIntensity,
        WorkoutActivityType.HighVolumeStrength => WorkoutActivityDto.HighVolumeStrength,
        WorkoutActivityType.RockClimbing => WorkoutActivityDto.RockClimbing,
        WorkoutActivityType.Yoga => WorkoutActivityDto.Yoga,
        WorkoutActivityType.Hiit => WorkoutActivityDto.Hiit,
        WorkoutActivityType.Cycling => WorkoutActivityDto.Cycling,
        WorkoutActivityType.Running => WorkoutActivityDto.Running,
        WorkoutActivityType.Swimming => WorkoutActivityDto.Swimming,
        WorkoutActivityType.GeneralStrength => WorkoutActivityDto.GeneralStrength,
        WorkoutActivityType.Walking => WorkoutActivityDto.Walking,
        WorkoutActivityType.Rowing => WorkoutActivityDto.Rowing,
        WorkoutActivityType.Elliptical => WorkoutActivityDto.Elliptical,
        WorkoutActivityType.Mobility => WorkoutActivityDto.Mobility,
        WorkoutActivityType.GentleRecovery => WorkoutActivityDto.GentleRecovery,
        WorkoutActivityType.RestDay => WorkoutActivityDto.RestDay,
        WorkoutActivityType.Other => WorkoutActivityDto.Other,
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, "Unknown workout activity.")
    };
}
