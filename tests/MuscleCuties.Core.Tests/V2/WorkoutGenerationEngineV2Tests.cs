using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout.Planning.V2;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests.V2;

public sealed class WorkoutGenerationEngineV2Tests
{
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static readonly DateOnly AdultBirthDate = new(1995, 5, 10);

    [Fact]
    public void One_day_beginner_generates_safe_full_body_plan_with_advisory()
    {
        var input = CreateBaseInput(
            availableDays: [DayOfWeek.Monday],
            focus: TrainingFocus.Balanced,
            style: StrengthStyleV2.GeneralStrength);

        var result = WorkoutGenerationEngineV2.Generate(input);

        Assert.NotEqual(WorkoutGenerationStatusV2.InvalidInput, result.Status);
        Assert.NotEqual(WorkoutGenerationStatusV2.SafetyBlocked, result.Status);
        Assert.Single(result.AdaptedSessions);
        Assert.Contains(WorkoutGenerationEngineV2.OneDayFullBodyAdvisoryCode, result.AdvisoryCodes);
        Assert.True(result.AdaptedSessions[0].Exercises.Count > 6); // No arbitrary 6-exercise cap
    }

    [Fact]
    public void Glute_focus_3day_includes_hip_abduction_3_lower_patterns_and_dynamic_ordering()
    {
        var input = CreateBaseInput(
            availableDays: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            focus: TrainingFocus.GluteSpecialization,
            style: StrengthStyleV2.GeneralStrength,
            experience: TrainingExperienceLevel.Intermediate);

        var result = WorkoutGenerationEngineV2.Generate(input);

        Assert.NotNull(result.CoverageSummary);
        Assert.True(result.CoverageSummary!.CoversKneeDominant);
        Assert.True(result.CoverageSummary.CoversKneeFlexion);
        Assert.True(result.CoverageSummary.CoversHipExtension);
        Assert.True(result.CoverageSummary.CoversDirectHipAbduction);
        Assert.True(result.CoverageSummary.CoversDirectCalves);
        Assert.True(result.CoverageSummary.CoversDirectRearDelts);
        Assert.True(result.CoverageSummary.DynamicCoreOccasions >= 2);

        // Gate 57: Hip Thrust leads session 0, but session 1 does not force Hip Thrust first
        Assert.Equal(MovementPattern.HipThrust, result.AdaptedSessions[0].Exercises[0].Pattern);
        Assert.NotEqual(MovementPattern.HipThrust, result.AdaptedSessions[1].Exercises[0].Pattern);
    }

