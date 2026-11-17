using System.Text.Json;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.Catalog;
using MuscleCuties.Core.Domain.Workout.Planning.V2;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Core.Tests.V2;

public sealed class Gate60V2GenerationTests
{
    private static readonly DateOnly ReferenceMonday = new(2026, 11, 16);

    [Fact]
    public void Bodyweight_only_plan_is_feasible_uses_gate60_movements_and_discloses_missing_upper_pull()
    {
        var source = BuildCatalogSource(Equipment.Bodyweight, gluteFocus: false);
        var request = BuildRequest(WorkoutActivityDto.GeneralStrength, Equipment.Bodyweight);

        var preview = PreviewStrengthPlanV020UseCase.EvaluateLoadedSource(Guid.NewGuid(), request, source);
        var canonical = GenerationPlanV2Mapper.ForCanonicalUse(preview);

        Assert.Equal("Feasible", canonical.Status);
        Assert.NotNull(canonical.GenerationV2);
        Assert.NotEmpty(canonical.Sessions);

        var exerciseIds = canonical.GenerationV2!.Sessions.SelectMany(s => s.Exercises)
            .Select(e => e.ExerciseId).Distinct().ToArray();

        Assert.DoesNotContain(119, exerciseIds);
        Assert.DoesNotContain(125, exerciseIds);
        Assert.DoesNotContain(128, exerciseIds);
        Assert.DoesNotContain(148, exerciseIds);
        Assert.DoesNotContain(159, exerciseIds);
        Assert.DoesNotContain(165, exerciseIds);
        Assert.DoesNotContain(173, exerciseIds);
        Assert.DoesNotContain(177, exerciseIds);
        Assert.DoesNotContain(191, exerciseIds);

        Assert.Contains(exerciseIds, id => id is 129 or 130 or 138);
        Assert.Contains(exerciseIds, id => id is 104 or 108);
        Assert.Contains(155, exerciseIds);
        Assert.All(canonical.GenerationV2.Sessions.SelectMany(s => s.Exercises), ex =>
        {
            Assert.NotEqual(SessionLoadStatusV2.PrescribedLoad.ToString(), ex.LoadStatus);
            Assert.Equal(0m, ex.PrescribedLoadKg ?? 0m);
        });

        Assert.Contains("WeeklyCoverageMissing_UpperPull_Equipment", canonical.GenerationV2.MissingGaps);
        Assert.Contains("WeeklyCoverageMissing_UpperPull_Equipment", canonical.AdvisoryCodes);
    }

    [Fact]
    public void Bodyweight_with_confirmed_sliders_unlocks_slider_leg_curl_without_invented_load()
    {
        var capabilities = new EquipmentCapabilities(HasSliders: true);
        var source = BuildCatalogSource(Equipment.Bodyweight, gluteFocus: false, capabilities: capabilities);
        var request = BuildRequest(WorkoutActivityDto.GeneralStrength, Equipment.Bodyweight, capabilities: capabilities);

        var canonical = GenerationPlanV2Mapper.ForCanonicalUse(
            PreviewStrengthPlanV020UseCase.EvaluateLoadedSource(Guid.NewGuid(), request, source));

        Assert.Equal("Feasible", canonical.Status);
        var sliderCurls = canonical.GenerationV2!.Sessions.SelectMany(s => s.Exercises)
            .Where(e => e.ExerciseId == 119).ToArray();
        Assert.NotEmpty(sliderCurls);
        Assert.All(sliderCurls, curl =>
        {
            Assert.Equal(SessionLoadStatusV2.BodyweightZeroAddedLoad.ToString(), curl.LoadStatus);
            Assert.Equal(0m, curl.PrescribedLoadKg ?? 0m);
        });
    }

