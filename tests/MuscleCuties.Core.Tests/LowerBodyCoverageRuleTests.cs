using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class LowerBodyCoverageRuleTests
{
    [Fact]
    public void CountsSessionExposuresForRequiredLowerBodyMuscles()
    {
        var result = LowerBodyCoverageRule.Evaluate([
            new[] { "Glutes", "Hamstrings", "Quads" },
            new[] { "GluteMed", "GluteMax" },
            new[] { "Adductors" }]);

        Assert.Equal(2, result.SessionExposures[LowerBodyMuscleGroup.Glutes]);
        Assert.Equal(1, result.SessionExposures[LowerBodyMuscleGroup.Hamstrings]);
        Assert.Equal(1, result.SessionExposures[LowerBodyMuscleGroup.Abductors]);
        Assert.Equal(1, result.SessionExposures[LowerBodyMuscleGroup.Adductors]);
        // Gates 55–56 supersede unconditional adductor and abductor coverage expectations.
        Assert.Equal(new[] { LowerBodyMuscleGroup.Hamstrings }, result.BelowMinimum);
        Assert.False(result.MeetsMinimum);
    }

    [Fact]
    public void MetMinimumWhenEveryRequiredMuscleAppearsTwice()
    {
        var result = LowerBodyCoverageRule.Evaluate([
            new[] { "Glutes", "Hamstrings", "GluteMed", "Adductors" },
            new[] { "GluteMax", "Hamstring", "HipAbductors", "HipAdductors" }]);

        Assert.All(LowerBodyCoverageRule.MuscleGroups, muscle =>
            Assert.Equal(LowerBodyCoverageRule.MinimumWeeklySessionExposures, result.SessionExposures[muscle]));
        Assert.True(result.MeetsMinimum);
        Assert.Empty(result.BelowMinimum);
    }

    [Fact]
    public void Adductor_exposures_are_tracked_but_absence_is_not_an_unconditional_gap()
    {
        var result = LowerBodyCoverageRule.Evaluate([
            new[] { "Glutes", "Hamstrings", "GluteMed" },
            new[] { "Glutes", "Hamstrings", "GluteMed" }]);
        Assert.Equal(0, result.SessionExposures[LowerBodyMuscleGroup.Adductors]);
        Assert.DoesNotContain(LowerBodyMuscleGroup.Adductors, result.BelowMinimum);
        Assert.True(result.MeetsMinimum);
    }

    [Fact]
    public void A_caller_supplied_adductor_gap_does_not_unconditionally_promote_adduction()
    {
        var entry = new StrengthExerciseCatalogEntry("Synthetic adduction",
            new ReviewedExerciseCandidate(101, "synthetic", MovementPattern.HipAdduction,
                new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
                new HashSet<ActivityExperience> { ActivityExperience.Beginner },
                new HashSet<ActivityDemandTag>(), true, true, new HashSet<ActivityDemandTag>(),
                new HashSet<ExerciseTimingMethod> { ExerciseTimingMethod.StraightSets },
                1, new HashSet<int>(), InjuryFlag.None), []);
        var groups = new Dictionary<int, IReadOnlySet<string>> { [101] = new HashSet<string> { "Adductors" } };
        Assert.Empty(LowerBodyCoverageRule.PriorityExerciseIds([LowerBodyMuscleGroup.Adductors], groups, [entry]));
    }

    [Fact]
    public void A_caller_supplied_abductor_gap_does_not_unconditionally_promote_abduction()
    {
        var entry = new StrengthExerciseCatalogEntry("Synthetic abduction",
            new ReviewedExerciseCandidate(102, "synthetic", MovementPattern.HipAbduction,
                new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
                new HashSet<ActivityExperience> { ActivityExperience.Beginner },
                new HashSet<ActivityDemandTag>(), true, true, new HashSet<ActivityDemandTag>(),
                new HashSet<ExerciseTimingMethod> { ExerciseTimingMethod.StraightSets },
                1, new HashSet<int>(), InjuryFlag.None), []);
        var groups = new Dictionary<int, IReadOnlySet<string>> { [102] = new HashSet<string> { "HipAbductors" } };
        Assert.Empty(LowerBodyCoverageRule.PriorityExerciseIds([LowerBodyMuscleGroup.Abductors], groups, [entry]));
    }

    [Fact]
    public void Abductor_exposure_is_tracked_without_an_unconditional_frequency_requirement()
    {
        var result = LowerBodyCoverageRule.Evaluate([
            new[] { "Glutes", "Hamstrings" }, new[] { "Glutes", "Hamstrings" }]);
        Assert.Equal(0, result.SessionExposures[LowerBodyMuscleGroup.Abductors]);
        Assert.DoesNotContain(LowerBodyMuscleGroup.Abductors, result.BelowMinimum);
        Assert.True(result.MeetsMinimum);
    }

    [Fact]
    public void ReportsOneGapCodePerUnderCoveredMuscle()
    {
        Assert.Equal("LowerBodyCoverageBelowMinimum_Adductors",
            LowerBodyCoverageRule.GapCode(LowerBodyMuscleGroup.Adductors));
    }
}
