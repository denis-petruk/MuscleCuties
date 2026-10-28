namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>A deload only affects the bounded occurrence window; later prescriptions stay unchanged.</summary>
public static class CanonicalOccurrenceAdjustmentRule
{
    public const int DeloadDurationDays = 7;

    public static bool IsInDeloadWindow(DateOnly occurrence, DateOnly start, DateOnly endExclusive) =>
        endExclusive > start && occurrence >= start && occurrence < endExclusive;

    public static (int Sets, int TargetRir) Deload(int sets, int targetRir) =>
        (DeloadTriggerRule.CalculateDeloadSets(sets), DeloadTriggerRule.CalculateDeloadRir(targetRir));
}