    [Fact]
    public void Bands_without_anchor_allow_band_curl_but_block_row_and_chest_press_with_honest_disclosure()
    {
        var capabilities = new EquipmentCapabilities(HasSliders: false, HasConfirmedBandAnchor: false, HasDumbbells: false, HasBands: true);
        var source = BuildCatalogSource(Equipment.HomeDumbbellsBands, gluteFocus: false, capabilities: capabilities);
        var request = BuildRequest(WorkoutActivityDto.GeneralStrength, Equipment.HomeDumbbellsBands, capabilities: capabilities);

        var canonical = GenerationPlanV2Mapper.ForCanonicalUse(
            PreviewStrengthPlanV020UseCase.EvaluateLoadedSource(Guid.NewGuid(), request, source));

        Assert.Equal("Feasible", canonical.Status);
        var exercises = canonical.GenerationV2!.Sessions.SelectMany(s => s.Exercises).ToArray();
        var ids = exercises.Select(e => e.ExerciseId).ToHashSet();

        Assert.DoesNotContain(148, ids);
        Assert.DoesNotContain(159, ids);
        Assert.DoesNotContain(145, ids);
        Assert.Contains(165, ids);
        Assert.Contains("WeeklyCoverageMissing_UpperPull_BandAnchorRequired", canonical.GenerationV2.MissingGaps);

        var bandCurl = exercises.First(e => e.ExerciseId == 165);
        Assert.Null(bandCurl.PrescribedLoadKg);
        Assert.Equal(SessionLoadStatusV2.CalibrationRequired.ToString(), bandCurl.LoadStatus);
    }

    [Fact]
    public void Bands_with_confirmed_anchor_unlock_band_row_and_chest_press_without_invented_kg()
    {
        var capabilities = new EquipmentCapabilities(HasSliders: false, HasConfirmedBandAnchor: true, HasDumbbells: false, HasBands: true);
        var source = BuildCatalogSource(Equipment.HomeDumbbellsBands, gluteFocus: false, capabilities: capabilities);
        var request = BuildRequest(WorkoutActivityDto.GeneralStrength, Equipment.HomeDumbbellsBands, capabilities: capabilities);

        var canonical = GenerationPlanV2Mapper.ForCanonicalUse(
            PreviewStrengthPlanV020UseCase.EvaluateLoadedSource(Guid.NewGuid(), request, source));

        Assert.Equal("Feasible", canonical.Status);
        var exercises = canonical.GenerationV2!.Sessions.SelectMany(s => s.Exercises).ToArray();
        var ids = exercises.Select(e => e.ExerciseId).ToHashSet();

        Assert.Contains(148, ids);
        Assert.Contains(159, ids);
        Assert.DoesNotContain("WeeklyCoverageMissing_UpperPull_Equipment", canonical.GenerationV2.MissingGaps);
        Assert.DoesNotContain("WeeklyCoverageMissing_UpperPull_BandAnchorRequired", canonical.GenerationV2.MissingGaps);

        foreach (var bandEx in exercises.Where(e => e.ExerciseId is 148 or 159 or 165))
        {
            Assert.Null(bandEx.PrescribedLoadKg);
            Assert.Equal(SessionLoadStatusV2.CalibrationRequired.ToString(), bandEx.LoadStatus);
        }
    }

    [Fact]
    public void Home_dumbbells_profile_covers_upper_pull_via_dumbbell_row()
    {
        var capabilities = new EquipmentCapabilities(HasSliders: false, HasConfirmedBandAnchor: false, HasDumbbells: true, HasBands: false);
        var source = BuildCatalogSource(Equipment.HomeDumbbellsBands, gluteFocus: false, capabilities: capabilities);
        var request = BuildRequest(WorkoutActivityDto.GeneralStrength, Equipment.HomeDumbbellsBands, capabilities: capabilities);

        var canonical = GenerationPlanV2Mapper.ForCanonicalUse(
            PreviewStrengthPlanV020UseCase.EvaluateLoadedSource(Guid.NewGuid(), request, source));

        Assert.Equal("Feasible", canonical.Status);
        var ids = canonical.GenerationV2!.Sessions.SelectMany(s => s.Exercises).Select(e => e.ExerciseId).ToHashSet();
        Assert.Contains(145, ids);
        Assert.DoesNotContain(148, ids);
        Assert.DoesNotContain(165, ids);
    }

