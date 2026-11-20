using MuscleCuties.Core.Domain.Health;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.V2;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests.V2;

/// <summary>
/// Gate 62 (WE2-G62): Core scenario contracts. Persistence and simulator journeys have separate host oracles.
/// </summary>
public sealed class ScenarioMatrix26Tests
{
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static readonly DateOnly AdultBirthDate = new(1995, 5, 10);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    public void S01_S02_and_available_day_counts_produce_bounded_feasible_splits(int dayCount)
    {
        var days = Enum.GetValues<DayOfWeek>().Take(dayCount).ToHashSet();
        var input = CreateInput(days, TrainingFocus.Balanced, StrengthStyleV2.GeneralStrength,
            dayCount >= 4 ? TrainingExperienceLevel.Intermediate : TrainingExperienceLevel.Beginner);

        var result = WorkoutGenerationEngineV2.Generate(input);

        Assert.NotEqual(WorkoutGenerationStatusV2.InvalidInput, result.Status);
        Assert.NotEqual(WorkoutGenerationStatusV2.SafetyBlocked, result.Status);
        Assert.InRange(result.AdaptedSessions.Count, 1, 5);
    }

    [Theory]
    [InlineData(TrainingExperienceLevel.Beginner, 3)]
    [InlineData(TrainingExperienceLevel.Intermediate, 4)]
    [InlineData(TrainingExperienceLevel.Advanced, 5)]
    public void S02_S04_requested_frequency_never_bypasses_experience_or_return_caps(TrainingExperienceLevel experience, int cap)
    {
        var input = CreateInput(Enum.GetValues<DayOfWeek>().ToHashSet(), TrainingFocus.Balanced,
            StrengthStyleV2.GeneralStrength, experience) with { RequestedStrengthDaysPerWeek = 7 };
        Assert.Equal(cap, WorkoutGenerationEngineV2.Generate(input).AdaptedSessions.Count);
        Assert.Equal(2, WorkoutGenerationEngineV2.Generate(input with { IsReturningAfterDetraining = true }).AdaptedSessions.Count);
    }

    [Fact]
    public void Two_available_strength_days_preserve_two_direct_glute_exposures()
    {
        var input = CreateInput([DayOfWeek.Monday, DayOfWeek.Thursday], TrainingFocus.Balanced,
            StrengthStyleV2.GeneralStrength);
        var result = WorkoutGenerationEngineV2.Generate(input);
        var glutes = Assert.Single(result.CoverageSummary!.Muscles, muscle => muscle.Muscle == MuscleGroupV2.GluteusMaximus);
        Assert.Equal(2, glutes.MeaningfulDirectDays);
        Assert.True(glutes.IndirectSets > 0);
    }

    [Fact]
    public void Missing_direct_glute_equipment_preserves_feasible_work_and_explains_the_gap()
    {
        var input = CreateInput([DayOfWeek.Monday, DayOfWeek.Thursday], TrainingFocus.Balanced,
            StrengthStyleV2.GeneralStrength);
        var result = WorkoutGenerationEngineV2.Generate(input with {
            Catalog = input.Catalog.Where(item => !item.MuscleContributions.Any(contribution =>
                contribution == ExerciseMuscleContributionV2.Direct(MuscleGroupV2.GluteusMaximus))).ToArray()
        });
        Assert.Equal(2, result.AdaptedSessions.Count);
        Assert.Contains("WeeklyCoverageBelowTarget_DirectGluteDays", result.AdvisoryCodes);
    }

    [Fact]
    public void S03_glute_focus_4day_upper_lower_allocates_direct_abduction_and_lower_patterns()
    {
        var input = CreateInput(
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            TrainingFocus.GluteSpecialization,
            StrengthStyleV2.HighVolumeStrength,
            TrainingExperienceLevel.Intermediate);

        var result = WorkoutGenerationEngineV2.Generate(input);

        Assert.Equal(4, result.AdaptedSessions.Count);
        Assert.Contains(result.AdaptedSessions, s => s.SplitKind == SplitKindV2.UpperLower);
        Assert.True(result.CoverageSummary!.CoversDirectHipAbduction);
        Assert.True(result.CoverageSummary.CoversKneeDominant);
        Assert.True(result.CoverageSummary.CoversKneeFlexion);
        Assert.True(result.CoverageSummary.CoversHipExtension);
        Assert.InRange(Assert.Single(result.CoverageSummary.Muscles, muscle => muscle.Muscle == MuscleGroupV2.GluteusMaximus).DirectSets, 8m, 12m);
        Assert.All(result.CoverageSummary.Muscles, muscle => Assert.True(muscle.MeetsTargetVolume, muscle.Muscle.ToString()));
    }

