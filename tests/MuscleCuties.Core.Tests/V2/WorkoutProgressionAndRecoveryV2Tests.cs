using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.V2;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests.V2;

public sealed class WorkoutProgressionAndRecoveryV2Tests
{
    private static readonly ExerciseCatalogItemV2 LegPress = new(
        2, "Leg Press", MovementPattern.SquatPattern,
        [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Quadriceps)],
        new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Machine },
        new HashSet<Models.Enums.Users.TrainingExperienceLevel> { Models.Enums.Users.TrainingExperienceLevel.Beginner },
        InjuryFlag.None, true, false, false, 1.5m, 3, 5.0m);

    private static readonly ExerciseCatalogItemV2 BulgarianSplitSquat = new(
        14, "Bulgarian Split Squat", MovementPattern.Lunge,
        [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Quadriceps)],
        new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Dumbbell },
        new HashSet<Models.Enums.Users.TrainingExperienceLevel> { Models.Enums.Users.TrainingExperienceLevel.Intermediate },
        InjuryFlag.None, true, true, false, 1.5m, 3, 2.0m);

    [Fact]
    public void Gate71_72_two_comparable_sessions_without_rir_earn_equipment_increment_increase()
    {
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var history = new[]
        {
            new ObservedSetRecordV2(s1, 2, new DateOnly(2026, 11, 2), DateTimeOffset.UtcNow.AddDays(-7), 1, SetKindV2.Working, 12, 100m, RirActual: null),
            new ObservedSetRecordV2(s1, 2, new DateOnly(2026, 11, 2), DateTimeOffset.UtcNow.AddDays(-7), 2, SetKindV2.Working, 12, 100m, RirActual: null),
            new ObservedSetRecordV2(s2, 2, new DateOnly(2026, 11, 9), DateTimeOffset.UtcNow.AddDays(-2), 1, SetKindV2.Working, 12, 100m, RirActual: null),
            new ObservedSetRecordV2(s2, 2, new DateOnly(2026, 11, 9), DateTimeOffset.UtcNow.AddDays(-2), 2, SetKindV2.Working, 12, 100m, RirActual: null)
        };

        var decision = WorkoutProgressionAndRecoveryV2.EvaluateStrengthProgression(
            LegPress, prescribedSets: 2, minReps: 8, maxReps: 12, targetRir: 3, history);

        Assert.Equal(ProgressionDecisionKindV2.RecommendIncrease, decision.Decision);
        Assert.Equal(105m, decision.RecommendedLoadKg);
        Assert.Equal(8, decision.RecommendedTargetRepsAfterIncrease);
    }

    [Fact]
    public void Gate73_two_consecutive_below_min_sessions_without_rir_recommend_reduction()
    {
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var history = new[]
        {
            new ObservedSetRecordV2(s1, 2, new DateOnly(2026, 11, 2), DateTimeOffset.UtcNow.AddDays(-7), 1, SetKindV2.Working, 6, 100m, RirActual: null),
            new ObservedSetRecordV2(s1, 2, new DateOnly(2026, 11, 2), DateTimeOffset.UtcNow.AddDays(-7), 2, SetKindV2.Working, 6, 100m, RirActual: null),
            new ObservedSetRecordV2(s2, 2, new DateOnly(2026, 11, 9), DateTimeOffset.UtcNow.AddDays(-2), 1, SetKindV2.Working, 7, 100m, RirActual: null),
            new ObservedSetRecordV2(s2, 2, new DateOnly(2026, 11, 9), DateTimeOffset.UtcNow.AddDays(-2), 2, SetKindV2.Working, 7, 100m, RirActual: null)
        };

        var decision = WorkoutProgressionAndRecoveryV2.EvaluateStrengthProgression(
            LegPress, prescribedSets: 2, minReps: 8, maxReps: 12, targetRir: 3, history);

        Assert.Equal(ProgressionDecisionKindV2.RecommendDecrease, decision.Decision);
        Assert.Equal(95m, decision.RecommendedLoadKg);
    }

    [Fact]
    public void Gate75_unilateral_progression_gates_on_weaker_side()
    {
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var history = new[]
        {
            new ObservedSetRecordV2(s1, 14, new DateOnly(2026, 11, 2), DateTimeOffset.UtcNow.AddDays(-7), 1, SetKindV2.Working, 12, 20m, LeftSideReps: 12, RightSideReps: 12),
            new ObservedSetRecordV2(s1, 14, new DateOnly(2026, 11, 2), DateTimeOffset.UtcNow.AddDays(-7), 2, SetKindV2.Working, 12, 20m, LeftSideReps: 12, RightSideReps: 12),
            new ObservedSetRecordV2(s2, 14, new DateOnly(2026, 11, 9), DateTimeOffset.UtcNow.AddDays(-2), 1, SetKindV2.Working, 12, 20m, LeftSideReps: 10, RightSideReps: 12),
            new ObservedSetRecordV2(s2, 14, new DateOnly(2026, 11, 9), DateTimeOffset.UtcNow.AddDays(-2), 2, SetKindV2.Working, 12, 20m, LeftSideReps: 10, RightSideReps: 12)
        };

        var decision = WorkoutProgressionAndRecoveryV2.EvaluateStrengthProgression(
            BulgarianSplitSquat, prescribedSets: 2, minReps: 8, maxReps: 12, targetRir: 3, history);

        Assert.Equal(ProgressionDecisionKindV2.Maintain, decision.Decision);
        Assert.Equal(20m, decision.RecommendedLoadKg);
    }

    [Fact]
    public void Gate77_unknown_machine_load_requires_calibration_never_zero()
    {
        var decision = WorkoutProgressionAndRecoveryV2.EvaluateStrengthProgression(
            LegPress, prescribedSets: 2, minReps: 8, maxReps: 12, targetRir: 3, history: []);

        Assert.Equal(ProgressionDecisionKindV2.CalibrationRequired, decision.Decision);
        Assert.Null(decision.RecommendedLoadKg);
    }

    [Fact]
    public void Gate88_72h_timer_alone_never_clears_and_resolved_return_uses_conservative_load()
    {
        var reported = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        var after72h = reported.AddHours(73);

        var unresolved = new PainRestrictionV2(2, null, InjuryFlag.Knee, reported, IsResolved: false);
        var unresolvedEval = WorkoutProgressionAndRecoveryV2.EvaluatePainReturn(unresolved, after72h, 100m, 5m);
        Assert.True(unresolvedEval.ReassessmentDue);
        Assert.False(unresolvedEval.CanReturnWithConservativeLoad);

        var resolved = unresolved with { IsResolved = true, ConservativeReturnSessionsCompleted = 0 };
        var resolvedEval = WorkoutProgressionAndRecoveryV2.EvaluatePainReturn(resolved, after72h, 100m, 5m);
        Assert.True(resolvedEval.CanReturnWithConservativeLoad);
        Assert.Equal(85m, resolvedEval.ConservativeLoadKg); // 100 * 0.875 = 87.5 floored to 5kg step -> 85kg
    }
}
