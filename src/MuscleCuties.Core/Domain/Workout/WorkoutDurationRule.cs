namespace MuscleCuties.Core.Domain.Workout;

public readonly record struct WorkoutExerciseDose(int Sets, int Reps);

public static class WorkoutDurationRule
{
    private const int DefaultMinutes = 45;
    private const int MaximumMinutes = 240;
    private const int WarmupSeconds = 12 * 60;
    private const int TransitionSeconds = 3 * 60;

    public static int EstimateStrengthMinutes(IReadOnlyList<WorkoutExerciseDose>? doses)
    {
        if (doses is null || doses.Count == 0 || doses.Count > 100)
            return DefaultMinutes;

        var valid = doses.Where(dose => dose.Sets is > 0 and <= 100 &&
                                        dose.Reps is > 0 and <= 1000).ToArray();
        if (valid.Length == 0)
            return DefaultMinutes;

        var seconds = WarmupSeconds + (long)(valid.Length - 1) * TransitionSeconds;
        foreach (var dose in valid)
        {
            seconds += (long)dose.Sets * dose.Reps * 4;
            seconds += (long)(dose.Sets - 1) * RestSeconds(dose.Reps);
            if (seconds > MaximumMinutes * 60L)
                return MaximumMinutes;
        }

        return Math.Clamp((int)Math.Ceiling(seconds / 60d), DefaultMinutes, MaximumMinutes);
    }

    private static int RestSeconds(int reps)
    {
        return reps <= 8 ? 150 : reps <= 10 ? 120 : 90;
    }
}
