namespace MuscleCuties.Core.Domain.Workout;

public readonly record struct NormalizedWorkoutInjuryEntry(int Pain, DateOnly Since);

public static class WorkoutInjuryEntryRule
{
    public const int NoInjurySite = 0;
    public const int MinimumPain = 0;
    public const int MaximumPain = 5;

    public static bool TryNormalize(
        int site,
        int pain,
        DateOnly since,
        DateOnly today,
        out NormalizedWorkoutInjuryEntry normalized)
    {
        if (site == NoInjurySite)
        {
            normalized = default;
            return false;
        }

        normalized = new(
            Math.Clamp(pain, MinimumPain, MaximumPain),
            since > today ? today : since);
        return true;
    }
}