    [Fact]
    public void Restricted_gym_respects_injury_contraindications_across_gate60_exercises()
    {
        var source = BuildCatalogSource(Equipment.FullGym, gluteFocus: false, injuries: InjuryFlag.Knee | InjuryFlag.Shoulder);
        var request = BuildRequest(WorkoutActivityDto.GeneralStrength, Equipment.FullGym);

        var canonical = GenerationPlanV2Mapper.ForCanonicalUse(
            PreviewStrengthPlanV020UseCase.EvaluateLoadedSource(Guid.NewGuid(), request, source));

        var ids = canonical.GenerationV2!.Sessions.SelectMany(s => s.Exercises).Select(e => e.ExerciseId).ToHashSet();
        Assert.DoesNotContain(129, ids);
        Assert.DoesNotContain(130, ids);
        Assert.DoesNotContain(138, ids);
        Assert.DoesNotContain(119, ids);
        Assert.DoesNotContain(159, ids);
    }

    [Fact]
    public void Glute_focused_bodyweight_program_uses_floor_glute_bridge_without_requiring_hip_thrust_hardware()
    {
        var source = BuildCatalogSource(Equipment.Bodyweight, gluteFocus: true);
        var request = BuildRequest(WorkoutActivityDto.GeneralStrength, Equipment.Bodyweight, gluteFocus: true);

        var preview = GenerationPlanV2Mapper.ForCanonicalUse(
            PreviewStrengthPlanV020UseCase.EvaluateLoadedSource(Guid.NewGuid(), request, source));

        Assert.Equal("Feasible", preview.Status);
        var exercises = preview.GenerationV2!.Sessions.SelectMany(s => s.Exercises).ToArray();
        Assert.Contains(exercises, e => e.ExerciseId is 104 or 108);
        Assert.Contains(exercises, e => e.ExerciseId == 135);
    }

    [Fact]
    public void V2_catalog_mapper_preserves_secondary_contributions_and_rejects_timed_only_core_displacement()
    {
        var source = BuildCatalogSource(Equipment.FullGym, gluteFocus: false);
        var result = WorkoutGenerationV2Service.GenerateFromPreviewSource(
            BuildRequest(WorkoutActivityDto.GeneralStrength, Equipment.FullGym),
            source);

        var exercises = result.AdaptedSessions.SelectMany(s => s.Exercises).ToArray();
        Assert.DoesNotContain(exercises, e => e.ExerciseId is 136 or 173 or 177 or 178);
        Assert.DoesNotContain(exercises, e => e.ExerciseId == 119);
        Assert.DoesNotContain(exercises, e => e.ExerciseId is 148 or 159);
        Assert.True(result.CoverageSummary!.DynamicCoreOccasions >= 2);
        Assert.True(result.CoverageSummary.Muscles.Single(m => m.Muscle == MuscleGroupV2.GluteusMaximus).IndirectSets > 0m);
    }

