using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Reduces an eligible demanding occurrence to its existing conservative duration policy.</summary>
public static class SameDayDemandAdjustmentRule
{
    /// <summary>Lowers the session's demand when it shares a day with harder work, or returns null when nothing changes.</summary>
    public static ActivitySessionPrescription? Adjust(ActivitySessionPrescription? original)
    {
        if (original is null) return null;
        decimal? minimum = original.Activity switch
        {
            WorkoutActivityType.RockClimbing => ActivityPrescriptionRule.BeginnerClimbingVisitMinutes,
            WorkoutActivityType.Yoga when original.TrainingStyle == SessionTrainingStyle.Demanding =>
                ActivitySessionDurationRule.DemandingYogaMinimumMinutes,
            _ => null
        };
        if (minimum is null || original.MainMinutes is null || original.MainMinutes <= minimum ||
            original.WarmupMinutes is < 0 || original.CooldownMinutes is < 0)
            return null;
        return original with { MainMinutes = minimum };
    }

    /// <summary>True when the adjusted session stays within what the day can carry.</summary>
    public static bool IsValid(ActivitySessionPrescription? original,
        ActivitySessionPrescription? occurrence, decimal? durationMinutes)
    {
        var expected = Adjust(original);
        if (expected is null || occurrence is null || durationMinutes is null) return false;
        // Reasons are explanatory and may be independently deserialized.
        if (expected with { Reasons = occurrence.Reasons } != occurrence) return false;
        return durationMinutes == TotalMinutes(occurrence);
    }

    /// <summary>Total minutes a prescription prescribes across its segments.</summary>
    public static decimal TotalMinutes(ActivitySessionPrescription prescription) =>
        (prescription.WarmupMinutes ?? decimal.Zero) + (prescription.MainMinutes ?? decimal.Zero) +
        (prescription.CooldownMinutes ?? decimal.Zero);
}