    [Fact]
    public void S05_bodyweight_only_filters_machines_and_discloses_catalog_gaps()
    {
        const int machineExerciseId = 903;
        var input = CreateInput([DayOfWeek.Monday], TrainingFocus.Balanced, StrengthStyleV2.GeneralStrength) with
        {
            EquipmentProfile = Equipment.Bodyweight,
            AvailableEquipment = new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
            Catalog =
            [
                SyntheticExercise(901, "bodyweight push", MovementPattern.HorizontalPush, MuscleGroupV2.Chest,
                    ExerciseEquipmentKind.Bodyweight, isBodyweightOnly: true),
                SyntheticExercise(902, "bodyweight squat", MovementPattern.SquatPattern, MuscleGroupV2.Quadriceps,
                    ExerciseEquipmentKind.Bodyweight, isBodyweightOnly: true),
                SyntheticExercise(machineExerciseId, "machine row", MovementPattern.HorizontalPull, MuscleGroupV2.Back,
                    ExerciseEquipmentKind.Machine, isBodyweightOnly: false)
            ]
        };

        var result = WorkoutGenerationEngineV2.Generate(input);
        var exercises = result.AdaptedSessions.SelectMany(session => session.Exercises).ToArray();

        Assert.NotEqual(WorkoutGenerationStatusV2.SafetyBlocked, result.Status);
        Assert.NotEmpty(exercises);
        Assert.DoesNotContain(exercises, exercise => exercise.ExerciseId == machineExerciseId);
        Assert.Contains(WeeklyMovementCoverageRule.MissingKneeFlexion, result.AdvisoryCodes);
    }

    [Fact]
    public void S06_bands_and_dumbbells_include_only_equipped_synthetic_movements()
    {
        const int bandExerciseId = 911;
        const int dumbbellExerciseId = 912;
        const int unavailableMachineId = 913;
        var equipment = new HashSet<ExerciseEquipmentKind>
        {
            ExerciseEquipmentKind.ResistanceBand,
            ExerciseEquipmentKind.Dumbbell
        };
        var input = CreateInput([DayOfWeek.Monday, DayOfWeek.Thursday], TrainingFocus.Balanced,
            StrengthStyleV2.GeneralStrength) with
        {
            EquipmentProfile = Equipment.HomeDumbbellsBands,
            AvailableEquipment = equipment,
            Catalog =
            [
                SyntheticExercise(bandExerciseId, "band row", MovementPattern.HorizontalPull, MuscleGroupV2.Back,
                    ExerciseEquipmentKind.ResistanceBand, isBodyweightOnly: false),
                SyntheticExercise(dumbbellExerciseId, "dumbbell press", MovementPattern.HorizontalPush, MuscleGroupV2.Chest,
                    ExerciseEquipmentKind.Dumbbell, isBodyweightOnly: false),
                SyntheticExercise(unavailableMachineId, "machine press", MovementPattern.HorizontalPush, MuscleGroupV2.Chest,
                    ExerciseEquipmentKind.Machine, isBodyweightOnly: false)
            ]
        };

        var result = WorkoutGenerationEngineV2.Generate(input);
        var exercises = result.AdaptedSessions.SelectMany(session => session.Exercises).ToArray();

        Assert.NotEmpty(exercises);
        Assert.All(exercises, exercise => Assert.Contains(exercise.ExerciseId,
            new[] { bandExerciseId, dumbbellExerciseId }));
        Assert.DoesNotContain(exercises, exercise => exercise.ExerciseId == unavailableMachineId);
    }

