using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class SameDayDemandAdjustmentRuleTests
{
    private const int SessionFrequency = 2;
    private const decimal ClimbingMinutes = 90m;
    private const decimal ReducedClimbingMinutes = ActivityPrescriptionRule.BeginnerClimbingVisitMinutes;

    [Fact]
    public void AdjustsCompanionAndValidatesPersistedCopyWithoutChangingStrength()
    {
        var original = Prescription(WorkoutActivityType.RockClimbing, ActivityModality.Climbing);
        var adjusted = SameDayDemandAdjustmentRule.Adjust(original)!;
        Assert.Equal(ReducedClimbingMinutes, adjusted.MainMinutes);
        Assert.Null(SameDayDemandAdjustmentRule.Adjust(Prescription(WorkoutActivityType.GeneralStrength, ActivityModality.Strength)));
        Assert.True(SameDayDemandAdjustmentRule.IsValid(original, adjusted with { Reasons = adjusted.Reasons.ToArray() },
            ReducedClimbingMinutes));
        Assert.False(SameDayDemandAdjustmentRule.IsValid(original, adjusted with { Intensity = IntendedIntensity.Vigorous },
            ReducedClimbingMinutes));
        Assert.False(SameDayDemandAdjustmentRule.IsValid(original, adjusted, ClimbingMinutes));
    }

    [Fact]
    public void UnadjustedDemandingPairCannotPassScheduleValidation()
    {
        var original = Prescription(WorkoutActivityType.RockClimbing, ActivityModality.Climbing);
        var strength = new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.GeneralStrength,
            true, new HashSet<string> { "Legs" });
        var climbing = new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.RockClimbing,
            true, DurationMinutes: ClimbingMinutes, OriginalPrescription: original);
        Assert.Contains(WorkoutScheduleValidationRule.MultipleHardSessions,
            WorkoutScheduleValidationRule.Validate([strength, climbing]));
        var adjusted = SameDayDemandAdjustmentRule.Adjust(original)!;
        Assert.Empty(WorkoutScheduleValidationRule.Validate([strength, climbing with
        { DurationMinutes = ReducedClimbingMinutes, OccurrencePrescription = adjusted }]));
    }

    private static ActivitySessionPrescription Prescription(WorkoutActivityType activity, ActivityModality modality) =>
        new(activity, SessionPurpose.Climbing, modality, IntendedIntensity.Moderate, SessionTrainingStyle.Unspecified,
            SessionFrequency, null, ClimbingMinutes, null, null, null, null, null, null, null, null, null, null,
            false, ["FacilityVisitDuration"]);
}
