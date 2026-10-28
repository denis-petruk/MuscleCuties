using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutScheduleValidationRuleTests
{
    private const int OneClimbingSessionPerWeek = 1;
    private static ActivitySessionPrescription ClimbingPrescription() => new(
        WorkoutActivityType.RockClimbing, SessionPurpose.Climbing, ActivityModality.Climbing,
        IntendedIntensity.Moderate, SessionTrainingStyle.Unspecified,
        OneClimbingSessionPerWeek, null,
        ActivityPrescriptionRule.EstablishedClimbingVisitMinutesWithoutBaseline, null,
        null, null, null, null, null, null, null, null, null, false, []);

    [Fact]
    public void ReducedClimbingAllowsTwoDemandingSessionsAndPreservesOriginal()
    {
        var original = ClimbingPrescription();
        var adjusted = Assert.IsType<ActivitySessionPrescription>(SameDayDemandAdjustmentRule.Adjust(original));
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.GeneralStrength, true,
                new HashSet<string> { "Legs" }),
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.RockClimbing, true,
                null, SameDayDemandAdjustmentRule.TotalMinutes(adjusted), original, adjusted)
        };

        Assert.Empty(WorkoutScheduleValidationRule.Validate(sessions));
        Assert.Equal(ActivityPrescriptionRule.EstablishedClimbingVisitMinutesWithoutBaseline, original.MainMinutes);
        Assert.Equal(ActivityPrescriptionRule.BeginnerClimbingVisitMinutes, adjusted.MainMinutes);
    }

    [Fact]
    public void ForgedOrMisplacedAdjustmentFailsClosed()
    {
        var original = ClimbingPrescription();
        var adjusted = Assert.IsType<ActivitySessionPrescription>(SameDayDemandAdjustmentRule.Adjust(original));
        var climbing = new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.RockClimbing,
            true, null, SameDayDemandAdjustmentRule.TotalMinutes(adjusted), original, adjusted);
        var strength = new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.GeneralStrength,
            true, new HashSet<string> { "Legs" });

        Assert.Contains(WorkoutScheduleValidationRule.InvalidOccurrenceAdjustment,
            WorkoutScheduleValidationRule.Validate([climbing]));
        var forged = climbing with { OccurrencePrescription = adjusted with { RequiresUserConfirmation = true } };
        var reasons = WorkoutScheduleValidationRule.Validate([strength, forged]);
        Assert.Contains(WorkoutScheduleValidationRule.InvalidOccurrenceAdjustment, reasons);
        Assert.Contains(WorkoutScheduleValidationRule.MultipleHardSessions, reasons);
        var wrongDuration = climbing with { DurationMinutes = original.MainMinutes };
        Assert.Contains(WorkoutScheduleValidationRule.MultipleHardSessions,
            WorkoutScheduleValidationRule.Validate([strength, wrongDuration]));
    }

    [Fact]
    public void AlreadyConservativeClimbingCannotClaimReduction()
    {
        var original = ClimbingPrescription() with { MainMinutes = ActivityPrescriptionRule.BeginnerClimbingVisitMinutes };
        Assert.Null(SameDayDemandAdjustmentRule.Adjust(original));
    }

    [Fact]
    public void ChecksMuscleOverlapAcrossWeekBoundary()
    {
        var groups = new HashSet<string> { "Legs" };
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Sunday, WorkoutActivityType.GeneralStrength, true, groups),
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.GeneralStrength, true, groups)
        };

        Assert.Contains(WorkoutScheduleValidationRule.ConsecutiveStrengthMuscleGroups,
            WorkoutScheduleValidationRule.Validate(sessions));
    }

    [Fact]
    public void AllowsDifferentRecoveryActivitiesOnSameDay()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.Yoga, false),
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.Mobility, false)
        };

        Assert.Empty(WorkoutScheduleValidationRule.Validate(sessions));
    }
    [Fact]
    public void MissingStrengthMusclesAndHiitConflictFailClosed()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.GeneralStrength, true),
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.Hiit, false)
        };
        var reasons = WorkoutScheduleValidationRule.Validate(sessions);
        Assert.Contains(WorkoutScheduleValidationRule.StrengthMuscleMetadataMissing, reasons);
        Assert.Contains(WorkoutScheduleValidationRule.HiitConflict, reasons);
    }

    [Fact]
    public void SharedSecondaryMuscleIsNotMissedAcrossConsecutiveDays()
    {
        var sessions = new[]
        {
            new WorkoutScheduleValidationSession(DayOfWeek.Monday, WorkoutActivityType.GeneralStrength, true,
                new HashSet<string> { "Chest", "Triceps" }),
            new WorkoutScheduleValidationSession(DayOfWeek.Tuesday, WorkoutActivityType.GeneralStrength, true,
                new HashSet<string> { "Shoulders", "Triceps" })
        };
        Assert.Contains(WorkoutScheduleValidationRule.ConsecutiveStrengthMuscleGroups,
            WorkoutScheduleValidationRule.Validate(sessions));
    }

    [Fact]
    public void BlankMuscleGroupIsMissingMetadata()
    {
        var session = new WorkoutScheduleValidationSession(DayOfWeek.Monday,
            WorkoutActivityType.GeneralStrength, true, new HashSet<string> { " " });
        Assert.Contains(WorkoutScheduleValidationRule.StrengthMuscleMetadataMissing,
            WorkoutScheduleValidationRule.Validate([session]));
    }

}
