using MuscleCuties.Core.Domain.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutOverviewRuleTests
{
    private const string SupportedTimeZoneId = "America/Toronto";
    private static readonly DateOnly SupportedDate = DateOnly.FromDateTime(WorkoutScheduleMathRule.MaxSupportedDate);

    [Fact]
    public void Easier_Alternative_Must_Be_Optional_And_Have_A_Valid_Workout_Id()
    {
        Assert.True(WorkoutOverviewRule.IsValidSessionOptionality(
            WorkoutSessionActionRule.MinimumPositiveIdentifier, true, true));
        Assert.False(WorkoutOverviewRule.IsValidSessionOptionality(
            WorkoutSessionActionRule.MinimumPositiveIdentifier, false, true));
        Assert.False(WorkoutOverviewRule.IsValidSessionOptionality(
            WorkoutSessionActionRule.MinimumNonNegativeCount, true, false));
    }

    [Fact]
    public void Readiness_Adjustment_Requires_A_Supported_Date_And_Time_Zone()
    {
        Assert.True(WorkoutOverviewRule.IsValidReadinessAdjustmentRequest(SupportedDate, SupportedTimeZoneId));
        Assert.False(WorkoutOverviewRule.IsValidReadinessAdjustmentRequest(SupportedDate, " "));
        Assert.False(WorkoutOverviewRule.IsValidReadinessAdjustmentRequest(DateOnly.MinValue, SupportedTimeZoneId));
        Assert.True(WorkoutOverviewRule.IsValidReadinessAdjustmentDecision(
            Guid.NewGuid(), SupportedDate, SupportedTimeZoneId));
        Assert.False(WorkoutOverviewRule.IsValidReadinessAdjustmentDecision(
            Guid.Empty, SupportedDate, SupportedTimeZoneId));
    }
}
