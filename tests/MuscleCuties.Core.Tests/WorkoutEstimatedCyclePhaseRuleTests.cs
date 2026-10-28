using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutEstimatedCyclePhaseRuleTests
{
    private const int RegularCycleDays = 28;
    private const int DifferentCycleDays = 29;
    private const int LongerCycleDays = 30;
    private const int InvalidCycleDays = 70;
    private const int RecentObservationDay = 2;
    private const int FollicularDayOffset = 7;
    private const int PhaseDisagreementDayOffset = 12;
    private const int OvulatoryDayOffset = 13;
    private const int LutealDayOffset = 19;
    private static readonly DateOnly LatestStart = new(2026, 10, 1);

    [Theory]
    [InlineData(0, CyclePhase.Menstrual)]
    [InlineData(FollicularDayOffset, CyclePhase.Follicular)]
    [InlineData(OvulatoryDayOffset, CyclePhase.Ovulatory)]
    [InlineData(LutealDayOffset, CyclePhase.Luteal)]
    public void Consistent_recorded_cycles_support_each_phase(int targetOffset, CyclePhase expected)
    {
        var starts = Starts(RegularCycleDays, RegularCycleDays, RegularCycleDays);
        Assert.Equal(expected, WorkoutEstimatedCyclePhaseRule.Estimate(
            LatestStart.AddDays(targetOffset), LatestStart.AddDays(Math.Min(targetOffset, RecentObservationDay)), starts));
    }

    [Fact]
    public void Variable_cycles_are_used_only_where_all_lengths_agree()
    {
        var starts = Starts(LongerCycleDays, DifferentCycleDays, RegularCycleDays);
        Assert.Equal(CyclePhase.Follicular, WorkoutEstimatedCyclePhaseRule.Estimate(
            LatestStart.AddDays(FollicularDayOffset), LatestStart.AddDays(RecentObservationDay), starts));
        Assert.Equal(CyclePhase.Unknown, WorkoutEstimatedCyclePhaseRule.Estimate(
            LatestStart.AddDays(PhaseDisagreementDayOffset), LatestStart.AddDays(RecentObservationDay), starts));
    }

    [Fact]
    public void Missing_invalid_or_expired_intervals_do_not_establish_a_phase()
    {
        var regular = Starts(RegularCycleDays, RegularCycleDays, RegularCycleDays);
        Assert.Equal(CyclePhase.Unknown, WorkoutEstimatedCyclePhaseRule.Estimate(
            LatestStart, LatestStart, regular.Skip(1).ToArray()));
        Assert.Equal(CyclePhase.Unknown, WorkoutEstimatedCyclePhaseRule.Estimate(
            LatestStart, LatestStart, Starts(InvalidCycleDays, RegularCycleDays, RegularCycleDays)));
        Assert.Equal(CyclePhase.Unknown, WorkoutEstimatedCyclePhaseRule.Estimate(
            LatestStart.AddDays(RegularCycleDays), LatestStart.AddDays(RecentObservationDay), regular));
        Assert.Equal(CyclePhase.Unknown, WorkoutEstimatedCyclePhaseRule.Estimate(
            LatestStart.AddDays(-1), LatestStart, regular));
    }

    private static DateOnly[] Starts(int oldestInterval, int middleInterval, int latestInterval)
    {
        var third = LatestStart.AddDays(-latestInterval);
        var second = third.AddDays(-middleInterval);
        return [second.AddDays(-oldestInterval), second, third, LatestStart];
    }
}
