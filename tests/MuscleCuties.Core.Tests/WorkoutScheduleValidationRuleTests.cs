using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutScheduleValidationRuleTests
{
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

}
