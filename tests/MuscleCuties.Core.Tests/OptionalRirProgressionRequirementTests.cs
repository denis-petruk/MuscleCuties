using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class OptionalRirProgressionRequirementTests
{
    private static SnapshotProgressionExposure Exposure(int day, int target, params SnapshotProgressionSet[] sets) =>
        new(new DateTime(2026, 11, day, 0, 0, 0, DateTimeKind.Utc), 2, target, 2, sets);
    private static SnapshotProgressionSet Set(int number, int reps, int? rir = null, bool technique = true, decimal load = 60m) =>
        new(number, reps, load, rir, technique);
    private static StrengthProgressionAdvice Resolve(params SnapshotProgressionExposure[] exposures) =>
        SnapshotProgressionRule.Resolve(MajorMuscleGroup.Quads, EffectiveExperienceTier.Tier2IntermediateOrEstablished, 101, 8, 60m, exposures);

    [Fact]
    public void Two_complete_exposures_can_progress_without_rir_using_each_prescribed_target()
    {
        var result = Resolve(Exposure(1, 8, Set(1, 8), Set(2, 8)), Exposure(2, 10, Set(1, 10), Set(2, 10)));
        Assert.True(result.LoadIncreased);
        Assert.Equal(62.5m, result.PrescribedLoad);
    }

    [Fact]
    public void A_partial_latest_exposure_cannot_be_skipped_to_earn_an_increase()
    {
        var result = Resolve(Exposure(1, 8, Set(1, 8), Set(2, 8)), Exposure(2, 8, Set(1, 8), Set(2, 8)), Exposure(3, 8, Set(1, 8)));
        Assert.False(result.LoadIncreased);
    }

    [Fact]
    public void Two_complete_under_target_exposures_with_recorded_rir_preserve_existing_reduction()
    {
        var result = Resolve(Exposure(1, 8, Set(1, 4, 2), Set(2, 4, 2)), Exposure(2, 8, Set(1, 4, 2), Set(2, 4, 2)));
        Assert.Equal(StrengthProgressionAdviceRule.ReduceCode, result.RecommendationCode);
        Assert.Equal(57.5m, result.PrescribedLoad);
    }

    [Fact]
    public void Under_target_duplicate_set_bad_technique_and_different_load_never_earn_progression()
    {
        var good = Exposure(1, 8, Set(1, 8), Set(2, 8));
        Assert.False(Resolve(good, Exposure(2, 10, Set(1, 10), Set(2, 9))).LoadIncreased);
        Assert.False(Resolve(good, Exposure(2, 8, Set(1, 8), Set(1, 8))).LoadIncreased);
        Assert.False(Resolve(good, Exposure(2, 8, Set(1, 8), Set(2, 8, technique: false))).LoadIncreased);
        Assert.False(Resolve(good, Exposure(2, 8, Set(1, 8), Set(2, 8, load: 55m))).LoadIncreased);
    }
}
