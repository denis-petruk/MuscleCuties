using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>A deload only affects the bounded occurrence window; later prescriptions stay unchanged.</summary>
public static class CanonicalOccurrenceAdjustmentRule
{
    public const int DeloadDurationDays = 7;

    /// <summary>True when the occurrence falls inside the deload window.</summary>
    public static bool IsInDeloadWindow(DateOnly occurrence, DateOnly start, DateOnly endExclusive) =>
        endExclusive > start && occurrence >= start && occurrence < endExclusive;

    /// <summary>Sets and effort a deload week keeps from a normal occurrence.</summary>
    public static (int Sets, int TargetRir) Deload(int sets, int targetRir) =>
        (DeloadTriggerRule.CalculateDeloadSets(sets), DeloadTriggerRule.CalculateDeloadRir(targetRir));

    /// <summary>True when an optional companion must be dropped because the day is already hard.</summary>
    public static bool RemoveOptionalHardCompanion(bool optional, WorkoutActivityType activity,
        bool hasStrengthOnSameDay, DateOnly occurrence, DateOnly start, DateOnly endExclusive) =>
        optional && activity == WorkoutActivityType.Hiit && hasStrengthOnSameDay &&
        IsInDeloadWindow(occurrence, start, endExclusive);
}