    [Fact]
    public void Pain_return_for_bodyweight_and_slider_with_zero_load_history_does_not_trap_in_calibration()
    {
        var capabilities = new EquipmentCapabilities(HasSliders: true);
        var source = BuildCatalogSource(Equipment.Bodyweight, gluteFocus: false, capabilities: capabilities) with
        {
            PainRestrictions =
            [
                new PainRestrictionV2(119, null, InjuryFlag.None, new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero),
                    IsResolved: true, ConservativeReturnSessionsCompleted: 0, ReturnBaselineLoadKg: null, RequiresReturnCalibration: true),
                new PainRestrictionV2(129, null, InjuryFlag.None, new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero),
                    IsResolved: true, ConservativeReturnSessionsCompleted: 0, ReturnBaselineLoadKg: null, RequiresReturnCalibration: true)
            ]
        };
        var request = BuildRequest(WorkoutActivityDto.GeneralStrength, Equipment.Bodyweight, capabilities: capabilities);

        var result = WorkoutGenerationV2Service.GenerateFromPreviewSource(request, source);
        var bodyweightExercises = result.AdaptedSessions.SelectMany(s => s.Exercises)
            .Where(e => e.ExerciseId is 119 or 129).ToArray();

        Assert.NotEmpty(bodyweightExercises);
        Assert.All(bodyweightExercises, ex =>
        {
            Assert.Equal(SessionLoadStatusV2.BodyweightZeroAddedLoad, ex.LoadStatus);
            Assert.Equal(0m, ex.PrescribedLoadKg);
        });
    }

    [Fact]
    public void Band_exercise_after_completed_zero_load_sessions_leaves_calibration_without_inventing_kg()
    {
        var bandRowCatalogItem = new ExerciseCatalogItemV2(
            ExerciseId: 148,
            Name: "Resistance Band Row",
            Pattern: MovementPattern.HorizontalPull,
            MuscleContributions:
            [
                ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Back),
                ExerciseMuscleContributionV2.Indirect(MuscleGroupV2.Biceps)
            ],
            RequiredEquipment: new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.ResistanceBand, ExerciseEquipmentKind.BandAnchor },
            SupportedExperiences: new HashSet<TrainingExperienceLevel> { TrainingExperienceLevel.Beginner, TrainingExperienceLevel.Intermediate },
            Contraindications: InjuryFlag.LowBack,
            IsCompound: true,
            IsUnilateral: false,
            IsBodyweightOnly: false,
            SetupMinutes: 0.75m,
            SecondsPerRep: 4,
            MinimumLoadIncrementKg: null);

        var session1 = Guid.NewGuid();
        var session2 = Guid.NewGuid();
        var history = new ObservedSetRecordV2[]
        {
            new(session1, 148, ReferenceMonday.AddDays(-14), new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero), 1, SetKindV2.Working, 15, 0m, 2, true),
            new(session1, 148, ReferenceMonday.AddDays(-14), new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero), 2, SetKindV2.Working, 15, 0m, 2, true),
            new(session2, 148, ReferenceMonday.AddDays(-7), new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero), 1, SetKindV2.Working, 15, 0m, 2, true),
            new(session2, 148, ReferenceMonday.AddDays(-7), new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero), 2, SetKindV2.Working, 15, 0m, 2, true)
        };

        var decision = WorkoutProgressionAndRecoveryV2.EvaluateStrengthProgression(
            bandRowCatalogItem, prescribedSets: 2, minReps: 10, maxReps: 15, targetRir: 2, history: history);

        Assert.Equal(ProgressionDecisionKindV2.Maintain, decision.Decision);
        Assert.Null(decision.RecommendedLoadKg);
    }

    [Fact]
    public void Service_empty_request_sessions_map_falls_back_to_saved_sessions_per_week()
    {
        var source = BuildCatalogSource(Equipment.FullGym, gluteFocus: false) with
        {
            AvailableWeekdays = new HashSet<DayOfWeek>
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
            },
            RequestedSessionsPerWeek = new Dictionary<int, int>
            {
                [(int)WorkoutActivityDto.GeneralStrength] = 2
            }
        };
        var request = BuildRequest(WorkoutActivityDto.GeneralStrength, Equipment.FullGym) with
        {
            DraftPreferences = new WorkoutPreferencesDto(
                [(int)WorkoutActivityDto.GeneralStrength],
                [(int)DayOfWeek.Monday, (int)DayOfWeek.Tuesday, (int)DayOfWeek.Wednesday, (int)DayOfWeek.Thursday, (int)DayOfWeek.Friday],
                new Dictionary<int, int> { [(int)WorkoutActivityDto.GeneralStrength] = 60 },
                Equipment.FullGym.ToString()),
            AvailableWeekdays = new HashSet<DayOfWeek>
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
            },
            RequestedSessionsPerWeek = new Dictionary<int, int>()
        };

        var result = WorkoutGenerationV2Service.GenerateFromPreviewSource(request, source);
        Assert.Equal(2, result.AdaptedSessions.Count);
    }

    private static StrengthPlanPreviewRequestV020 BuildRequest(
        WorkoutActivityDto activity,
        Equipment equipment,
        bool gluteFocus = false,
        EquipmentCapabilities? capabilities = null) =>
        new(activity, ReferenceMonday, new ActivityTrainingHistoryInputV020("strength", 0, 0m, null, true))
        {
            DraftPreferences = new WorkoutPreferencesDto(
                [(int)activity],
                [(int)DayOfWeek.Monday, (int)DayOfWeek.Wednesday, (int)DayOfWeek.Friday],
                new Dictionary<int, int> { [(int)activity] = 60 },
                equipment.ToString(),
                GluteFocus: gluteFocus,
                HasSliders: capabilities?.HasSliders,
                HasConfirmedBandAnchor: capabilities?.HasConfirmedBandAnchor,
                HasDumbbells: capabilities?.HasDumbbells,
                HasBands: capabilities?.HasBands),
            SelectedActivityTypes = new HashSet<int> { (int)activity },
            AvailableWeekdays = new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
            ActivitySessionDurationsMinutes = new Dictionary<int, int> { [(int)activity] = 60 },
            WarmupMinutes = 10,
            CooldownMinutes = 5
        };

    private static StrengthPlanPreviewSourceV020 BuildCatalogSource(
        Equipment equipment,
        bool gluteFocus,
        EquipmentCapabilities? capabilities = null,
        InjuryFlag injuries = InjuryFlag.None)
    {
        var defs = CatalogAuditFixture.Definitions;
        var contribs = CatalogAuditFixture.Contributions;
        var sprById = defs.Values.Where(d => d.SecondsPerRep > 0).ToDictionary(d => d.Id, d => d.SecondsPerRep);
        var primaryById = contribs.ToDictionary(
            p => p.Key,
            p => p.Value.OrderByDescending(r => r.Fraction).ThenBy(r => r.MuscleGroupId).First().MuscleGroup);
        var groupsById = contribs.ToDictionary(
            p => p.Key,
            p => (IReadOnlySet<string>)p.Value.Select(r => r.MuscleGroup).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var slotMap = new Dictionary<string, IReadOnlySet<int>>(StringComparer.Ordinal);
        var slotConditions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var slot in ReviewedCatalogFixture.Root.GetProperty("templateSlots").EnumerateArray())
        {
            var templateId = slot.GetProperty("templateIdAndVersion").GetString()!.Split(" / ")[0];
            var slotId = slot.GetProperty("movementSlot").GetString()!;
            var key = $"{templateId}|{slotId}";
            slotMap[key] = slot.GetProperty("candidateSourceExerciseIds").EnumerateArray()
                .Select(v => v.GetInt32()).ToHashSet();
            slotConditions[key] = slot.GetProperty("selectionConstraints").GetString() ?? string.Empty;
        }

        return new StrengthPlanPreviewSourceV020(
            UserGoal.MaintainHealth,
            equipment,
            injuries,
            false,
            ReviewedCatalogFixture.Catalog,
            new Dictionary<int, int>(),
            new HashSet<int>(),
            new Dictionary<int, int>(),
            new HashSet<int>(),
            sprById,
            primaryById,
            slotMap,
            slotConditions)
        {
            BirthDate = new DateOnly(1996, 5, 1),
            GluteFocus = gluteFocus,
            ProfileTrainingExperience = TrainingExperienceLevel.Beginner,
            SelectedActivityTypes = new HashSet<int> { (int)WorkoutActivityDto.GeneralStrength },
            AvailableWeekdays = new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
            ActivitySessionDurationsMinutes = new Dictionary<int, int> { [(int)WorkoutActivityDto.GeneralStrength] = 60 },
            MuscleGroupsByExerciseId = groupsById,
            MuscleContributionsByExerciseId = contribs,
            DraftEquipmentCapabilities = capabilities
        };
    }
}
