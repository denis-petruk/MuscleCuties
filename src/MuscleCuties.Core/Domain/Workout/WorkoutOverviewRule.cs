namespace MuscleCuties.Core.Domain.Workout;

public static class WorkoutOverviewRule
{
    public static bool IsValidSessionOptionality(
        int workoutDayId,
        bool isOptional,
        bool isEasierAlternative) =>
        WorkoutSessionActionRule.IsValidIdentifier(workoutDayId) &&
        (!isEasierAlternative || isOptional);
}
