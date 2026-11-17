using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.V2;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests.V2;

public sealed class MuscleAccountingAndVolumeV2Tests
{
    [Theory]
    [InlineData(MuscleGroupV2.GluteusMaximus, TrainingExperienceLevel.Beginner, TrainingFocus.Balanced, 6, 8)]
    [InlineData(MuscleGroupV2.GluteusMaximus, TrainingExperienceLevel.Intermediate, TrainingFocus.GluteSpecialization, 8, 12)]
    [InlineData(MuscleGroupV2.GluteusMaximus, TrainingExperienceLevel.Advanced, TrainingFocus.GluteSpecialization, 10, 16)]
    [InlineData(MuscleGroupV2.Quadriceps, TrainingExperienceLevel.Beginner, TrainingFocus.Balanced, 4, 6)]
    [InlineData(MuscleGroupV2.Hamstrings, TrainingExperienceLevel.Intermediate, TrainingFocus.Balanced, 6, 10)]
    [InlineData(MuscleGroupV2.Back, TrainingExperienceLevel.Advanced, TrainingFocus.Balanced, 10, 14)]
    [InlineData(MuscleGroupV2.Chest, TrainingExperienceLevel.Intermediate, TrainingFocus.Balanced, 6, 8)]
    [InlineData(MuscleGroupV2.RearDelts, TrainingExperienceLevel.Beginner, TrainingFocus.Balanced, 2, 2)]
    [InlineData(MuscleGroupV2.Calves, TrainingExperienceLevel.Advanced, TrainingFocus.Balanced, 2, 6)]
    [InlineData(MuscleGroupV2.HipAbductors, TrainingExperienceLevel.Beginner, TrainingFocus.GluteSpecialization, 2, 4)]
    [InlineData(MuscleGroupV2.HipAbductors, TrainingExperienceLevel.Beginner, TrainingFocus.Balanced, 0, 0)]
    public void Gate58_weekly_volume_targets_match_approved_table(
        MuscleGroupV2 muscle,
        TrainingExperienceLevel tier,
        TrainingFocus focus,
        int expectedMin,
        int expectedMax)
    {
        var range = WorkoutPolicyV2.GetWeeklyVolumeTarget(muscle, tier, focus);
        Assert.Equal(expectedMin, range.MinimumSets);
        Assert.Equal(expectedMax, range.MaximumSets);
    }

    [Fact]
    public void Meaningful_exposure_requires_two_direct_sets_on_distinct_dates_and_separates_indirect_work()
    {
        var hipThrust = new ExerciseCatalogItemV2(
            1, "Hip Thrust Machine", MovementPattern.HipThrust,
            [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.GluteusMaximus), ExerciseMuscleContributionV2.Indirect(MuscleGroupV2.Hamstrings)],
            new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Machine },
            new HashSet<TrainingExperienceLevel> { TrainingExperienceLevel.Beginner },
            InjuryFlag.None, IsCompound: true, IsUnilateral: false, IsBodyweightOnly: false,
            SetupMinutes: 1.5m, SecondsPerRep: 3, MinimumLoadIncrementKg: 2.5m, IsGluteDominant: true);

        var row = new ExerciseCatalogItemV2(
            2, "Seated Cable Row", MovementPattern.HorizontalPull,
            [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Back), ExerciseMuscleContributionV2.Indirect(MuscleGroupV2.RearDelts), ExerciseMuscleContributionV2.Indirect(MuscleGroupV2.Biceps)],
            new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Cable },
            new HashSet<TrainingExperienceLevel> { TrainingExperienceLevel.Beginner },
            InjuryFlag.None, IsCompound: true, IsUnilateral: false, IsBodyweightOnly: false,
            SetupMinutes: 1.0m, SecondsPerRep: 3, MinimumLoadIncrementKg: 2.5m);

        var catalogById = new Dictionary<int, ExerciseCatalogItemV2> { [1] = hipThrust, [2] = row };

        var monday = new DateOnly(2026, 11, 16);
        var sessions = new[]
        {
            new PlannedSessionV2(0, DayOfWeek.Monday, monday, WorkoutActivityType.GeneralStrength, SplitKindV2.FullBody, 6, 30, 4,
            [
                new PlannedExercisePrescriptionV2(0, 1, hipThrust.Name, hipThrust.Pattern, true, WarmUpSets: 1, WorkingSets: 2, 8, 12, 3, 120, SessionLoadStatusV2.CalibrationRequired, null, 8, null, "Test"),
                new PlannedExercisePrescriptionV2(1, 2, row.Name, row.Pattern, true, WarmUpSets: 1, WorkingSets: 2, 8, 12, 3, 120, SessionLoadStatusV2.CalibrationRequired, null, 8, null, "Test")
            ]),
            // Second session on the SAME Monday date should count as 1 meaningful day, not 2
            new PlannedSessionV2(1, DayOfWeek.Monday, monday, WorkoutActivityType.GeneralStrength, SplitKindV2.FullBody, 6, 20, 2,
            [
                new PlannedExercisePrescriptionV2(0, 1, hipThrust.Name, hipThrust.Pattern, true, WarmUpSets: 0, WorkingSets: 2, 8, 12, 3, 120, SessionLoadStatusV2.CalibrationRequired, null, 8, null, "Test")
            ])
        };

        var summary = MuscleAccountingRuleV2.Evaluate(sessions, catalogById, TrainingExperienceLevel.Beginner, TrainingFocus.GluteSpecialization);

        Assert.Equal(6, summary.TotalPhysicalWorkingSets); // warm-up sets excluded
        var glutes = summary.Muscles.Single(m => m.Muscle == MuscleGroupV2.GluteusMaximus);
        Assert.Equal(4m, glutes.DirectSets);
        Assert.Equal(1, glutes.MeaningfulDirectDays); // same-day sessions = 1 date

        var rearDelts = summary.Muscles.Single(m => m.Muscle == MuscleGroupV2.RearDelts);
        Assert.Equal(0m, rearDelts.DirectSets);
        Assert.Equal(1.0m, rearDelts.IndirectSets);
        Assert.False(summary.CoversDirectRearDelts); // rows do not satisfy direct rear-delt requirement
    }
}
