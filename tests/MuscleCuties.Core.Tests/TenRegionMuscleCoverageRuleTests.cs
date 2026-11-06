using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class TenRegionMuscleCoverageRuleTests
{
    [Fact]
    public void ExactlyTenPrimaryRegionsAreDefined()
    {
        Assert.Equal(10, TenRegionMuscleCoverageRule.Regions.Count);
        Assert.Contains(PrimaryMuscleRegion.Chest, TenRegionMuscleCoverageRule.Regions);
        Assert.Contains(PrimaryMuscleRegion.LatsUpperBack, TenRegionMuscleCoverageRule.Regions);
        Assert.Contains(PrimaryMuscleRegion.Deltoids, TenRegionMuscleCoverageRule.Regions);
        Assert.Contains(PrimaryMuscleRegion.Quadriceps, TenRegionMuscleCoverageRule.Regions);
        Assert.Contains(PrimaryMuscleRegion.Hamstrings, TenRegionMuscleCoverageRule.Regions);
        Assert.Contains(PrimaryMuscleRegion.Glutes, TenRegionMuscleCoverageRule.Regions);
        Assert.Contains(PrimaryMuscleRegion.Biceps, TenRegionMuscleCoverageRule.Regions);
        Assert.Contains(PrimaryMuscleRegion.Triceps, TenRegionMuscleCoverageRule.Regions);
        Assert.Contains(PrimaryMuscleRegion.Calves, TenRegionMuscleCoverageRule.Regions);
        Assert.Contains(PrimaryMuscleRegion.Trunk, TenRegionMuscleCoverageRule.Regions);
    }

    [Fact]
    public void FullBodyTwoDayPlan_MeetsMinimumTwoExposuresForEveryRegion()
    {
        // Session A: Squat, HipHinge, HorizontalPush, HorizontalPull, Trunk, CalfRaise
        // Session B: Lunge, HipThrust, VerticalPush, VerticalPull, Trunk, CalfRaise
        var sessionA = new[] { "SquatPattern", "HipHinge", "HorizontalPush", "HorizontalPull", "Trunk", "CalfRaise" };
        var sessionB = new[] { "Lunge", "HipThrust", "VerticalPush", "VerticalPull", "Trunk", "CalfRaise" };

        var result = TenRegionMuscleCoverageRule.Evaluate([sessionA, sessionB], scheduledStrengthDays: 2);

        Assert.True(result.MeetsMinimum);
        Assert.True(result.IsFeasible);
        Assert.Empty(result.BelowMinimum);
        Assert.Empty(result.LimitationCodes);
        Assert.Contains(TenRegionMuscleCoverageRule.TwoDaysCoverageLimitedAdvisory, result.AdvisoryCodes);

        foreach (var region in TenRegionMuscleCoverageRule.Regions)
        {
            Assert.True(result.WeeklyExposures[region] >= 2.0m,
                $"Region {region} exposure {result.WeeklyExposures[region]} was below minimum 2.0");
        }
    }

    [Fact]
    public void FullBodyThreeDayPlan_MeetsPreferredThreeExposuresWhenSchedulePermits()
    {
        var sessionA = new[] { "SquatPattern", "HipHinge", "HorizontalPush", "HorizontalPull", "Trunk", "CalfRaise" };
        var sessionB = new[] { "Lunge", "HipThrust", "VerticalPush", "VerticalPull", "Trunk", "CalfRaise" };
        var sessionC = new[] { "SquatPattern", "HipHinge", "HorizontalPush", "VerticalPull", "Trunk", "CalfRaise" };

        var result = TenRegionMuscleCoverageRule.Evaluate([sessionA, sessionB, sessionC], scheduledStrengthDays: 3);

        Assert.True(result.MeetsMinimum);
        Assert.True(result.MeetsPreferred);
        Assert.True(result.IsFeasible);
        Assert.Empty(result.BelowMinimum);
        Assert.Empty(result.BelowPreferred);

        foreach (var region in TenRegionMuscleCoverageRule.Regions)
        {
            Assert.True(result.WeeklyExposures[region] >= 3.0m,
                $"Region {region} exposure {result.WeeklyExposures[region]} was below preferred 3.0");
        }
    }

    [Fact]
    public void OneDaySchedule_IsExplicitlyInfeasible_ReportingDaysInsufficient()
    {
        var session = new[] { "SquatPattern", "HipHinge", "HorizontalPush", "HorizontalPull", "Trunk", "CalfRaise" };

        var result = TenRegionMuscleCoverageRule.Evaluate([session], scheduledStrengthDays: 1);

        Assert.False(result.MeetsMinimum);
        Assert.False(result.IsFeasible);
        Assert.Contains(TenRegionMuscleCoverageRule.DaysInsufficientForCoverageCode, result.LimitationCodes);
        Assert.Equal(10, result.BelowMinimum.Count); // Every region is below 2 exposures
    }

    [Fact]
    public void OmittedMovementPattern_LeavesRegionBelowMinimum_ReportingLimitation()
    {
        // 2 sessions, but both omit Push and Pull movements entirely (only lower body)
        var sessionA = new[] { "SquatPattern", "HipHinge" };
        var sessionB = new[] { "Lunge", "HipThrust" };

        var result = TenRegionMuscleCoverageRule.Evaluate([sessionA, sessionB], scheduledStrengthDays: 2);

        Assert.False(result.MeetsMinimum);
        Assert.False(result.IsFeasible);
        Assert.Contains(PrimaryMuscleRegion.Chest, result.BelowMinimum);
        Assert.Contains(PrimaryMuscleRegion.LatsUpperBack, result.BelowMinimum);
        Assert.Contains(TenRegionMuscleCoverageRule.GapCode(PrimaryMuscleRegion.Chest), result.LimitationCodes);
        Assert.Contains(TenRegionMuscleCoverageRule.GapCode(PrimaryMuscleRegion.LatsUpperBack), result.LimitationCodes);
    }
}
