using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WeeklyHypertrophyVolumeRuleTests
{
    [Fact]
    public void GetBounds_ReturnsExpectedTierRanges()
    {
        var (t1Min, t1Max) = WeeklyHypertrophyVolumeRule.GetBounds(EffectiveExperienceTier.Tier1BeginnerOrNew);
        var (t2Min, t2Max) = WeeklyHypertrophyVolumeRule.GetBounds(EffectiveExperienceTier.Tier2IntermediateOrEstablished);
        var (t3Min, t3Max) = WeeklyHypertrophyVolumeRule.GetBounds(EffectiveExperienceTier.Tier3Advanced);

        Assert.Equal(8, t1Min);
        Assert.Equal(12, t1Max);

        Assert.Equal(8, t2Min);
        Assert.Equal(16, t2Max);

        Assert.Equal(12, t3Min);
        Assert.Equal(22, t3Max);
    }

    [Fact]
    public void CalculateVolume_SumsDirectSetsCorrectly()
    {
        var exercises = new[]
        {
            new PlannedExerciseSets(101, MajorMuscleGroup.Chest, 3),
            new PlannedExerciseSets(102, MajorMuscleGroup.Chest, 3),
            new PlannedExerciseSets(103, MajorMuscleGroup.Back, 4),
            new PlannedExerciseSets(104, MajorMuscleGroup.Quads, 5),
            new PlannedExerciseSets(105, MajorMuscleGroup.Quads, -1) // negative ignored
        };

        var volume = WeeklyHypertrophyVolumeRule.CalculateVolume(exercises);

        Assert.Equal(6, volume[MajorMuscleGroup.Chest]);
        Assert.Equal(4, volume[MajorMuscleGroup.Back]);
        Assert.Equal(5, volume[MajorMuscleGroup.Quads]);
        Assert.Equal(0, volume[MajorMuscleGroup.Glutes]);
    }

    [Fact]
    public void Evaluate_Tier1_DetectsVolumeWithinBoundsAndExceeded()
    {
        var exercises = new[]
        {
            new PlannedExerciseSets(101, MajorMuscleGroup.Chest, 8), // within 8..12
            new PlannedExerciseSets(102, MajorMuscleGroup.Back, 2),  // below 8
            new PlannedExerciseSets(103, MajorMuscleGroup.Quads, 13) // above 12
        };

        var targeted = new HashSet<MajorMuscleGroup>
        {
            MajorMuscleGroup.Chest,
            MajorMuscleGroup.Back,
            MajorMuscleGroup.Quads
        };

        var result = WeeklyHypertrophyVolumeRule.Evaluate(
            EffectiveExperienceTier.Tier1BeginnerOrNew, exercises, targeted);

        Assert.False(result.AllTargetedMusclesWithinBounds);

        var chest = Assert.Single(result.MuscleStatuses, s => s.Muscle == MajorMuscleGroup.Chest);
        Assert.True(chest.IsWithinBounds);
        Assert.False(chest.IsBelowMinimum);
        Assert.False(chest.IsAboveMaximum);

        var back = Assert.Single(result.MuscleStatuses, s => s.Muscle == MajorMuscleGroup.Back);
        Assert.False(back.IsWithinBounds);
        Assert.True(back.IsBelowMinimum);

        var quads = Assert.Single(result.MuscleStatuses, s => s.Muscle == MajorMuscleGroup.Quads);
        Assert.False(quads.IsWithinBounds);
        Assert.True(quads.IsAboveMaximum);
    }
}