    [Fact]
    public void S07_time_constrained_40_minute_session_never_exceeds_budget()
    {
        var input = CreateInput(
            [DayOfWeek.Monday, DayOfWeek.Wednesday],
            TrainingFocus.Balanced,
            StrengthStyleV2.GeneralStrength) with
        {
            SessionDurationMinutesByActivity = new Dictionary<WorkoutActivityType, int>
            {
                [WorkoutActivityType.GeneralStrength] = 40
            }
        };

        var result = WorkoutGenerationEngineV2.Generate(input);

        Assert.All(result.AdaptedSessions, s => Assert.True(s.EstimatedTotalMinutes <= 40m));
        Assert.Contains(result.RuleTrace, entry => entry.StartsWith("SupersetPaired:", StringComparison.Ordinal));
        var trace = result.RuleTrace.ToList();
        var firstPair = trace.FindIndex(entry => entry.StartsWith("SupersetPaired:", StringComparison.Ordinal));
        var firstTrim = trace.FindIndex(entry => entry.StartsWith("Trimmed", StringComparison.Ordinal));
        Assert.True(firstTrim < 0 || firstPair < firstTrim, "Compatible supersets must precede accessory trimming.");
    }

    [Fact]
    public void S16_independent_sessions_never_exceed_two_per_day()
    {
        var input = CreateInput(Enum.GetValues<DayOfWeek>().ToHashSet(), TrainingFocus.Balanced,
            StrengthStyleV2.GeneralStrength) with
        {
            SelectedActivities = new HashSet<WorkoutActivityType>
            {
                WorkoutActivityType.GeneralStrength,
                WorkoutActivityType.Cycling,
                WorkoutActivityType.Running,
                WorkoutActivityType.Swimming,
                WorkoutActivityType.Rowing,
                WorkoutActivityType.Elliptical,
                WorkoutActivityType.Walking
            }
        };

        var result = WorkoutGenerationEngineV2.Generate(input);

        Assert.All(result.AdaptedSessions.GroupBy(session => session.LocalDate),
            day => Assert.InRange(day.Count(), 1, WorkoutPolicyV2.MaximumIndependentSessionsPerDay));
    }

    [Theory]
    [InlineData("policy")]
    [InlineData("input-contract")]
    public void S23_unsupported_policy_or_input_contract_version_is_rejected(string changedVersion)
    {
        var input = CreateInput([DayOfWeek.Monday, DayOfWeek.Wednesday], TrainingFocus.Balanced,
            StrengthStyleV2.GeneralStrength);
        input = changedVersion switch
        {
            "policy" => input with { PolicyVersion = "synthetic-obsolete-policy" },
            "input-contract" => input with { InputContractVersion = "synthetic-obsolete-input-contract" },
            _ => throw new ArgumentOutOfRangeException(nameof(changedVersion))
        };

        var result = WorkoutGenerationEngineV2.Generate(input);

        Assert.Equal(WorkoutGenerationStatusV2.InvalidInput, result.Status);
        Assert.Contains(changedVersion == "policy"
            ? WorkoutGenerationEngineV2.UnsupportedPolicyVersionCode
            : WorkoutGenerationEngineV2.UnsupportedInputContractVersionCode, result.LimitationCodes);
    }

    [Fact]
    public void S10_local_pain_restriction_excludes_provoking_exercise_and_preserves_rest_of_session()
    {
        var input = CreateInput(
            [DayOfWeek.Monday, DayOfWeek.Thursday],
            TrainingFocus.Balanced,
            StrengthStyleV2.GeneralStrength) with
        {
            ActivePainRestrictions =
            [
                new PainRestrictionV2(
                    ExerciseId: 2,
                    BlockedPattern: MovementPattern.SquatPattern,
                    SiteFlag: InjuryFlag.Knee,
                    ReportedAtUtc: new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                    IsResolved: false)
            ]
        };

        var result = WorkoutGenerationEngineV2.Generate(input);

        Assert.NotEqual(WorkoutGenerationStatusV2.SafetyBlocked, result.Status);
        Assert.All(result.AdaptedSessions, s => Assert.DoesNotContain(s.Exercises, e => e.ExerciseId == 2));
        Assert.True(result.AdaptedSessions.All(s => s.Exercises.Count > 0));
    }

