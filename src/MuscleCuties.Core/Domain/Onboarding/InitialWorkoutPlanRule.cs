namespace MuscleCuties.Core.Domain.Onboarding;

public sealed record InitialWorkoutPlanDayObservation(
    int WeekIndex,
    int DayOfWeek,
    IReadOnlyList<InitialWorkoutPlanExerciseObservation> Exercises,
    bool HasValidWorkoutType,
    bool HasName);

public sealed record InitialWorkoutPlanExerciseObservation(int ExerciseId, int Sets, int Reps);

public static class InitialWorkoutPlanRule
{
    public const int MinimumWeekIndex = 1;
    public const int MaximumWeekIndex = 4;
    public const int MinimumDayOfWeek = 0;
    public const int MaximumDayOfWeek = 6;
    public const int MinimumSetsPerExercise = 1;
    public const int MinimumRepetitionsPerExercise = 1;

    public static OnboardingValidationResult Validate(IReadOnlyList<InitialWorkoutPlanDayObservation>? days)
    {
        if (days is null || days.Count == 0)
            return OnboardingValidationResult.Failure("Choose at least one day for your initial workout plan.");

        if (days.Any(day => day.WeekIndex is < MinimumWeekIndex or > MaximumWeekIndex ||
                            day.DayOfWeek is < MinimumDayOfWeek or > MaximumDayOfWeek ||
                            day.Exercises is null || day.Exercises.Count == 0 ||
                            day.Exercises.Any(exercise => exercise.ExerciseId <= 0 ||
                                exercise.Sets < MinimumSetsPerExercise ||
                                exercise.Reps < MinimumRepetitionsPerExercise) ||
                            !day.HasValidWorkoutType || !day.HasName))
            return OnboardingValidationResult.Failure("One or more initial workout plan days are invalid.");

        if (days.GroupBy(day => (day.WeekIndex, day.DayOfWeek)).Any(group => group.Count() != 1))
            return OnboardingValidationResult.Failure("The initial workout plan contains duplicate scheduled days.");

        if (days.Any(day => day.Exercises.Select(exercise => exercise.ExerciseId).Distinct().Count() !=
                            day.Exercises.Count))
            return OnboardingValidationResult.Failure("An exercise can only appear once on a plan day.");

        return OnboardingValidationResult.Success();
    }
}
