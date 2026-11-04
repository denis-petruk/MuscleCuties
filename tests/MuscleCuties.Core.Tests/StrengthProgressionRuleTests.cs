using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class StrengthProgressionRuleTests
{
    private const decimal StartingLoad = 50.0m;
    private const decimal Increment = 2.5m;

    [Fact]
    public void EffortPrescription_CompoundAndAccessoryDefaults()
    {
        var (cMin, cMax, cRir) = StrengthProgressionRule.GetEffortPrescription(ExerciseMovementCategory.Compound);
        var (aMin, aMax, aRir) = StrengthProgressionRule.GetEffortPrescription(ExerciseMovementCategory.Accessory);

        Assert.Equal(6, cMin);
        Assert.Equal(12, cMax);
        Assert.Equal(2, cRir);

        Assert.Equal(10, aMin);
        Assert.Equal(20, aMax);
        Assert.Equal(2, aRir);
    }

    [Fact]
    public void SingleSetLoad_TopRepAndTargetRir_IncreasesLoad()
    {
        var result = StrengthProgressionRule.ResolveSingleSetLoad(
            repsPrevious: 12,
            loadPrevious: StartingLoad,
            rirPrevious: 2,
            loadIncrement: Increment,
            repLow: 6,
            repHigh: 12,
            targetRir: 2);

        Assert.Equal(StartingLoad + Increment, result.PrescribedLoad);
        Assert.Equal(StrengthProgressionRule.ActionIncrease, result.ProgressionAction);
        Assert.Equal(StrengthProgressionRule.DoubleProgressionEarned, result.ReasonCode);
    }

    [Fact]
    public void SingleSetLoad_WithinRangeAndTargetRir_MaintainsLoad()
    {
        var result = StrengthProgressionRule.ResolveSingleSetLoad(
            repsPrevious: 10,
            loadPrevious: StartingLoad,
            rirPrevious: 2,
            loadIncrement: Increment,
            repLow: 6,
            repHigh: 12,
            targetRir: 2);

        Assert.Equal(StartingLoad, result.PrescribedLoad);
        Assert.Equal(StrengthProgressionRule.ActionMaintain, result.ProgressionAction);
    }

    [Fact]
    public void SingleSetLoad_BelowMinimumReps_DecreasesLoad()
    {
        var result = StrengthProgressionRule.ResolveSingleSetLoad(
            repsPrevious: 4,
            loadPrevious: StartingLoad,
            rirPrevious: 2,
            loadIncrement: Increment,
            repLow: 6,
            repHigh: 12,
            targetRir: 2);

        Assert.Equal(StartingLoad - Increment, result.PrescribedLoad);
        Assert.Equal(StrengthProgressionRule.ActionDecrease, result.ProgressionAction);
    }

    [Fact]
    public void SingleSetLoad_UnacceptableTechnique_BlocksIncrease()
    {
        var result = StrengthProgressionRule.ResolveSingleSetLoad(
            repsPrevious: 12,
            loadPrevious: StartingLoad,
            rirPrevious: 2,
            loadIncrement: Increment,
            repLow: 6,
            repHigh: 12,
            targetRir: 2,
            techniqueAcceptable: false);

        Assert.Equal(StartingLoad, result.PrescribedLoad);
        Assert.Equal(StrengthProgressionRule.ActionMaintain, result.ProgressionAction);
        Assert.Equal(StrengthProgressionRule.UnacceptableTechniqueBlocksProgression, result.ReasonCode);
    }

    [Fact]
    public void DoubleProgression_TwoConsecutiveWeeksTopReps_ProgressesLoad()
    {
        var week1 = new DoubleProgressionWeekPerformance(1,
        [
            new CompletedWorkingSet(12, StartingLoad, 2),
            new CompletedWorkingSet(12, StartingLoad, 2)
        ]);
        var week2 = new DoubleProgressionWeekPerformance(2,
        [
            new CompletedWorkingSet(12, StartingLoad, 2),
            new CompletedWorkingSet(12, StartingLoad, 2)
        ]);

        var candidate = new MuscleProgressionCandidate(
            MajorMuscleGroup.Chest,
            ExerciseId: 101,
            CurrentLoad: StartingLoad,
            LoadIncrement: Increment,
            ExerciseMovementCategory.Compound,
            RecentWeeks: [week1, week2],
            CurrentWeeklyVolume: 8,
            PreviousWeeklyVolume: 8);

        var decisions = StrengthProgressionRule.ResolveDoubleProgression(
            EffectiveExperienceTier.Tier1BeginnerOrNew, [candidate]);

        var decision = Assert.Single(decisions);
        Assert.True(decision.LoadIncreased);
        Assert.Equal(StartingLoad + Increment, decision.PrescribedLoad);
        Assert.Equal(StrengthProgressionRule.StatusProgressed, decision.ProgressionStatus);
    }

    [Fact]
    public void DoubleProgression_SimultaneousVolumeIncrease_BlocksLoadIncrease()
    {
        var week1 = new DoubleProgressionWeekPerformance(1, [new CompletedWorkingSet(12, StartingLoad, 2)]);
        var week2 = new DoubleProgressionWeekPerformance(2, [new CompletedWorkingSet(12, StartingLoad, 2)]);

        var candidate = new MuscleProgressionCandidate(
            MajorMuscleGroup.Chest,
            ExerciseId: 101,
            CurrentLoad: StartingLoad,
            LoadIncrement: Increment,
            ExerciseMovementCategory.Compound,
            RecentWeeks: [week1, week2],
            CurrentWeeklyVolume: 10, // volume increased from 8 to 10
            PreviousWeeklyVolume: 8);

        var decisions = StrengthProgressionRule.ResolveDoubleProgression(
            EffectiveExperienceTier.Tier1BeginnerOrNew, [candidate]);

        var decision = Assert.Single(decisions);
        Assert.False(decision.LoadIncreased);
        Assert.Equal(StartingLoad, decision.PrescribedLoad);
        Assert.Equal(StrengthProgressionRule.StatusBlockedByVolume, decision.ProgressionStatus);
        Assert.Equal(StrengthProgressionRule.SimultaneousVolumeIncreaseBlocksLoadProgression, decision.ReasonCode);
    }

    [Fact]
    public void DoubleProgression_Tier1_CapsConcurrentProgressionsToOneMuscle()
    {
        var week1 = new DoubleProgressionWeekPerformance(1, [new CompletedWorkingSet(12, StartingLoad, 2)]);
        var week2 = new DoubleProgressionWeekPerformance(2, [new CompletedWorkingSet(12, StartingLoad, 2)]);

        var candidate1 = new MuscleProgressionCandidate(
            MajorMuscleGroup.Chest, 101, StartingLoad, Increment, ExerciseMovementCategory.Compound,
            [week1, week2], 8, 8);
        var candidate2 = new MuscleProgressionCandidate(
            MajorMuscleGroup.Back, 102, StartingLoad, Increment, ExerciseMovementCategory.Compound,
            [week1, week2], 8, 8);

        var decisions = StrengthProgressionRule.ResolveDoubleProgression(
            EffectiveExperienceTier.Tier1BeginnerOrNew, [candidate1, candidate2]);

        Assert.Equal(2, decisions.Count);
        Assert.True(decisions[0].LoadIncreased);
        Assert.Equal(StrengthProgressionRule.StatusProgressed, decisions[0].ProgressionStatus);

        Assert.False(decisions[1].LoadIncreased);
        Assert.Equal(StrengthProgressionRule.StatusBlockedByTierCap, decisions[1].ProgressionStatus);
        Assert.Equal(StrengthProgressionRule.ConcurrentProgressionCapExceeded, decisions[1].ReasonCode);
    }
}
