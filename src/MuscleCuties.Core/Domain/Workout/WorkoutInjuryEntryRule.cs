namespace MuscleCuties.Core.Domain.Workout;

/// <summary>An injury entry after normalisation, with its site, side and severity.</summary>
public readonly record struct NormalizedWorkoutInjuryEntry(int Pain, DateOnly Since);

/// <summary>Normalises a submitted injury, refusing one without a site or with an unknown severity.</summary>
public static class WorkoutInjuryEntryRule
{
    public const int NoInjurySite = 0;
    public const int MinimumPain = 0;
    public const int MaximumPain = 5;

    /// <summary>Normalizes an injury entry, reporting why it cannot be accepted.</summary>
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
