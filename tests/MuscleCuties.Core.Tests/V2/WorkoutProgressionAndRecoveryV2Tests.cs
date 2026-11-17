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
    public void S15_new_machine_variant_does_not_copy_load_from_another_machine()
    {
        var machineVariant = new ExerciseCatalogItemV2(
            ExerciseId: 15,
            Name: "Synthetic test alternate leg press",
            Pattern: MovementPattern.SquatPattern,
            MuscleContributions: [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Quadriceps)],
            RequiredEquipment: new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Machine },
            SupportedExperiences: new HashSet<Models.Enums.Users.TrainingExperienceLevel>
                { Models.Enums.Users.TrainingExperienceLevel.Beginner },
            Contraindications: InjuryFlag.None,
            IsCompound: true,
            IsUnilateral: false,
            IsBodyweightOnly: false,
            SetupMinutes: 1m,
            SecondsPerRep: 3,
            MinimumLoadIncrementKg: 5m);
        var otherMachineHistory = new[]
        {
            new ObservedSetRecordV2(Guid.NewGuid(), LegPress.ExerciseId, new DateOnly(2026, 11, 9),
                DateTimeOffset.UtcNow.AddDays(-1), 1, SetKindV2.Working, 12, 100m)
        };

        var decision = WorkoutProgressionAndRecoveryV2.EvaluateStrengthProgression(
            machineVariant, prescribedSets: 1, minReps: 8, maxReps: 12, targetRir: 3, otherMachineHistory);

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

    [Fact]
    public void EngineV2_wires_double_progression_and_cardio_single_variable_step_into_generation()
    {
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var today = new DateOnly(2026, 11, 16);
        var t1 = new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

        var observedSets = new[]
        {
            new ObservedSetRecordV2(s1, LegPress.ExerciseId, new DateOnly(2026, 11, 9), t1, 1, SetKindV2.Working, 12, 100m, 3, true),
            new ObservedSetRecordV2(s1, LegPress.ExerciseId, new DateOnly(2026, 11, 9), t1, 2, SetKindV2.Working, 12, 100m, 3, true),
            new ObservedSetRecordV2(s2, LegPress.ExerciseId, new DateOnly(2026, 11, 12), t2, 1, SetKindV2.Working, 12, 100m, 3, true),
            new ObservedSetRecordV2(s2, LegPress.ExerciseId, new DateOnly(2026, 11, 12), t2, 2, SetKindV2.Working, 12, 100m, 3, true)
        };
        var observedCardio = new[]
        {
            new CardioActualRecordV2(WorkoutActivityType.Walking, new DateOnly(2026, 11, 10), 25, 2.0m, 2600, 4, null, null),
            new CardioActualRecordV2(WorkoutActivityType.Walking, new DateOnly(2026, 11, 13), 25, 2.1m, 2700, 4, null, null)
        };

        var input = new WorkoutGenerationInputV2(
            AccountLocalDate: today,
            AccountTimeZoneId: "UTC",
            BirthDate: new DateOnly(1995, 5, 10),
            PregnancyStatus: MuscleCuties.Core.Domain.Health.PregnancyStatus.No,
            EffectiveExperience: Models.Enums.Users.TrainingExperienceLevel.Beginner,
            DistinctCompletedWorkoutDays: 4,
            IsReturningAfterDetraining: false,
            Goal: Models.Enums.Users.UserGoal.Strength,
            Focus: TrainingFocus.Balanced,
            StrengthStyle: StrengthStyleV2.GeneralStrength,
            SelectedActivities: new HashSet<WorkoutActivityType> { WorkoutActivityType.GeneralStrength, WorkoutActivityType.Walking },
            AvailableWeekdays: new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
            SessionDurationMinutesByActivity: new Dictionary<WorkoutActivityType, int>(),
            EquipmentProfile: Equipment.FullGym,
            AvailableEquipment: new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Machine, ExerciseEquipmentKind.Dumbbell },
            Catalog: [LegPress, BulgarianSplitSquat],
            PriorObservedSets: observedSets,
            PriorCardioActuals: observedCardio,
            RequestedStrengthDaysPerWeek: 2);

        var generated = WorkoutGenerationEngineV2.Generate(input);

        var legPressRx = generated.AdaptedSessions
            .Where(s => s.Exercises.Count > 0)
            .SelectMany(s => s.Exercises)
            .First(e => e.ExerciseId == LegPress.ExerciseId);
        Assert.Equal(105m, legPressRx.PrescribedLoadKg);
        Assert.Equal(WorkoutProgressionAndRecoveryV2.DoubleProgressionEarnedCode, legPressRx.SelectionReasonCode);

        var walkingSession = generated.AdaptedSessions.Single(s => s.Activity == WorkoutActivityType.Walking);
        Assert.Equal(30m, walkingSession.EstimatedTotalMinutes);
        Assert.Contains(generated.RuleTrace, entry => entry.Contains("CardioProgression:Walking:SingleVariableDurationStepRecommended:30m"));
    }
}