    [Fact]
    public void Four_day_lower_sessions_keep_a_trunk_slot_and_at_least_four_exercises()
    {
        var input = CreateBaseInput(
            availableDays: [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            focus: TrainingFocus.Balanced,
            style: StrengthStyleV2.HighVolumeStrength,
            experience: TrainingExperienceLevel.Intermediate);

        var result = WorkoutGenerationEngineV2.Generate(input);
        var lowerSessions = result.AdaptedSessions
            .Where(session => session.SplitKind == SplitKindV2.UpperLower)
            .Where(session => session.Exercises.Any(exercise => exercise.Pattern is
                MovementPattern.SquatPattern or MovementPattern.Lunge or MovementPattern.HipHinge or
                MovementPattern.HipThrust))
            .ToArray();

        Assert.Equal(2, lowerSessions.Length);
        Assert.All(lowerSessions, session =>
        {
            Assert.True(session.Exercises.Count >= 4,
                $"Lower session {session.SessionIndex} has only {session.Exercises.Count} exercises.");
            Assert.Contains(session.Exercises, exercise => exercise.Pattern is
                MovementPattern.AntiExtension or MovementPattern.AntiRotation or MovementPattern.AntiLateralFlexion);
        });
    }

    [Fact]
    public void Thirty_minute_two_day_high_volume_plan_keeps_a_full_body_session()
    {
        var input = CreateBaseInput(
            availableDays: [DayOfWeek.Monday, DayOfWeek.Thursday],
            focus: TrainingFocus.Balanced,
            style: StrengthStyleV2.HighVolumeStrength) with
        {
            SessionDurationMinutesByActivity = new Dictionary<WorkoutActivityType, int>
            {
                [WorkoutActivityType.HighVolumeStrength] = 30
            }
        };

        var result = WorkoutGenerationEngineV2.Generate(input);

        Assert.All(result.AdaptedSessions, session =>
        {
            Assert.True(session.EstimatedTotalMinutes <= 30m,
                $"Session {session.SessionIndex} takes {session.EstimatedTotalMinutes} minutes.");
            Assert.True(session.Exercises.Count >= 4,
                $"Session {session.SessionIndex} has only {session.Exercises.Count} exercises: " +
                string.Join(", ", session.Exercises.Select(exercise => exercise.ExerciseName)) +
                $". Trace: {string.Join("; ", result.RuleTrace)}");
            Assert.Contains(session.Exercises, exercise => exercise.Pattern is
                MovementPattern.SquatPattern or MovementPattern.Lunge or MovementPattern.HipHinge or MovementPattern.HipThrust);
            Assert.Contains(session.Exercises, exercise => exercise.Pattern is
                MovementPattern.HorizontalPush or MovementPattern.VerticalPush);
            Assert.Contains(session.Exercises, exercise => exercise.Pattern is
                MovementPattern.HorizontalPull or MovementPattern.VerticalPull);
        });
    }

    [Fact]
    public void High_intensity_strength_uses_1_rir_2_sets_and_approved_rep_rest_ranges()
    {
        var input = CreateBaseInput(
            availableDays: [DayOfWeek.Monday, DayOfWeek.Thursday],
            focus: TrainingFocus.Balanced,
            style: StrengthStyleV2.StrengthHighIntensity);

        var result = WorkoutGenerationEngineV2.Generate(input);

        var allExercises = result.AdaptedSessions.SelectMany(s => s.Exercises).ToArray();
        Assert.NotEmpty(allExercises);
        Assert.All(allExercises, ex =>
        {
            Assert.Equal(1, ex.TargetRir);
            Assert.Equal(2, ex.WorkingSets);
            if (ex.IsPriorityOrCompound)
            {
                Assert.Equal(5, ex.MinRepetitions);
                Assert.Equal(8, ex.MaxRepetitions);
                Assert.InRange(ex.RestSeconds, 120, 150);
            }
            else
            {
                Assert.Equal(8, ex.MinRepetitions);
                Assert.Equal(12, ex.MaxRepetitions);
                Assert.InRange(ex.RestSeconds, 90, 120);
            }
        });
    }

    [Fact]
    public void Doms_and_cycle_phase_alone_leave_prescription_hash_completely_invariant()
    {
        var baseInput = CreateBaseInput(
            availableDays: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            focus: TrainingFocus.Balanced,
            style: StrengthStyleV2.GeneralStrength) with
        {
            DailyReadiness = new DailyReadinessInputV2(EnergyLevel: 2, FatigueLevel: 1, SleepQuality: 2, SleepHours: 8m, SorenessDoms: 1),
            CycleContext = new CycleContextInputV2(CyclePhase.Follicular, IsEstimatedOnly: true)
        };

        var highDomsLuteal = baseInput with
        {
            DailyReadiness = baseInput.DailyReadiness! with { SorenessDoms = 5 },
            CycleContext = new CycleContextInputV2(CyclePhase.Luteal, IsEstimatedOnly: true)
        };

        var resA = WorkoutGenerationEngineV2.Generate(baseInput);
        var resB = WorkoutGenerationEngineV2.Generate(highDomsLuteal);

        Assert.Equal(resA.NormalizedPrescriptionHash, resB.NormalizedPrescriptionHash);
        Assert.False(resA.ReadinessAdaptationApplied);
        Assert.False(resB.ReadinessAdaptationApplied);
    }

    [Fact]
    public void Low_readiness_reduces_one_optional_set_without_changing_original_sessions_or_loads()
    {
        var input = CreateBaseInput(
            availableDays: [DayOfWeek.Monday, DayOfWeek.Wednesday],
            focus: TrainingFocus.Balanced,
            style: StrengthStyleV2.GeneralStrength) with
        {
            DailyReadiness = new DailyReadinessInputV2(EnergyLevel: 1, FatigueLevel: 3, SleepQuality: 1, SleepHours: 5.5m, SorenessDoms: 4)
        };

        var result = WorkoutGenerationEngineV2.Generate(input);

        Assert.True(result.ReadinessAdaptationApplied);
        Assert.Equal(WorkoutGenerationEngineV2.ReadinessOptionalSetReducedExplanation, result.CoachExplanation);
        Assert.Equal(
            result.OriginalSessions[0].PhysicalWorkingSets - 1,
            result.AdaptedSessions[0].PhysicalWorkingSets);
        var originalByPosition = result.OriginalSessions.SelectMany(session => session.Exercises.Select(exercise =>
                (Session: session, Exercise: exercise)))
            .ToDictionary(item => (item.Session.SessionIndex, item.Exercise.OrderIndex), item => item.Exercise);
        foreach (var (session, exercise) in result.AdaptedSessions.SelectMany(session => session.Exercises.Select(exercise =>
                     (session, exercise))))
        {
            var original = originalByPosition[(session.SessionIndex, exercise.OrderIndex)];
            Assert.Equal(original.LoadStatus, exercise.LoadStatus);
            Assert.Equal(original.PrescribedLoadKg, exercise.PrescribedLoadKg);
        }
    }

    [Fact]
    public void Systemic_red_flag_and_pregnancy_block_generation_safely()
    {
        var redFlagInput = CreateBaseInput([DayOfWeek.Monday], TrainingFocus.Balanced, StrengthStyleV2.GeneralStrength) with
        {
            DailyReadiness = new DailyReadinessInputV2(1, 3, 1, 5m, null, HasSystemicRedFlagSymptoms: true)
        };
        var pregnancyInput = CreateBaseInput([DayOfWeek.Monday], TrainingFocus.Balanced, StrengthStyleV2.GeneralStrength) with
        {
            PregnancyStatus = PregnancyStatus.Yes
        };

        var redFlagRes = WorkoutGenerationEngineV2.Generate(redFlagInput);
        var pregRes = WorkoutGenerationEngineV2.Generate(pregnancyInput);

        Assert.Equal(WorkoutGenerationStatusV2.SafetyBlocked, redFlagRes.Status);
        Assert.Contains(WorkoutGenerationEngineV2.SystemicRedFlagSafetyStopCode, redFlagRes.LimitationCodes);
        Assert.Equal(WorkoutGenerationStatusV2.SafetyBlocked, pregRes.Status);
        Assert.Contains("WorkoutPopulationPregnancyUnsupported", pregRes.LimitationCodes);
    }

    [Fact]
    public void S17_strength_cardio_finisher_stays_attached_and_is_capped_at_30_minutes()
    {
        var input = CreateBaseInput([DayOfWeek.Monday, DayOfWeek.Thursday], TrainingFocus.Balanced, StrengthStyleV2.GeneralStrength) with
        {
            SelectedActivities = new HashSet<WorkoutActivityType>
            {
                WorkoutActivityType.GeneralStrength,
                WorkoutActivityType.Hiit,
                WorkoutActivityType.Cycling
            },
            SessionDurationMinutesByActivity = new Dictionary<WorkoutActivityType, int>
            {
                [WorkoutActivityType.GeneralStrength] = 90,
                [WorkoutActivityType.Cycling] = 45
            }
        };

        var result = WorkoutGenerationEngineV2.Generate(input);

        Assert.Contains(WorkoutGenerationEngineV2.HiitDeferredInV2Code, result.AdvisoryCodes);
        Assert.DoesNotContain(result.AdaptedSessions, s => s.Activity == WorkoutActivityType.Hiit);
        var finisher = result.AdaptedSessions[0].AttachedFinisher;
        Assert.NotNull(finisher);
        Assert.Equal(WorkoutActivityType.Cycling, finisher!.Activity);
        Assert.True(finisher.DurationMinutes <= 30);
        Assert.Equal(result.OriginalSessions.Count, result.AdaptedSessions.Count);
        Assert.All(result.AdaptedSessions, session => Assert.NotEmpty(session.Exercises));
    }

    [Fact]
    public void Determinism_100_repeated_runs_produce_identical_hash()
    {
        var input = CreateBaseInput([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], TrainingFocus.GluteSpecialization, StrengthStyleV2.GeneralStrength);
        var baseline = WorkoutGenerationEngineV2.Generate(input).NormalizedPrescriptionHash;

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(baseline, WorkoutGenerationEngineV2.Generate(input).NormalizedPrescriptionHash);
        }
    }

