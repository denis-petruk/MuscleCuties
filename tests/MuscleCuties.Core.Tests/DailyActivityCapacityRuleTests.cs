using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class DailyActivityCapacityRuleTests
{
    private const decimal DailyAvailableMinutes = 90m;
    private const decimal StrengthMinutes = 40m;
    private const decimal CardioMinutes = 20m;
    private const decimal MobilityMinutes = 10m;
    private const decimal ExpectedCombinedMinutes = 70m;
    private const decimal ClimbingMinutes = 45m;
    private const decimal PullingMinutes = 30m;
    private const decimal BeyondCapacityMinutes = 95m;
    private static readonly DateOnly Day = new(2026, 10, 23);
    private static readonly ActivityWindow FullDayWindow = new(new TimeOnly(8, 0), new TimeOnly(18, 0));

    [Fact]
    public void MultiSession_Day_Combines_Distinct_Activity_Time_Once()
    {
        var activities = new[]
        {
            Activity("strength", StrengthMinutes, ActivityDemandTag.UpperBody),
            Activity("cardio", CardioMinutes, ActivityDemandTag.Conditioning),
            Activity("mobility", MobilityMinutes, ActivityDemandTag.Mobility)
        };

        var result = DailyActivityCapacityRule.Evaluate(Availability(), activities);

        Assert.Equal(DailyCapacityStatus.Feasible, result.Status);
        Assert.Equal(ExpectedCombinedMinutes, result.PlannedMinutes);
        Assert.Equal(activities.Length, result.DistinctActivities.Count);
    }

    [Fact]
    public void Imported_And_Manual_Copy_With_Same_Canonical_Id_Count_Once()
    {
        var imported = Activity("climbing-session", ClimbingMinutes, ActivityDemandTag.Grip, outside: true);

        var result = DailyActivityCapacityRule.Evaluate(Availability(), [imported, imported with { }]);

        Assert.Equal(DailyCapacityStatus.Feasible, result.Status);
        Assert.Equal(ClimbingMinutes, result.PlannedMinutes);
        Assert.Single(result.DistinctActivities);
    }

    [Fact]
    public void Climbing_And_Pulling_Request_Overlap_Review_Without_Fatigue_Percentage()
    {
        var result = DailyActivityCapacityRule.Evaluate(Availability(),
        [
            Activity("climb", ClimbingMinutes, ActivityDemandTag.Grip, outside: true),
            Activity("pull", PullingMinutes, ActivityDemandTag.Grip)
        ]);

        Assert.Equal(DailyCapacityStatus.NeedsReview, result.Status);
        Assert.Contains(DailyCapacityReason.OutsideActivityDemandOverlap, result.Reasons);
        Assert.Equal(ClimbingMinutes + PullingMinutes, result.PlannedMinutes);
    }

    [Fact]
    public void Demanding_Recovery_Label_Does_Not_Erase_Demand_Metadata()
    {
        var recoveryLabeled = Activity("recovery", ClimbingMinutes, ActivityDemandTag.Grip, outside: true);
        var pulling = Activity("pull", PullingMinutes, ActivityDemandTag.Grip);

        var result = DailyActivityCapacityRule.Evaluate(Availability(), [recoveryLabeled, pulling]);

        Assert.Contains(DailyCapacityReason.OutsideActivityDemandOverlap, result.Reasons);
    }

    [Fact]
    public void Insufficient_Time_And_Simultaneous_Sessions_Are_Not_Feasible()
    {
        var tooLong = DailyActivityCapacityRule.Evaluate(Availability(),
            [Activity("long", BeyondCapacityMinutes, ActivityDemandTag.UpperBody)]);
        var simultaneous = DailyActivityCapacityRule.Evaluate(Availability(),
        [
            Activity("first", StrengthMinutes, ActivityDemandTag.UpperBody,
                start: new TimeOnly(9, 0), end: new TimeOnly(10, 0)),
            Activity("second", CardioMinutes, ActivityDemandTag.Conditioning,
                start: new TimeOnly(9, 30), end: new TimeOnly(10, 30))
        ]);

        Assert.Equal(DailyCapacityStatus.Limited, tooLong.Status);
        Assert.Contains(DailyCapacityReason.ExceedsAvailableMinutes, tooLong.Reasons);
        Assert.Equal(DailyCapacityStatus.Blocked, simultaneous.Status);
        Assert.Contains(DailyCapacityReason.SimultaneousActivities, simultaneous.Reasons);
    }

    [Fact]
    public void Missing_Timing_And_Conflicting_Duplicate_Are_Explicit()
    {
        var missing = DailyActivityCapacityRule.Evaluate(Availability(),
            [Activity("unknown", null, ActivityDemandTag.Mobility)]);
        var conflicting = DailyActivityCapacityRule.Evaluate(Availability(),
        [
            Activity("same", CardioMinutes, ActivityDemandTag.Conditioning),
            Activity("same", StrengthMinutes, ActivityDemandTag.Conditioning)
        ]);

        Assert.Equal(DailyCapacityStatus.NeedsReview, missing.Status);
        Assert.Null(missing.PlannedMinutes);
        Assert.Contains(DailyCapacityReason.MissingDuration, missing.Reasons);
        Assert.Equal(DailyCapacityStatus.NeedsReview, conflicting.Status);
        Assert.Null(conflicting.PlannedMinutes);
        Assert.Contains(DailyCapacityReason.ConflictingDuplicate, conflicting.Reasons);
    }

    private static DailyActivityAvailability Availability() => new(Day, DailyAvailableMinutes, [FullDayWindow]);

    private static PlannedDailyActivity Activity(string id, decimal? minutes, ActivityDemandTag tag,
        bool outside = false, TimeOnly? start = null, TimeOnly? end = null) =>
        new(id, Day, minutes, new HashSet<ActivityDemandTag> { tag }, true, outside,
            false, false, start, end);
}