    [Fact]
    public void S25_all_four_cycle_phases_produce_identical_prescription_hash_when_readiness_matches()
    {
        var hashes = new[]
        {
            CyclePhase.Menstrual,
            CyclePhase.Follicular,
            CyclePhase.Ovulatory,
            CyclePhase.Luteal
        }.Select(phase =>
        {
            var input = CreateInput([DayOfWeek.Monday, DayOfWeek.Wednesday], TrainingFocus.Balanced, StrengthStyleV2.GeneralStrength) with
            {
                CycleContext = new CycleContextInputV2(phase, IsEstimatedOnly: true)
            };
            return WorkoutGenerationEngineV2.Generate(input).NormalizedPrescriptionHash;
        }).Distinct().ToArray();

        Assert.Single(hashes);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void S04_established_advanced_uses_five_hybrid_days_and_glute_target(int availableDays)
    {
        var input = CreateInput(Enum.GetValues<DayOfWeek>().Take(availableDays).ToHashSet(),
            TrainingFocus.GluteSpecialization, StrengthStyleV2.HighVolumeStrength,
            TrainingExperienceLevel.Advanced) with { DistinctCompletedWorkoutDays = 130 };
        var result = WorkoutGenerationEngineV2.Generate(input);
        Assert.Equal(5, result.AdaptedSessions.Count);
        Assert.All(result.AdaptedSessions, session => Assert.Equal(SplitKindV2.Hybrid, session.SplitKind));
        var glutes = Assert.Single(result.CoverageSummary!.Muscles, muscle => muscle.Muscle == MuscleGroupV2.GluteusMaximus);
        Assert.InRange(glutes.DirectSets, 10m, 16m);
        Assert.All(result.CoverageSummary.Muscles.Where(muscle => muscle.TargetRange.MaximumSets > 0),
            muscle => Assert.InRange(muscle.DirectSets, muscle.TargetRange.MinimumSets, muscle.TargetRange.MaximumSets));
        Assert.All(result.AdaptedSessions, session => Assert.True(session.EstimatedTotalMinutes <= 90));
        Assert.All(result.AdaptedSessions.SelectMany(session => session.Exercises), exercise => Assert.True(exercise.WorkingSets >= 2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S24_sunday_lower_work_moves_monday_to_upper_and_records_rolling_load(bool priorExerciseNowExcluded)
    {
        var input = CreateInput([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            TrainingFocus.GluteSpecialization, StrengthStyleV2.HighVolumeStrength,
            TrainingExperienceLevel.Intermediate) with
        {
            PermanentlyExcludedExerciseIds = priorExerciseNowExcluded ? new HashSet<int> { 1 } : null,
            PriorObservedSets = [new ObservedSetRecordV2(Guid.NewGuid(), 1, Today.AddDays(-1),
                new DateTimeOffset(Today.AddDays(-1).ToDateTime(new TimeOnly(18, 0)), TimeSpan.Zero),
                1, SetKindV2.Working, 10, 80m)]
        };
        var result = WorkoutGenerationEngineV2.Generate(input);
        Assert.Contains("Rolling7DayLoadAccounted", result.RuleTrace);
        var monday = Assert.Single(result.AdaptedSessions, session => session.Weekday == DayOfWeek.Monday);
        Assert.DoesNotContain(monday.Exercises, exercise => exercise.Pattern is MovementPattern.HipThrust
            or MovementPattern.SquatPattern or MovementPattern.HipHinge or MovementPattern.KneeFlexion);
        Assert.Equal(4, result.AdaptedSessions.Count);
    }

    [Fact]
    public void S01_one_day_beginner_gets_full_body_and_coverage_advisory()
    {
        var result = WorkoutGenerationEngineV2.Generate(CreateInput([DayOfWeek.Monday],
            TrainingFocus.Balanced, StrengthStyleV2.GeneralStrength));
        Assert.Equal(SplitKindV2.FullBody, Assert.Single(result.AdaptedSessions).SplitKind);
        Assert.Contains(WorkoutGenerationEngineV2.OneDayFullBodyAdvisoryCode, result.AdvisoryCodes);
        Assert.NotEmpty(result.AdaptedSessions[0].Exercises);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void S02_two_to_three_beginner_days_get_full_body(int count)
    {
        var result = WorkoutGenerationEngineV2.Generate(CreateInput(
            new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }.Take(count).ToHashSet(),
            TrainingFocus.Balanced, StrengthStyleV2.GeneralStrength));
        Assert.Equal(count, result.AdaptedSessions.Count);
        Assert.All(result.AdaptedSessions, session => Assert.Equal(SplitKindV2.FullBody, session.SplitKind));
    }

    [Fact]
    public void S08_poor_sleep_removes_exactly_one_optional_set_and_preserves_main_weights()
    {
        var input = CreateInput([DayOfWeek.Monday, DayOfWeek.Thursday], TrainingFocus.Balanced,
            StrengthStyleV2.GeneralStrength);
        var baseline = WorkoutGenerationEngineV2.Generate(input);
        var tired = WorkoutGenerationEngineV2.Generate(input with
            { DailyReadiness = new DailyReadinessInputV2(2, 1, 1, 5m, null) });
        Assert.True(tired.ReadinessAdaptationApplied);
        Assert.Equal(baseline.AdaptedSessions.Sum(session => session.PhysicalWorkingSets) - 1,
            tired.AdaptedSessions.Sum(session => session.PhysicalWorkingSets));
        Assert.Equal(baseline.AdaptedSessions.SelectMany(session => session.Exercises)
                .Where(exercise => exercise.IsPriorityOrCompound).Select(exercise => (exercise.WorkingSets, exercise.PrescribedLoadKg)),
            tired.AdaptedSessions.SelectMany(session => session.Exercises)
                .Where(exercise => exercise.IsPriorityOrCompound).Select(exercise => (exercise.WorkingSets, exercise.PrescribedLoadKg)));
    }

    [Fact]
    public void S09_all_25_pairs_of_DOMS_values_preserve_prescription_hash()
    {
        var input = CreateInput([DayOfWeek.Monday, DayOfWeek.Thursday], TrainingFocus.GluteSpecialization,
            StrengthStyleV2.HighVolumeStrength);
        var results = Enumerable.Range(1, 5).Select(doms => WorkoutGenerationEngineV2.Generate(input with
            { DailyReadiness = new DailyReadinessInputV2(2, 1, 2, 8m, doms) })).ToArray();
        foreach (var first in results)
        foreach (var second in results)
            Assert.Equal(first.NormalizedPrescriptionHash, second.NormalizedPrescriptionHash);
    }

    [Fact]
    public void S11_systemic_red_flags_stop_all_prescriptions()
    {
        var result = WorkoutGenerationEngineV2.Generate(CreateInput([DayOfWeek.Monday], TrainingFocus.Balanced,
            StrengthStyleV2.GeneralStrength) with
            { DailyReadiness = new DailyReadinessInputV2(2, 1, 2, 8m, null, HasSystemicRedFlagSymptoms: true) });
        Assert.Equal(WorkoutGenerationStatusV2.SafetyBlocked, result.Status);
        Assert.Empty(result.AdaptedSessions);
        Assert.Contains(WorkoutGenerationEngineV2.SystemicRedFlagSafetyStopCode, result.LimitationCodes);
    }

    [Fact]
    public void S12_missing_RIR_stays_null_in_capture_and_two_sessions_earn_progression()
    {
        var captured = ObservedSetInputMapping.Build(true, [new EditableObservedSet(1, 12, 50m, null)]);
        Assert.Null(Assert.Single(captured!).RirActual);
        var (exercise, history) = ProgressionFixture(12, 12);
        Assert.All(history, set => Assert.Null(set.RirActual));
        var decision = WorkoutProgressionAndRecoveryV2.EvaluateStrengthProgression(exercise, 2, 8, 12, 3, history);
        Assert.Equal(ProgressionDecisionKindV2.RecommendIncrease, decision.Decision);
        Assert.Equal(55m, decision.RecommendedLoadKg);
    }

    [Fact]
    public void S13_rep_climb_holds_load_until_two_upper_bound_sessions_then_equipment_increment()
    {
        var (exercise, climbing) = ProgressionFixture(10, 11);
        var hold = WorkoutProgressionAndRecoveryV2.EvaluateStrengthProgression(exercise, 2, 8, 12, 3, climbing);
        Assert.Equal(ProgressionDecisionKindV2.Maintain, hold.Decision);
        Assert.Equal(50m, hold.RecommendedLoadKg);
        var (_, atTop) = ProgressionFixture(12, 12);
        var increase = WorkoutProgressionAndRecoveryV2.EvaluateStrengthProgression(exercise, 2, 8, 12, 3, atTop);
        Assert.Equal(55m, increase.RecommendedLoadKg);
        Assert.Equal(8, increase.RecommendedTargetRepsAfterIncrease);
    }

    [Fact]
    public void S14_one_deficit_holds_and_two_consecutive_deficits_reduce_without_RIR()
    {
        var (exercise, singleDeficit) = ProgressionFixture(10, 7);
        Assert.Equal(ProgressionDecisionKindV2.Maintain,
            WorkoutProgressionAndRecoveryV2.EvaluateStrengthProgression(exercise, 2, 8, 12, 3, singleDeficit).Decision);
        var (_, twoDeficits) = ProgressionFixture(6, 7);
        var decrease = WorkoutProgressionAndRecoveryV2.EvaluateStrengthProgression(exercise, 2, 8, 12, 3, twoDeficits);
        Assert.Equal(ProgressionDecisionKindV2.RecommendDecrease, decrease.Decision);
        Assert.Equal(45m, decrease.RecommendedLoadKg);
    }

    [Fact]
    public void S15_machine_swap_isolates_load_history_and_requires_calibration()
    {
        var (exercise, history) = ProgressionFixture(12, 12);
        var swapped = exercise with { ExerciseId = 202, Name = "Synthetic different machine" };
        var result = WorkoutProgressionAndRecoveryV2.EvaluateStrengthProgression(swapped, 2, 8, 12, 3, history);
        Assert.Equal(ProgressionDecisionKindV2.CalibrationRequired, result.Decision);
        Assert.Null(result.RecommendedLoadKg);
    }

    [Fact]
    public void S17_post_strength_finisher_is_light_at_most_30_minutes_and_not_an_independent_session()
    {
        var input = CreateInput([DayOfWeek.Monday], TrainingFocus.Balanced, StrengthStyleV2.GeneralStrength) with
        {
            SelectedActivities = new HashSet<WorkoutActivityType> { WorkoutActivityType.GeneralStrength, WorkoutActivityType.Cycling },
            SessionDurationMinutesByActivity = new Dictionary<WorkoutActivityType, int>
                { [WorkoutActivityType.GeneralStrength] = 90, [WorkoutActivityType.Cycling] = 60 }
        };
        var session = Assert.Single(WorkoutGenerationEngineV2.Generate(input).AdaptedSessions);
        Assert.NotEmpty(session.Exercises);
        Assert.NotNull(session.AttachedFinisher);
        Assert.Equal(30, session.AttachedFinisher.DurationMinutes);
        Assert.Equal(CardioIntensityBandV2.Light, session.AttachedFinisher.IntensityBand);
    }

    [Fact]
    public void S18_active_partial_session_keeps_observed_rows_and_cannot_be_regenerated()
    {
        var decision = WorkoutSessionLifecycleRule.CanModifySession(WorkoutSessionLifecycleState.InProgress);
        Assert.False(decision.IsAllowed);
        var saved = ObservedSetInputMapping.Build(true,
            [new EditableObservedSet(1, 9, 25m, null), new EditableObservedSet(2, 0, 25m, null)]);
        var restored = System.Text.Json.JsonSerializer.Deserialize<ObservedSetRecord[]>(
            System.Text.Json.JsonSerializer.Serialize(saved));
        var actual = Assert.Single(restored!);
        Assert.Equal(9, actual.RepsCompleted);
        Assert.Equal(25m, actual.LoadKg);
        Assert.Null(actual.RirActual);
    }

    [Fact]
    public void S19_completed_session_is_terminal_and_blocks_post_completion_mutation()
    {
        var decision = WorkoutSessionLifecycleRule.CanModifySession(WorkoutSessionLifecycleState.Completed);
        Assert.False(decision.IsAllowed);
        Assert.Equal(WorkoutSessionLifecycleRule.ReasonCompletedOrInProgressSessionCannotBeModified, decision.ReasonCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S20_first_feasible_full_or_reduced_volume_plan_auto_applies(bool reducedVolume)
    {
        Assert.True(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(false, true, true, false, false, false, reducedVolume));
        Assert.False(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(false, false, true, false, false, false, reducedVolume));
        var acknowledgment = WorkoutPlanningFlowRule.AcceptanceAcknowledgments(false, reducedVolume);
        Assert.False(acknowledgment.ReplacementConfirmed);
        Assert.False(acknowledgment.ReducedVolumeAcknowledged);
    }

    [Fact]
    public void S21_existing_active_plan_requires_explicit_replacement_acceptance()
    {
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(true, true, true, true, true, false, false, false));
        Assert.True(WorkoutPlanningFlowRule.RequiresReplacementConfirmation(true, false));
        Assert.True(WorkoutPlanningFlowRule.AcceptanceAcknowledgments(true, false).ReplacementConfirmed);
        Assert.False(WorkoutSessionLifecycleRule.CanModifySession(WorkoutSessionLifecycleState.Completed).IsAllowed);
    }

    [Fact]
    public void S22_one_hundred_runs_and_catalog_permutations_have_identical_hashes()
    {
        var input = CreateInput([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            TrainingFocus.GluteSpecialization, StrengthStyleV2.HighVolumeStrength);
        var hash = WorkoutGenerationEngineV2.Generate(input).NormalizedPrescriptionHash;
        for (var run = 0; run < 100; run++)
        {
            var permuted = input.Catalog.Skip(run % input.Catalog.Count).Concat(input.Catalog.Take(run % input.Catalog.Count));
            if (run % 2 == 0) permuted = permuted.Reverse();
            Assert.Equal(hash, WorkoutGenerationEngineV2.Generate(input with { Catalog = permuted.ToArray() }).NormalizedPrescriptionHash);
        }
    }

    private static (ExerciseCatalogItemV2 Exercise, ObservedSetRecordV2[] History) ProgressionFixture(int firstReps, int secondReps)
    {
        var exercise = CreateInput([DayOfWeek.Monday], TrainingFocus.Balanced, StrengthStyleV2.GeneralStrength).Catalog.Single(item => item.ExerciseId == 2);
        var history = new[] { (Id: Guid.NewGuid(), Reps: firstReps, DaysAgo: 7), (Id: Guid.NewGuid(), Reps: secondReps, DaysAgo: 2) }
            .SelectMany(session => Enumerable.Range(1, 2).Select(set => new ObservedSetRecordV2(session.Id,
                exercise.ExerciseId, Today.AddDays(-session.DaysAgo), new DateTimeOffset(Today.AddDays(-session.DaysAgo)
                    .ToDateTime(new TimeOnly(18, 0)), TimeSpan.Zero), set, SetKindV2.Working, session.Reps, 50m))).ToArray();
        return (exercise, history);
    }

    private static WorkoutGenerationInputV2 CreateInput(
        HashSet<DayOfWeek> days,
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
            new(2, "Leg Press", MovementPattern.SquatPattern, [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Quadriceps), ExerciseMuscleContributionV2.Indirect(MuscleGroupV2.GluteusMaximus)], allEquip, allExp, InjuryFlag.Knee, true, false, false, 1.5m, 3, 5.0m),
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
            AvailableWeekdays: days,
            SessionDurationMinutesByActivity: new Dictionary<WorkoutActivityType, int> { [WorkoutActivityType.GeneralStrength] = 90 },
            EquipmentProfile: Equipment.FullGym,
            AvailableEquipment: allEquip,
            Catalog: catalog);
    }

    private static ExerciseCatalogItemV2 SyntheticExercise(
        int id,
        string name,
        MovementPattern pattern,
        MuscleGroupV2 muscle,
        ExerciseEquipmentKind equipment,
        bool isBodyweightOnly)
    {
        var experiences = new HashSet<TrainingExperienceLevel>
        {
            TrainingExperienceLevel.Beginner,
            TrainingExperienceLevel.Intermediate,
            TrainingExperienceLevel.Advanced
        };
        return new ExerciseCatalogItemV2(id, $"Synthetic test {name}", pattern,
            [ExerciseMuscleContributionV2.Direct(muscle)], new HashSet<ExerciseEquipmentKind> { equipment },
            experiences, InjuryFlag.None, IsCompound: true, IsUnilateral: false, IsBodyweightOnly: isBodyweightOnly,
            SetupMinutes: 0.5m, SecondsPerRep: 3, MinimumLoadIncrementKg: 1m,
            ReviewStatus: CatalogReviewStatusV2.ApprovedWithConditions, ReviewVersion: "SYNTHETIC-TEST-ONLY");
    }
}