    private static WorkoutGenerationInputV2 CreateBaseInput(
        DayOfWeek[] availableDays,
        TrainingFocus focus,
        StrengthStyleV2 style,
        TrainingExperienceLevel experience = TrainingExperienceLevel.Beginner)
    {
        var allEquip = Enum.GetValues<ExerciseEquipmentKind>().ToHashSet();
        var allExp = new HashSet<TrainingExperienceLevel>
        {
            TrainingExperienceLevel.Beginner,
            TrainingExperienceLevel.Intermediate,
            TrainingExperienceLevel.Advanced
        };

        var catalog = new List<ExerciseCatalogItemV2>
        {
            new(1, "Hip Thrust Machine", MovementPattern.HipThrust, [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.GluteusMaximus)], allEquip, allExp, InjuryFlag.None, true, false, false, 1.5m, 3, 2.5m, IsGluteDominant: true),
            new(2, "Leg Press", MovementPattern.SquatPattern, [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Quadriceps), ExerciseMuscleContributionV2.Indirect(MuscleGroupV2.GluteusMaximus)], allEquip, allExp, InjuryFlag.None, true, false, false, 1.5m, 3, 5.0m),
            new(3, "Seated Leg Curl", MovementPattern.KneeFlexion, [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Hamstrings)], allEquip, allExp, InjuryFlag.None, false, false, false, 1.0m, 3, 2.5m),
            new(4, "Romanian Deadlift", MovementPattern.HipHinge, [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Hamstrings), ExerciseMuscleContributionV2.Indirect(MuscleGroupV2.GluteusMaximus)], allEquip, allExp, InjuryFlag.None, true, false, false, 1.5m, 3, 2.5m),
            new(5, "Hip Abduction Machine", MovementPattern.HipAbduction, [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.HipAbductors)], allEquip, allExp, InjuryFlag.None, false, false, false, 1.0m, 3, 2.5m),
            new(6, "Machine Chest Press", MovementPattern.HorizontalPush, [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Chest), ExerciseMuscleContributionV2.Indirect(MuscleGroupV2.Triceps)], allEquip, allExp, InjuryFlag.None, true, false, false, 1.0m, 3, 2.5m, VarietyTag: ExerciseVarietyTag.HorizontalChestPress),
            new(7, "Lat Pulldown", MovementPattern.VerticalPull, [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Back), ExerciseMuscleContributionV2.Indirect(MuscleGroupV2.Biceps)], allEquip, allExp, InjuryFlag.None, true, false, false, 1.0m, 3, 2.5m, VarietyTag: ExerciseVarietyTag.WideVerticalPull),
            new(8, "Reverse Pec Deck", MovementPattern.RearDelt, [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.RearDelts)], allEquip, allExp, InjuryFlag.None, false, false, false, 1.0m, 3, 2.5m, VarietyTag: ExerciseVarietyTag.RearDeltDirect),
            new(9, "Cable Lateral Raise", MovementPattern.LateralRaise, [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.LateralDelts)], allEquip, allExp, InjuryFlag.None, false, true, false, 1.0m, 3, 1.25m, VarietyTag: ExerciseVarietyTag.LateralRaise),
            new(10, "Cable Curl", MovementPattern.ElbowFlexion, [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Biceps)], allEquip, allExp, InjuryFlag.None, false, false, false, 1.0m, 3, 1.25m),
            new(11, "Cable Triceps Pushdown", MovementPattern.ElbowExtension, [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Triceps)], allEquip, allExp, InjuryFlag.None, false, false, false, 1.0m, 3, 1.25m),
            new(12, "Standing Calf Raise Machine", MovementPattern.CalfRaise, [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Calves)], allEquip, allExp, InjuryFlag.None, false, false, false, 1.0m, 2, 2.5m),
            new(13, "Abdominal Crunch Machine", MovementPattern.AntiExtension, [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Core)], allEquip, allExp, InjuryFlag.None, false, false, false, 1.0m, 2, 2.5m, VarietyTag: ExerciseVarietyTag.DynamicCore)
        };

        return new WorkoutGenerationInputV2(
            AccountLocalDate: Today,
            AccountTimeZoneId: "America/Toronto",
            BirthDate: AdultBirthDate,
            PregnancyStatus: PregnancyStatus.No,
            EffectiveExperience: experience,
            DistinctCompletedWorkoutDays: experience == TrainingExperienceLevel.Intermediate ? 25 : 5,
            IsReturningAfterDetraining: false,
            Goal: UserGoal.Strength,
            Focus: focus,
            StrengthStyle: style,
            SelectedActivities: new HashSet<WorkoutActivityType> { WorkoutActivityType.GeneralStrength },
            AvailableWeekdays: availableDays.ToHashSet(),
            SessionDurationMinutesByActivity: new Dictionary<WorkoutActivityType, int> { [WorkoutActivityType.GeneralStrength] = 90 },
            EquipmentProfile: Equipment.FullGym,
            AvailableEquipment: allEquip,
            Catalog: catalog);
    }
}
