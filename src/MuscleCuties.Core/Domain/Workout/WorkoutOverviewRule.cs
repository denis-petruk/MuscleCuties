namespace MuscleCuties.Core.Domain.Workout;

/// <summary>Assembles the overview's derived values, so the client shows one consistent week.</summary>
public static class WorkoutOverviewRule
{
    /// <summary>True when the optional-session choice is one the overview supports.</summary>
    public static bool IsValidSessionOptionality(
        int workoutDayId,
        bool isOptional,
        bool isEasierAlternative) =>
        WorkoutSessionActionRule.IsValidIdentifier(workoutDayId) &&
        (!isEasierAlternative || isOptional);
}
