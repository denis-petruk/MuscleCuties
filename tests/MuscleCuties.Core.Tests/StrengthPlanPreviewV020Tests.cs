using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.V2;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class StrengthPlanPreviewV020Tests
{
    [Fact]
    public async Task V2_preview_transports_the_full_prescription_for_canonical_acceptance_projection()
    {
        var source = Profile();
        var request = Request();
        var preview = await new PreviewStrengthPlanV020UseCase(new Source(source)).ExecuteAsync(Guid.NewGuid(), request);
        var expected = WorkoutGenerationV2Service.GenerateFromPreviewSource(request, source);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(preview.GenerationV2,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.True(json.TryGetProperty("sessions", out var sessions));
        Assert.Equal(expected.AdaptedSessions.Count, sessions.GetArrayLength());
        Assert.Equal(expected.AdaptedSessions.Where(session => session.Exercises.Count > 0)
                .SelectMany(session => session.Exercises).Select(exercise => exercise.ExerciseId),
            GenerationPlanV2Mapper.ToStrengthSessions(preview.GenerationV2!).SelectMany(session => session.Slots)
                .Select(slot => slot.ExerciseId!.Value));
        foreach (var session in sessions.EnumerateArray())
        foreach (var exercise in session.GetProperty("exercises").EnumerateArray())
        {
            Assert.True(exercise.TryGetProperty("warmUpSets", out _));
            Assert.True(exercise.TryGetProperty("supersetGroupId", out _));
            Assert.True(exercise.TryGetProperty("isUnilateral", out _));
            Assert.True(exercise.TryGetProperty("loadStatus", out _));
            Assert.True(exercise.TryGetProperty("prescribedLoadKg", out _));
        }
    }

    [Theory]
    [InlineData(20, false, "Intermediate", 4)]
    [InlineData(130, false, "Advanced", 5)]
    [InlineData(130, true, "Advanced", 2)]
    public void V2_adapter_uses_durable_day_promotion_and_detraining(int days, bool returning,
        string expectedTier, int expectedSessions)
    {
        var source = Profile() with
        {
            ProfileTrainingExperience = TrainingExperienceLevel.Beginner,
            Catalog = Profile().Catalog.Select(entry => entry with { Candidate = entry.Candidate with
                { SupportedExperiences = new HashSet<ActivityExperience> { ActivityExperience.Beginner, ActivityExperience.Established } } }).ToArray(),
            DistinctCompletedWorkoutDays = days,
            IsReturningAfterDetraining = returning,
            RequestedSessionsPerWeek = null
        };
        var request = Request() with { AvailableWeekdays = Enum.GetValues<DayOfWeek>().ToHashSet(), ActivityHistory = null };
        var result = WorkoutGenerationV2Service.GenerateFromPreviewSource(request, source);
        Assert.Contains(result.RuleTrace, entry => entry.StartsWith($"Experience:{expectedTier};", StringComparison.Ordinal));
        Assert.Equal(expectedSessions, result.AdaptedSessions.Count(session => session.Exercises.Count > 0));
    }

    private const int OwnerMaximumActivitiesPerDay = 2;
    private const int HistoryConsistentWeeks = 1;
    private const int HistoryAverageSessions = 1;
    private const int PreviousSessionDays = 1;
    private const int NoWarmupOrCooldownMinutes = 0;
    private const int SelectedDemandingYogaDurationMinutes = 35;
    private const int SelectedFallbackStrengthMinutes = 60;
    private const int StaleGeneralStrengthMinutes = 90;
    private const int ExpectedSavedProposalCount = 1;
    private const string MixedScheduleIncompleteReason = "MixedActivityScheduleIncomplete";
    private static readonly DateOnly LocalDate = new(2026, 10, 23);

    [Fact]
    public async Task Server_population_limitation_blocks_manual_and_automatic_previews()
    {
        var source = Profile() with { PopulationLimitationCode = WorkoutPopulationScopeRule.BirthDateRequired };
        var manual = await new PreviewStrengthPlanV020UseCase(new Source(source)).ExecuteAsync(Guid.NewGuid(), Request());
        var automatic = PreviewStrengthPlanV020UseCase.EvaluateAutomaticLoadedSource(Guid.NewGuid(),
            Request(), source, WorkoutActivityDto.GeneralStrength);
        Assert.Equal("Limited", manual.Status);
        Assert.Equal([WorkoutPopulationScopeRule.BirthDateRequired], manual.LimitationCodes);
        Assert.Empty(manual.Sessions);
        Assert.Equal("Limited", automatic.Status);
        Assert.Empty(automatic.Sessions);
    }

    [Fact]
    public async Task Reviewed_Required_Slots_Produce_Deterministic_Policy_Preview()
    {
        var source = new Source(Profile());
        var useCase = new PreviewStrengthPlanV020UseCase(source);
        var request = Request();

        var first = await useCase.ExecuteAsync(Guid.NewGuid(), request);
        var second = await useCase.ExecuteAsync(Guid.NewGuid(), request);

        Assert.Equal("Feasible", first.Status);
        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek, first.Sessions.Count);
        Assert.Equal([101, 102, 103, 104], first.Sessions[0].Slots
            .Where(slot => slot.IsRequired).Select(slot => slot.ExerciseId));
        Assert.Equal(first.Sessions[0].Slots.Select(slot => slot.ExerciseId),
            second.Sessions[0].Slots.Select(slot => slot.ExerciseId));
        Assert.True(first.Sessions[0].EstimatedMinutes > 0);
        Assert.DoesNotContain("OneDayFullBodyHasLimitedWeeklyCoverage", first.AdvisoryCodes);
        Assert.NotNull(first.MixedActivitySchedule);
        Assert.Equal(first.MixedActivitySchedule!.Sessions
                .Where(session => session.ActivityTypeId == (int)WorkoutActivityType.GeneralStrength)
                .Select(session => (DayOfWeek?)session.Weekday)
                .Take(first.Sessions.Count)
                .ToArray(),
            first.Sessions.Select(session => session.PlannedWeekday).ToArray());
    }

    [Fact]
    public async Task Preview_reports_missing_knee_flexion_from_exercises_actually_scheduled()
    {
        var preview = await new PreviewStrengthPlanV020UseCase(new Source(OneDayProfile()))
            .ExecuteAsync(Guid.NewGuid(), Request() with { AvailableWeekdays = null });

        Assert.Equal("Feasible", preview.Status);
        Assert.Contains("WeeklyMovementCoverageMissing_KneeFlexion", preview.AdvisoryCodes);
        Assert.DoesNotContain("WeeklyMovementCoverageMissing_KneeExtension", preview.AdvisoryCodes);
        Assert.DoesNotContain("WeeklyMovementCoverageMissing_HipExtension", preview.AdvisoryCodes);
    }

    [Fact]
    public async Task Preview_reports_a_knee_extension_gap_when_all_safe_knee_exercises_are_excluded()
    {
        var source = OneDayProfile() with { ExcludedExerciseIds = new HashSet<int> { 101 } };
        var preview = await new PreviewStrengthPlanV020UseCase(new Source(source))
            .ExecuteAsync(Guid.NewGuid(), Request() with { AvailableWeekdays = null });

        Assert.NotEmpty(preview.Sessions);
        Assert.Contains("WeeklyMovementCoverageMissing_KneeExtension", preview.AdvisoryCodes);
        Assert.Contains("WeeklyMovementCoverageMissing_KneeFlexion", preview.AdvisoryCodes);
        Assert.DoesNotContain("WeeklyMovementCoverageMissing_HipExtension", preview.AdvisoryCodes);
    }

    [Fact]
    public async Task Duration_omitted_knee_flexion_does_not_hide_weekly_pattern_gap()
    {
        var source = OneDayProfile();
        var accessoryKey = "MC-FB-1D-A|goal-specific-accessory";
        var allowed = source.ReviewedAllowedExerciseIdsByTemplateSlot.ToDictionary(x => x.Key, x => x.Value);
        var conditions = source.TemplateSlotReviewConditions.ToDictionary(x => x.Key, x => x.Value);
        allowed[accessoryKey] = new HashSet<int> { 105 };
        conditions[accessoryKey] = "Reviewed synthetic slot";
        var catalog = source.Catalog.Concat([Entry(105, MovementPattern.KneeFlexion)]).ToArray();
        var seconds = source.SecondsPerRepByExerciseId.ToDictionary(x => x.Key, x => x.Value);
        seconds[105] = 4;
        var primary = source.PrimaryMuscleByExerciseId.ToDictionary(x => x.Key, x => x.Value);
        primary[105] = "Hamstrings";
        var muscles = source.MuscleGroupsByExerciseId!.ToDictionary(x => x.Key, x => x.Value);
        muscles[105] = new HashSet<string> { "Hamstrings" };
        var durations = new Dictionary<int, int> { [(int)WorkoutActivityType.GeneralStrength] = 60 };
        var reviewed = source with { Catalog = catalog, ReviewedAllowedExerciseIdsByTemplateSlot = allowed,
            TemplateSlotReviewConditions = conditions, SecondsPerRepByExerciseId = seconds,
            PrimaryMuscleByExerciseId = primary, MuscleGroupsByExerciseId = muscles,
            ActivitySessionDurationsMinutes = durations };
        var shortEnough = await new PreviewStrengthPlanV020UseCase(new Source(reviewed))
            .ExecuteAsync(Guid.NewGuid(), Request() with { AvailableWeekdays = null });
        Assert.Contains(shortEnough.Sessions.SelectMany(session => session.Slots), slot => slot.ExerciseId == 105);
        Assert.DoesNotContain(WeeklyMovementCoverageRule.MissingKneeFlexion, shortEnough.AdvisoryCodes);

        seconds = seconds.ToDictionary(x => x.Key, x => x.Value);
        seconds[105] = 600;
        var tooSlow = await new PreviewStrengthPlanV020UseCase(new Source(reviewed with { SecondsPerRepByExerciseId = seconds }))
            .ExecuteAsync(Guid.NewGuid(), Request() with { AvailableWeekdays = null });
        Assert.DoesNotContain(tooSlow.Sessions.SelectMany(session => session.Slots), slot => slot.ExerciseId == 105);
        Assert.Contains(WeeklyMovementCoverageRule.MissingKneeFlexion, tooSlow.AdvisoryCodes);
    }

    [Fact]
    public void Movement_coverage_counts_reviewed_knee_flexion_and_does_not_guess_missing_catalog_metadata()
    {
        var catalog = new[] { Entry(101, MovementPattern.Lunge), Entry(102, MovementPattern.HipThrust),
            Entry(105, MovementPattern.KneeFlexion) };
        Assert.Empty(WeeklyMovementCoverageRule.MissingPatterns([101, 102, 105], catalog));
        Assert.Equal([WeeklyMovementCoverageRule.UnknownCoverage],
            WeeklyMovementCoverageRule.MissingPatterns([101, 999], catalog));
    }

    [Fact]
    public void One_available_day_is_valid_in_saved_preferences()
    {
        Assert.True(WorkoutPreferencesRule.HasMinimumDistinctWeekdays(new[] { DayOfWeek.Monday }));
        Assert.False(WorkoutPreferencesRule.HasMinimumDistinctWeekdays(Array.Empty<DayOfWeek>()));
    }

    [Fact]
    public void One_day_planning_uses_a_new_ruleset_version_for_snapshot_staleness()
    {
        Assert.NotEqual("MC-PLANNING-0.3", StrengthPlanPreviewRule.RulesetVersion);
    }

    [Fact]
    public async Task One_available_day_creates_a_safe_full_body_proposal_with_coverage_disclosure()
    {
        var source = OneDayProfile();
        var store = new ProposalStore();
        var useCase = new CreateStrengthPlanProposalV020UseCase(new PreviewStrengthPlanV020UseCase(new Source(source)), store);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request() with { AvailableWeekdays = null });

        Assert.True(result.IsPersisted);
        Assert.Equal("Feasible", result.Preview.Status);
        var session = Assert.Single(result.Preview.Sessions);
        Assert.Equal(DayOfWeek.Monday, session.PlannedWeekday);
        Assert.All(session.Slots.Where(slot => slot.IsRequired), slot => Assert.NotNull(slot.ExerciseId));
        Assert.Contains("OneDayFullBodyHasLimitedWeeklyCoverage", result.Preview.AdvisoryCodes);
    }

    [Fact]
    public async Task One_available_day_retains_the_selected_high_volume_style()
    {
        var source = OneDayProfile() with { SelectedActivityTypes = new HashSet<int> { (int)WorkoutActivityType.HighVolumeStrength } };
        var request = Request() with { Activity = WorkoutActivityDto.HighVolumeStrength,
            ExplicitStrengthStyleChoice = WorkoutActivityDto.HighVolumeStrength, AvailableWeekdays = null };
        var result = await new PreviewStrengthPlanV020UseCase(new Source(source)).ExecuteAsync(Guid.NewGuid(), request);

        Assert.Equal("Feasible", result.Status);
        Assert.Single(result.Sessions);
        Assert.Equal((int)WorkoutActivityType.HighVolumeStrength, result.EffectiveActivityTypeId);
        Assert.Contains("OneDayFullBodyHasLimitedWeeklyCoverage", result.AdvisoryCodes);
    }

    [Fact]
    public async Task One_day_with_no_allowed_strength_exercises_is_not_persisted()
    {
        var source = OneDayProfile() with { ExcludedExerciseIds = new HashSet<int> { 101, 102, 103, 104 } };
        var store = new ProposalStore();
        var useCase = new CreateStrengthPlanProposalV020UseCase(new PreviewStrengthPlanV020UseCase(new Source(source)), store);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request() with { AvailableWeekdays = null });

        Assert.False(result.IsPersisted);
        Assert.NotEqual("Feasible", result.Preview.Status);
        Assert.Empty(result.Preview.Sessions);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void Automatic_Preview_Rejects_An_Unselected_Current_Strength_Style()
    {
        var request = Request() with
        {
            SelectedActivityTypes = [(int)WorkoutActivityType.GeneralStrength]
        };
        var result = PreviewStrengthPlanV020UseCase.EvaluateAutomaticLoadedSource(Guid.NewGuid(),
            request, Profile(), WorkoutActivityDto.HighVolumeStrength);

        Assert.NotEqual("Feasible", result.Status);
        Assert.Empty(result.Sessions);
    }

    [Fact]
    public void Automatic_Preview_Retains_Selected_Current_Style_Without_Asking_For_Choice()
    {
        var style = WorkoutActivityDto.GeneralStrength;
        var selected = new HashSet<int>
        {
            (int)WorkoutActivityType.GeneralStrength,
            (int)WorkoutActivityType.HighVolumeStrength
        };
        var request = Request() with { SelectedActivityTypes = selected };
        var source = Profile() with { SelectedActivityTypes = selected };

        var result = PreviewStrengthPlanV020UseCase.EvaluateAutomaticLoadedSource(Guid.NewGuid(),
            request, source, style);

        Assert.Equal("Feasible", result.Status);
        Assert.Equal((int)WorkoutActivityType.GeneralStrength, result.EffectiveActivityTypeId);
        Assert.False(result.StrengthStyleChoiceRequired);
    }

    [Fact]
    public void Automatic_Preview_Rejects_A_NonStrength_Retained_Style()
    {
        var result = PreviewStrengthPlanV020UseCase.EvaluateAutomaticLoadedSource(Guid.NewGuid(),
            Request(), Profile(), WorkoutActivityDto.Yoga);

        Assert.NotEqual("Feasible", result.Status);
        Assert.Empty(result.Sessions);
    }

    [Fact]
    public async Task Preview_Does_Not_Reinsert_A_Strength_Activity_The_User_Removed()
    {
        var request = Request() with { SelectedActivityTypes = [] };
        var result = await new PreviewStrengthPlanV020UseCase(new Source(Profile()))
            .ExecuteAsync(Guid.NewGuid(), request);

        Assert.NotEqual("Feasible", result.Status);
        Assert.Empty(result.Sessions);
    }

    [Fact]
    public async Task Preview_Does_Not_Replace_Explicitly_Empty_Availability_With_Saved_Days()
    {
        var request = Request() with { AvailableWeekdays = [] };
        var result = await new PreviewStrengthPlanV020UseCase(new Source(Profile()))
            .ExecuteAsync(Guid.NewGuid(), request);

        Assert.NotEqual("Feasible", result.Status);
        Assert.Empty(result.Sessions);
    }

    [Fact]
    public async Task Preview_Uses_The_Activity_Choices_And_Weekdays_Sent_By_The_User()
    {
        var userActivities = new HashSet<int>
        {
            (int)WorkoutActivityType.GeneralStrength,
            (int)WorkoutActivityType.Yoga
        };
        var userWeekdays = new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Thursday };
        var userStrengthDuration = WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes +
                                   WorkoutPreferencesRule.ActivitySessionDurationIncrementMinutes;
        var userDurations = new Dictionary<int, int>
        {
            [(int)WorkoutActivityType.GeneralStrength] = userStrengthDuration,
            [(int)WorkoutActivityType.Yoga] = WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes
        };
        var userFrequencies = new Dictionary<int, int>
        {
            [(int)WorkoutActivityType.Yoga] = userWeekdays.Count
        };
        var request = Request() with
        {
            SelectedActivityTypes = userActivities,
            AvailableWeekdays = userWeekdays,
            ActivitySessionDurationsMinutes = userDurations,
            RequestedSessionsPerWeek = userFrequencies,
            ActivityEligibilityEvidence =
            [
                new ActivityEligibilityInputV020(WorkoutActivityDto.Yoga,
                    IsDemandingYogaContent: false)
            ]
        };
        var staleProfile = Profile() with
        {
            RequestedSessionsPerWeek = new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.Yoga] = ActivityPreferenceDefaults.OptionalSessionsPerWeek
            }
        };
        var result = await new PreviewStrengthPlanV020UseCase(new Source(staleProfile))
            .ExecuteAsync(Guid.NewGuid(), request);

        Assert.Equal("Feasible", result.Status);
        var schedule = result.MixedActivitySchedule!;
        Assert.Equal(userActivities.OrderBy(activity => activity), schedule.Activities
            .Select(decision => decision.ActivityTypeId).OrderBy(activity => activity));
        Assert.All(schedule.Sessions, session => Assert.Contains((DayOfWeek)session.Weekday, userWeekdays));
        Assert.All(schedule.Sessions.Where(session =>
                session.ActivityTypeId == (int)WorkoutActivityType.GeneralStrength),
            session => Assert.Equal((decimal)userStrengthDuration, session.DurationMinutes));
        var yogaDecision = Assert.Single(schedule.Activities,
            decision => decision.ActivityTypeId == (int)WorkoutActivityType.Yoga);
        Assert.Equal(userWeekdays.Count, yogaDecision.TargetSessionsPerWeek);
        Assert.Contains(schedule.Sessions, session => session.ActivityTypeId == (int)WorkoutActivityType.Yoga);
    }

    [Fact]
    public void Request_Can_Be_Serialized_And_Deserialized_Via_SystemTextJson()
    {
        var request = Request() with
        {
            SelectedActivityTypes = new HashSet<int>
            {
                (int)WorkoutActivityType.GeneralStrength,
                (int)WorkoutActivityType.Yoga
            },
            ActivitySessionDurationsMinutes = new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.GeneralStrength] =
                    WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes
            },
            RequestedSessionsPerWeek = new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.Yoga] = ActivityPreferenceDefaults.OptionalSessionsPerWeek
            }
        };
        var json = System.Text.Json.JsonSerializer.Serialize(request, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<StrengthPlanPreviewRequestV020>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(deserialized);
        Assert.Equal(request.Activity, deserialized.Activity);
        Assert.Equal(request.AvailableWeekdays, deserialized.AvailableWeekdays);
        Assert.True(request.SelectedActivityTypes!.SetEquals(deserialized.SelectedActivityTypes!));
        Assert.Equal(request.ActivitySessionDurationsMinutes![(int)WorkoutActivityType.GeneralStrength],
            deserialized.ActivitySessionDurationsMinutes![(int)WorkoutActivityType.GeneralStrength]);
        Assert.Equal(request.RequestedSessionsPerWeek![(int)WorkoutActivityType.Yoga],
            deserialized.RequestedSessionsPerWeek![(int)WorkoutActivityType.Yoga]);
    }

    [Fact]
    public async Task Missing_Flexible_Activity_Preferences_Use_Defaults_And_Keep_Strength_Preview()
    {
        var profile = Profile();
        var activities = profile.SelectedActivityTypes!.Append((int)WorkoutActivityType.Yoga).ToHashSet();
        var durations = profile.ActivitySessionDurationsMinutes!.ToDictionary(pair => pair.Key, pair => pair.Value);
        var source = new Source(profile with
        {
            SelectedActivityTypes = activities,
            ActivitySessionDurationsMinutes = durations
        });
        var request = Request() with
        {
            ActivityEligibilityEvidence =
            [
                new ActivityEligibilityInputV020(WorkoutActivityDto.Yoga,
                    IsDemandingYogaContent: false)
            ]
        };

        var result = await new PreviewStrengthPlanV020UseCase(source).ExecuteAsync(Guid.NewGuid(), request);

        Assert.Equal("Feasible", result.Status);
        Assert.DoesNotContain(MixedScheduleIncompleteReason, result.LimitationCodes);
        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek, result.Sessions.Count);
        var yogaDecision = Assert.Single(result.MixedActivitySchedule!.Activities,
            decision => decision.ActivityTypeId == (int)WorkoutActivityType.Yoga);
        Assert.Equal("Scheduled", yogaDecision.Disposition);
        Assert.NotNull(yogaDecision.PolicySessionsPerWeek);
        Assert.NotNull(yogaDecision.TargetSessionsPerWeek);
        Assert.Equal(yogaDecision.TargetSessionsPerWeek, yogaDecision.ScheduledSessionsPerWeek);
    }

    [Fact]
    public async Task PlanSpecificYogaDurationOverridesAnIncompatibleSavedValueAndPreservesStrengthLimit()
    {
        var profile = Profile();
        var selectedActivities = profile.SelectedActivityTypes!
            .Append((int)WorkoutActivityType.Yoga).ToHashSet();
        var savedDurations = profile.ActivitySessionDurationsMinutes!
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        savedDurations[(int)WorkoutActivityType.Yoga] = ActivitySessionDurationRule.GentleYogaDefaultMinutes;
        var source = new Source(profile with
        {
            SelectedActivityTypes = selectedActivities,
            ActivitySessionDurationsMinutes = savedDurations
        });
        var request = Request() with
        {
            ActivityEligibilityEvidence =
            [
                new ActivityEligibilityInputV020(WorkoutActivityDto.Yoga,
                    IsDemandingYogaContent: true,
                    YogaSessionDurationMinutes: SelectedDemandingYogaDurationMinutes)
            ]
        };

        var result = await new PreviewStrengthPlanV020UseCase(source).ExecuteAsync(Guid.NewGuid(), request);

        Assert.Equal("Feasible", result.Status);
        var yogaDecision = Assert.Single(result.MixedActivitySchedule!.Activities,
            decision => decision.ActivityTypeId == (int)WorkoutActivityType.Yoga);
        Assert.Equal((decimal)SelectedDemandingYogaDurationMinutes, yogaDecision.OriginalPrescription!.MainMinutes);
        Assert.All(result.MixedActivitySchedule.Sessions.Where(session => session.ActivityTypeId == (int)WorkoutActivityType.Yoga),
            session => Assert.Null(session.OccurrencePrescription));
        Assert.All(result.MixedActivitySchedule.Sessions.Where(session =>
                session.ActivityTypeId == (int)WorkoutActivityType.GeneralStrength),
            session => Assert.Equal(
                (decimal)profile.ActivitySessionDurationsMinutes![(int)WorkoutActivityType.GeneralStrength],
                session.DurationMinutes));
    }

    [Fact]
    public async Task Mixed_Plan_Reduces_Optional_Frequency_But_Keeps_Required_Strength_Sessions()
    {
        var profile = Profile();
        var selected = profile.SelectedActivityTypes!
            .Concat([(int)WorkoutActivityType.Cycling, (int)WorkoutActivityType.Yoga])
            .ToHashSet();
        var fourDays = new HashSet<DayOfWeek>
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday
        };
        var request = Request() with
        {
            ActivityEligibilityEvidence =
            [
                new ActivityEligibilityInputV020(WorkoutActivityDto.Yoga,
                    IsDemandingYogaContent: false)
            ]
        };
        var store = new ProposalStore();
        var ready = new CreateStrengthPlanProposalV020UseCase(
            new PreviewStrengthPlanV020UseCase(new Source(profile with
            {
                SelectedActivityTypes = selected,
                AvailableWeekdays = fourDays
            })), store);

        var proposal = await ready.ExecuteAsync(Guid.NewGuid(), request);

        Assert.True(proposal.IsPersisted);
        Assert.Equal("Feasible", proposal.Preview.Status);
        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek,
            proposal.Preview.MixedActivitySchedule!.Sessions.Count(session =>
                session.ActivityTypeId == (int)WorkoutActivityType.GeneralStrength));
        Assert.Contains(proposal.Preview.MixedActivitySchedule.Sessions, session =>
            session.ActivityTypeId == (int)WorkoutActivityType.Cycling);
        Assert.Contains(proposal.Preview.MixedActivitySchedule.Sessions, session =>
            session.ActivityTypeId == (int)WorkoutActivityType.Yoga);
        Assert.Equal(ExpectedSavedProposalCount, store.SaveCount);

        var oneDay = new CreateStrengthPlanProposalV020UseCase(
            new PreviewStrengthPlanV020UseCase(new Source(profile with
            {
                SelectedActivityTypes = selected,
                AvailableWeekdays = new HashSet<DayOfWeek>
                {
                    DayOfWeek.Monday
                }
            })), store);
        var oneDayProposal = await oneDay.ExecuteAsync(Guid.NewGuid(), request with { AvailableWeekdays = null });

        Assert.False(oneDayProposal.IsPersisted);
        Assert.NotEqual("Feasible", oneDayProposal.Preview.Status);
        Assert.Empty(oneDayProposal.Preview.Sessions);
        Assert.Equal(ExpectedSavedProposalCount, store.SaveCount);
    }

    [Fact]
    public async Task Preview_And_Proposal_Allow_One_Gentle_Companion_Per_Strength_Day()
    {
        var twoDays = new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Thursday };
        var profile = AddSelectedActivity(Profile(), WorkoutActivityType.Cycling, WorkoutActivityType.Yoga) with
        {
            AvailableWeekdays = twoDays
        };
        var request = Request() with
        {
            ActivityEligibilityEvidence =
            [
                new ActivityEligibilityInputV020(WorkoutActivityDto.Yoga,
                    IsDemandingYogaContent: false)
            ]
        };
        var store = new ProposalStore();
        var result = await new CreateStrengthPlanProposalV020UseCase(
                new PreviewStrengthPlanV020UseCase(new Source(profile)), store)
            .ExecuteAsync(Guid.NewGuid(), request);

        Assert.True(result.IsPersisted);
        Assert.Equal("Feasible", result.Preview.Status);
        var schedule = result.Preview.MixedActivitySchedule!;
        Assert.Contains(schedule.Sessions.GroupBy(session => session.Weekday), day => day.Count() > 1);
        Assert.All(schedule.Sessions.GroupBy(session => session.Weekday), day =>
        {
            Assert.InRange(day.Count(), 1,
                OwnerMaximumActivitiesPerDay);
            if (day.Count() > 1)
            {
                var sessions = day.ToArray();
                Assert.NotEqual(sessions[0].ActivityTypeId, sessions[1].ActivityTypeId);
                if (sessions.Any(session => session.ActivityTypeId == (int)WorkoutActivityType.GeneralStrength))
                    Assert.Equal((int)WorkoutActivityType.GeneralStrength, sessions.OrderBy(session => session.DayOrder).First().ActivityTypeId);
            }
        });
        Assert.Equal(ExpectedSavedProposalCount, store.SaveCount);
    }

    [Fact]
    public async Task Mobility_Can_Share_Strength_Day_Without_Blocking_The_Plan()
    {
        var twoDays = new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Thursday };
        var profile = AddSelectedActivity(Profile(), WorkoutActivityType.Mobility) with
        {
            AvailableWeekdays = twoDays
        };
        var store = new ProposalStore();
        var result = await new CreateStrengthPlanProposalV020UseCase(
                new PreviewStrengthPlanV020UseCase(new Source(profile)), store)
            .ExecuteAsync(Guid.NewGuid(), Request() with { AvailableWeekdays = twoDays });

        Assert.True(result.IsPersisted);
        Assert.Equal("Feasible", result.Preview.Status);
        Assert.DoesNotContain(MixedScheduleIncompleteReason, result.Preview.LimitationCodes);
        var schedule = result.Preview.MixedActivitySchedule!;
        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek,
            schedule.Sessions.Count(session => session.ActivityTypeId == (int)WorkoutActivityType.GeneralStrength));
        Assert.Contains(schedule.Sessions,
            session => session.ActivityTypeId == (int)WorkoutActivityType.Mobility);
        Assert.Contains(schedule.Activities, decision =>
            decision.ActivityTypeId == (int)WorkoutActivityType.Mobility &&
            decision.ScheduledSessionsPerWeek > 0);
        Assert.Equal(ExpectedSavedProposalCount, store.SaveCount);
    }

    [Fact]
    public async Task Missing_Demanding_Yoga_Duration_Uses_Twenty_Minutes_After_Classification()
    {
        var profile = Profile();
        var selected = profile.SelectedActivityTypes!.Append((int)WorkoutActivityType.Yoga).ToHashSet();
        var source = new Source(profile with { SelectedActivityTypes = selected });
        var request = Request() with
        {
            ActivityEligibilityEvidence =
            [
                new ActivityEligibilityInputV020(WorkoutActivityDto.Yoga,
                    IsDemandingYogaContent: true)
            ]
        };

        var result = await new PreviewStrengthPlanV020UseCase(source).ExecuteAsync(Guid.NewGuid(), request);

        var yoga = Assert.Single(result.MixedActivitySchedule!.Activities,
            decision => decision.ActivityTypeId == (int)WorkoutActivityType.Yoga);
        Assert.Equal(ActivitySessionDurationRule.DemandingYogaDurationMinutesDefault, yoga.OriginalPrescription!.MainMinutes);
        Assert.All(result.MixedActivitySchedule.Sessions.Where(session => session.ActivityTypeId == (int)WorkoutActivityType.Yoga),
            session =>
            {
                Assert.True(session.IsHardSession);
                Assert.Null(session.OccurrencePrescription);
            });
    }

    [Fact]
    public async Task Saved_Gentle_Yoga_Duration_Does_Not_Block_Demanding_Yoga_Plan()
    {
        var profile = Profile();
        var selected = profile.SelectedActivityTypes!.Append((int)WorkoutActivityType.Yoga).ToHashSet();
        var durations = profile.ActivitySessionDurationsMinutes!.ToDictionary(pair => pair.Key, pair => pair.Value);
        durations[(int)WorkoutActivityType.Yoga] = ActivitySessionDurationRule.YogaDurationMinutesDefault;
        var source = new Source(profile with
        {
            SelectedActivityTypes = selected,
            ActivitySessionDurationsMinutes = durations
        });
        var request = Request() with
        {
            ActivityEligibilityEvidence =
            [
                new ActivityEligibilityInputV020(WorkoutActivityDto.Yoga,
                    IsDemandingYogaContent: true)
            ]
        };

        var result = await new PreviewStrengthPlanV020UseCase(source).ExecuteAsync(Guid.NewGuid(), request);

        Assert.Equal("Feasible", result.Status);
        var yoga = Assert.Single(result.MixedActivitySchedule!.Activities,
            decision => decision.ActivityTypeId == (int)WorkoutActivityType.Yoga);
        Assert.Equal(ActivitySessionDurationRule.DemandingYogaDefaultMinutes, yoga.OriginalPrescription!.MainMinutes);
    }

    [Fact]
    public async Task Saved_Demanding_Yoga_Duration_Is_Reused_When_Compatible()
    {
        var profile = Profile();
        var selected = profile.SelectedActivityTypes!.Append((int)WorkoutActivityType.Yoga).ToHashSet();
        var duration = ActivitySessionDurationRule.DemandingYogaMinimumMinutes +
            ActivitySessionDurationRule.GentleYogaMinimumMinutes;
        var durations = profile.ActivitySessionDurationsMinutes!.ToDictionary(pair => pair.Key, pair => pair.Value);
        durations[(int)WorkoutActivityType.Yoga] = duration;
        var source = new Source(profile with
        {
            SelectedActivityTypes = selected,
            ActivitySessionDurationsMinutes = durations
        });
        var request = Request() with
        {
            ActivityEligibilityEvidence =
            [
                new ActivityEligibilityInputV020(WorkoutActivityDto.Yoga,
                    IsDemandingYogaContent: true)
            ]
        };

        var result = await new PreviewStrengthPlanV020UseCase(source).ExecuteAsync(Guid.NewGuid(), request);

        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek, result.Sessions.Count);
        var yoga = Assert.Single(result.MixedActivitySchedule!.Activities,
            decision => decision.ActivityTypeId == (int)WorkoutActivityType.Yoga);
        Assert.Equal(duration, yoga.OriginalPrescription!.MainMinutes);
        Assert.All(result.MixedActivitySchedule.Sessions.Where(session => session.ActivityTypeId == (int)WorkoutActivityType.Yoga),
            session =>
            {
                Assert.True(session.IsHardSession);
                Assert.Equal((decimal)duration, session.DurationMinutes);
                Assert.Null(session.OccurrencePrescription);
            });
    }

    [Fact]
    public async Task Selected_Yoga_Requires_Preferred_Type_Before_Preview()
    {
        var profile = AddSelectedActivity(Profile(), WorkoutActivityType.Yoga);
        var result = await new PreviewStrengthPlanV020UseCase(new Source(profile))
            .ExecuteAsync(Guid.NewGuid(), Request());

        Assert.Equal("RequiredAnswersMissing", result.Status);
        Assert.NotEmpty(result.LimitationCodes);
    }

    [Fact]
    public async Task Selected_Continuous_Cardio_Uses_Profile_Selection_Without_Extra_Evidence_Input()
    {
        var profile = Profile();
        var activities = profile.SelectedActivityTypes!.Append((int)WorkoutActivityType.Walking).ToHashSet();
        var durations = profile.ActivitySessionDurationsMinutes!.ToDictionary(pair => pair.Key, pair => pair.Value);
        durations[(int)WorkoutActivityType.Walking] = WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes;
        var source = new Source(profile with
        {
            SelectedActivityTypes = activities,
            ActivitySessionDurationsMinutes = durations
        });

        var result = await new PreviewStrengthPlanV020UseCase(source).ExecuteAsync(Guid.NewGuid(), Request());

        Assert.Equal("Feasible", result.Status);
        Assert.DoesNotContain(MixedScheduleIncompleteReason, result.LimitationCodes);
        var walkingDecision = Assert.Single(result.MixedActivitySchedule!.Activities,
            decision => decision.ActivityTypeId == (int)WorkoutActivityType.Walking);
        Assert.Equal("Scheduled", walkingDecision.Disposition);
        Assert.Equal(walkingDecision.TargetSessionsPerWeek, walkingDecision.ScheduledSessionsPerWeek);
    }

    [Theory]
    [InlineData(WorkoutActivityType.Cycling)]
    [InlineData(WorkoutActivityType.Running)]
    public async Task Unanchored_Hiit_Evidence_Does_Not_Establish_Rolling_Eligibility(
        WorkoutActivityType underlyingActivity)
    {
        var source = AddSelectedActivity(Profile(), underlyingActivity, WorkoutActivityType.Hiit);
        var request = Request() with
        {
            HiitUnderlyingCardioActivity = WorkoutActivityContractMapper.ToDto(underlyingActivity),
            HiitEligibilityEvidence = CompleteHiitEvidence(underlyingActivity)
        };

        var result = await new PreviewStrengthPlanV020UseCase(new Source(source))
            .ExecuteAsync(Guid.NewGuid(), request);

        // Gate 80: HIIT is deferred, so no evidence combination can produce a scheduled HIIT week.
        var hiitDecision = Assert.Single(result.MixedActivitySchedule!.Activities,
            decision => decision.ActivityTypeId == (int)WorkoutActivityType.Hiit);
        Assert.Equal("Blocked", hiitDecision.Disposition);
        Assert.Equal(0, hiitDecision.ScheduledSessionsPerWeek);
    }

    [Fact]
    public async Task Hiit_stays_blocked_regardless_of_evidence_stamp_age()
    {
        var evidence = CompleteHiitEvidence(WorkoutActivityType.Cycling);
        var answers = new SavedActivityAnswersDto(1, [], evidence)
        {
            HiitUnderlyingCardioActivity = WorkoutActivityDto.Cycling,
            HiitCapture = new HiitEvidenceCaptureStamp(LocalDate, 1)
        };
        var source = AddSelectedActivity(Profile(), WorkoutActivityType.Cycling, WorkoutActivityType.Hiit) with
        {
            SavedAnswers = answers, TimeZoneVersion = 1, CurrentAccountLocalDate = LocalDate
        };
        var fresh = await new PreviewStrengthPlanV020UseCase(new Source(source)).ExecuteAsync(Guid.NewGuid(), Request());
        var lastValid = await new PreviewStrengthPlanV020UseCase(new Source(source with
            { CurrentAccountLocalDate = LocalDate.AddDays(HiitEvidenceCaptureRule.EvidenceValidityCalendarDays - 1) })).ExecuteAsync(Guid.NewGuid(), Request());
        var expired = await new PreviewStrengthPlanV020UseCase(new Source(source with
            { CurrentAccountLocalDate = LocalDate.AddDays(HiitEvidenceCaptureRule.EvidenceValidityCalendarDays) })).ExecuteAsync(Guid.NewGuid(), Request());

        // Gate 80 defers HIIT, so stamp age no longer gates a prescription. The historical validity
        // window stays defined for persisted data compatibility.
        Assert.True(HiitEvidenceCaptureRule.EvidenceValidityCalendarDays > 0);
        foreach (var result in new[] { fresh, lastValid, expired })
        {
            var hiitDecision = Assert.Single(result.MixedActivitySchedule!.Activities,
                decision => decision.ActivityTypeId == (int)WorkoutActivityType.Hiit);
            Assert.Equal("Blocked", hiitDecision.Disposition);
            Assert.Equal(0, hiitDecision.ScheduledSessionsPerWeek);
        }
    }

    [Fact]
    public async Task Explicit_Hiit_Evidence_Cannot_Override_Trusted_Current_Symptoms()
    {
        var source = AddSelectedActivity(Profile() with { HasCurrentSymptoms = true },
            WorkoutActivityType.Cycling, WorkoutActivityType.Hiit);
        var request = Request() with
        {
            HiitUnderlyingCardioActivity = WorkoutActivityDto.Cycling,
            HiitEligibilityEvidence = CompleteHiitEvidence(WorkoutActivityType.Cycling)
        };

        var result = await new PreviewStrengthPlanV020UseCase(new Source(source))
            .ExecuteAsync(Guid.NewGuid(), request);

        var hiitDecision = Assert.Single(result.MixedActivitySchedule!.Activities,
            decision => decision.ActivityTypeId == (int)WorkoutActivityType.Hiit);
        Assert.Equal("Blocked", hiitDecision.Disposition);
        Assert.Equal(0, hiitDecision.ScheduledSessionsPerWeek);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Missing_Required_Safety_Metadata_Cannot_Be_Treated_As_A_Safe_Omission(
        bool demandTagsReviewed, bool restrictionsReviewed)
    {
        var profile = Profile() with { SelectedActivityTypes = new HashSet<int> { (int)WorkoutActivityType.GeneralStrength } };
        var catalog = profile.Catalog.Select(entry => entry.Candidate.ExerciseId == 101
            ? entry with { Candidate = entry.Candidate with
                { DemandTagsReviewed = demandTagsReviewed, RestrictionMappingsReviewed = restrictionsReviewed } }
            : entry).ToArray();
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(profile with { Catalog = catalog }));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request());

        Assert.Equal("Limited", result.Status);
        Assert.DoesNotContain(StrengthStyleSelectionRule.NoSafeStrengthStyleFitCode, result.LimitationCodes);
        Assert.Contains(result.Sessions[0].Slots,
            slot => slot.SlotId == "knee-dominant" && slot.ExerciseId is null);
    }

    [Fact]
    public async Task High_Intensity_Is_Limited_Until_Safer_Prescription_Is_Reviewed()
    {
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(Profile() with
            { SelectedActivityTypes = new HashSet<int> { (int)WorkoutActivityType.StrengthHighIntensity } }));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request() with
        {
            Activity = WorkoutActivityDto.StrengthHighIntensity,
            ExplicitStrengthStyleChoice = WorkoutActivityDto.StrengthHighIntensity,
            ActivityHistory = HistoryFor(WorkoutActivityType.StrengthHighIntensity)
        });

        Assert.Equal("Limited", result.Status);
        Assert.Empty(result.Sessions.SelectMany(session => session.Slots));
    }

    [Fact]
    public async Task High_Volume_Uses_The_Selected_Activity_In_The_Mixed_Schedule()
    {
        var source = new Source(Profile());
        var request = Request() with
        {
            Activity = WorkoutActivityDto.HighVolumeStrength,
            ExplicitStrengthStyleChoice = WorkoutActivityDto.HighVolumeStrength,
            ActivityHistory = new ActivityTrainingHistoryInputV020(
                WorkoutActivityType.HighVolumeStrength.ToString(),
                HistoryConsistentWeeks,
                HistoryAverageSessions,
                LocalDate.AddDays(-PreviousSessionDays),
                true)
        };

        var result = await new PreviewStrengthPlanV020UseCase(source)
            .ExecuteAsync(Guid.NewGuid(), request);

        Assert.Equal("Feasible", result.Status);
        Assert.Equal((int)WorkoutActivityType.HighVolumeStrength, result.EffectiveActivityTypeId);
        Assert.Null(result.FallbackSource);
        Assert.Null(result.FallbackReasonCode);
        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek,
            result.Sessions.Count);
        Assert.Equal(result.Sessions.Count, result.MixedActivitySchedule!.Sessions.Count(session =>
            session.ActivityTypeId == (int)WorkoutActivityType.HighVolumeStrength));
        Assert.DoesNotContain(result.MixedActivitySchedule.Sessions, session =>
            session.ActivityTypeId == (int)WorkoutActivityType.GeneralStrength);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task High_Volume_Fallback_Preserves_Selected_Strength_Duration(bool hasStaleGeneralDuration)
    {
        var durations = new Dictionary<int, int>
        {
            [(int)WorkoutActivityType.HighVolumeStrength] = SelectedFallbackStrengthMinutes
        };
        if (hasStaleGeneralDuration)
            durations[(int)WorkoutActivityType.GeneralStrength] = StaleGeneralStrengthMinutes;
        var profile = Profile() with
        {
            SelectedActivityTypes = new HashSet<int> { (int)WorkoutActivityType.HighVolumeStrength },
            ActivitySessionDurationsMinutes = durations
        };
        var request = Request() with
        {
            Activity = WorkoutActivityDto.HighVolumeStrength,
            ExplicitStrengthStyleChoice = WorkoutActivityDto.HighVolumeStrength,
            ActivityHistory = new ActivityTrainingHistoryInputV020(
                WorkoutActivityType.HighVolumeStrength.ToString(), HistoryConsistentWeeks,
                HistoryAverageSessions, LocalDate.AddDays(-PreviousSessionDays), true)
        };

        var result = await new PreviewStrengthPlanV020UseCase(new Source(profile))
            .ExecuteAsync(Guid.NewGuid(), request);

        Assert.Equal("Feasible", result.Status);
        Assert.Equal((int)WorkoutActivityType.HighVolumeStrength, result.EffectiveActivityTypeId);
        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek,
            result.MixedActivitySchedule!.Sessions.Count);
        Assert.All(result.MixedActivitySchedule.Sessions, session =>
            Assert.Equal((decimal)SelectedFallbackStrengthMinutes, session.DurationMinutes));
        Assert.All(result.Sessions, session =>
            Assert.True(session.EstimatedMinutes <= SelectedFallbackStrengthMinutes));
    }

    [Fact]
    public async Task High_Volume_Fallback_Does_Not_Invent_A_Missing_Selected_Duration()
    {
        var request = Request() with
        {
            Activity = WorkoutActivityDto.HighVolumeStrength,
            ExplicitStrengthStyleChoice = WorkoutActivityDto.HighVolumeStrength,
            ActivityHistory = new ActivityTrainingHistoryInputV020(
                WorkoutActivityType.HighVolumeStrength.ToString(), HistoryConsistentWeeks,
                HistoryAverageSessions, LocalDate.AddDays(-PreviousSessionDays), true),
            ActivitySessionDurationsMinutes = new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.GeneralStrength] = StaleGeneralStrengthMinutes
            }
        };

        var result = await new PreviewStrengthPlanV020UseCase(new Source(Profile() with
            { SelectedActivityTypes = new HashSet<int> { (int)WorkoutActivityType.HighVolumeStrength } }))
            .ExecuteAsync(Guid.NewGuid(), request);

        Assert.Equal("Limited", result.Status);
        Assert.Contains(StrengthPlanPreviewRule.SessionDurationMissingCode, result.LimitationCodes);
    }

    [Fact]
    public async Task High_Volume_Fallback_With_Gentle_Yoga_Is_Feasible_Without_Saved_Yoga_Duration()
    {
        var profile = AddSelectedActivity(Profile(), WorkoutActivityType.Yoga);
        var request = Request() with
        {
            Activity = WorkoutActivityDto.HighVolumeStrength,
            ExplicitStrengthStyleChoice = WorkoutActivityDto.HighVolumeStrength,
            ActivityHistory = new ActivityTrainingHistoryInputV020(
                WorkoutActivityType.HighVolumeStrength.ToString(),
                HistoryConsistentWeeks,
                HistoryAverageSessions,
                LocalDate.AddDays(-PreviousSessionDays),
                true),
            ActivityEligibilityEvidence =
            [
                new ActivityEligibilityInputV020(WorkoutActivityDto.Yoga,
                    IsDemandingYogaContent: false)
            ]
        };

        var result = await new PreviewStrengthPlanV020UseCase(new Source(profile))
            .ExecuteAsync(Guid.NewGuid(), request);

        Assert.Equal("Feasible", result.Status);
        Assert.DoesNotContain(MixedScheduleIncompleteReason, result.LimitationCodes);
        Assert.Equal((int)WorkoutActivityType.HighVolumeStrength, result.EffectiveActivityTypeId);
        Assert.Contains(result.MixedActivitySchedule!.Sessions, session =>
            session.ActivityTypeId == (int)WorkoutActivityType.Yoga &&
            session.DurationMinutes == ActivitySessionDurationRule.GentleYogaDefaultMinutes);
    }

    [Fact]
    public async Task Strength_Timing_Does_Not_Invent_Warmup_Or_Cooldown_Duration()
    {
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(Profile()));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request() with
        {
            WarmupMinutes = null
        });

        Assert.Equal("Limited", result.Status);
        Assert.Empty(result.Sessions);
        Assert.Contains("StrengthWarmupOrCooldownDurationMissing", result.LimitationCodes);
    }

    [Fact]
    public async Task Current_Symptoms_Block_Prescription_Despite_Reviewed_Catalog_Clearance()
    {
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(Profile() with
        {
            HasCurrentSymptoms = true
        }));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request());

        Assert.Equal("Limited", result.Status);
        Assert.Contains("RestrictionOrSymptomRequiresReview", result.LimitationCodes);
    }

    [Fact]
    public async Task Reviewed_Injury_Restriction_Evidence_Replaces_The_Legacy_Symptom_Block()
    {
        // The server derives activity restrictions from the reviewed injury table. An empty set
        // means no activity is restricted, so the legacy injury-pain flag cannot block planning;
        // exercise-level contraindications and injury provenance still guard every prescription.
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(Profile() with
        {
            HasCurrentSymptoms = true,
            RestrictedActivityTypes = new HashSet<int>()
        }));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request());

        Assert.Equal("Feasible", result.Status);
        Assert.DoesNotContain("RestrictionOrSymptomRequiresReview", result.LimitationCodes);
    }

    [Fact]
    public async Task Reviewed_Injury_Restriction_Evidence_Still_Blocks_A_Restricted_Activity()
    {
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(Profile() with
        {
            RestrictedActivityTypes = new HashSet<int>
            {
                (int)WorkoutActivityType.GeneralStrength,
                (int)WorkoutActivityType.HighVolumeStrength,
                (int)WorkoutActivityType.StrengthHighIntensity
            }
        }));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request());

        Assert.Equal("Limited", result.Status);
        Assert.Contains("RestrictionOrSymptomRequiresReview", result.LimitationCodes);
    }

    [Fact]
    public async Task Saved_And_Request_Exercise_Exclusions_Are_Merged_As_Hard_Filters()
    {
        var profile = Profile() with { ExcludedExerciseIds = new HashSet<int> { 101 } };
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(profile));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request() with
        {
            ExcludedExerciseIds = new HashSet<int> { 102 }
        });

        var selectedExerciseIds = result.Sessions.SelectMany(session => session.Slots)
            .Where(slot => slot.ExerciseId.HasValue)
            .Select(slot => slot.ExerciseId!.Value)
            .ToArray();
        Assert.DoesNotContain(101, selectedExerciseIds);
        Assert.DoesNotContain(102, selectedExerciseIds);
    }

    [Fact]
    public void Exercise_Exclusion_Choices_Contain_Only_Reviewed_Slot_Candidates()
    {
        var catalog = new[] { (101, "Hip thrust"), (102, "Squat"), (105, "Rope slam") };
        var reviewedExerciseIds = new HashSet<int> { 101, 102 };

        var options = ReviewedExerciseExclusionCatalogV020Mapper.FromCatalog(catalog, reviewedExerciseIds);

        Assert.Equal(new[] { 101, 102 }, options.Select(option => option.ExerciseId));
    }

    [Fact]
    public async Task Preview_Reports_Missing_Weekdays_Without_Inventing_Availability()
    {
        var profile = Profile() with
        {
            AvailableWeekdays = null,
            ActivitySessionDurationsMinutes = Profile().ActivitySessionDurationsMinutes
        };
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(profile));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request() with
        {
            AvailableWeekdays = null
        });

        Assert.Equal("Limited", result.Status);
        Assert.Contains(StrengthPlanPreviewRule.WeekdaysMissingCode, result.LimitationCodes);
        Assert.Empty(result.Sessions);
    }

    [Fact]
    public async Task Preview_Rejects_A_Single_Available_Day()
    {
        var profile = Profile() with
        {
            AvailableWeekdays = new HashSet<DayOfWeek> { DayOfWeek.Tuesday }
        };
        var store = new ProposalStore();
        var useCase = new CreateStrengthPlanProposalV020UseCase(
            new PreviewStrengthPlanV020UseCase(new Source(profile)), store);

        var proposal = await useCase.ExecuteAsync(Guid.NewGuid(), Request() with
        {
            AvailableWeekdays = new HashSet<DayOfWeek> { DayOfWeek.Tuesday }
        });

        Assert.False(proposal.IsPersisted);
        Assert.NotEqual("Feasible", proposal.Preview.Status);
        Assert.Empty(proposal.Preview.Sessions);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Preview_Is_Feasible_When_Not_All_Available_Days_Are_Filled()
    {
        var profile = Profile() with
        {
            AvailableWeekdays = new HashSet<DayOfWeek>
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
            }
        };
        var store = new ProposalStore();
        var useCase = new CreateStrengthPlanProposalV020UseCase(
            new PreviewStrengthPlanV020UseCase(new Source(profile)), store);

        var proposal = await useCase.ExecuteAsync(Guid.NewGuid(), Request() with
        {
            AvailableWeekdays = new HashSet<DayOfWeek>
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
            }
        });

        Assert.True(proposal.IsPersisted);
        Assert.Equal("Feasible", proposal.Preview.Status);
        Assert.True(proposal.Preview.Sessions.Count < 5);
        Assert.All(proposal.Preview.Sessions, s => Assert.NotNull(s.PlannedWeekday));
        Assert.DoesNotContain(MixedScheduleIncompleteReason, proposal.Preview.LimitationCodes);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task ExhaustedStrengthWithSafeCyclingRemainderStillRequiresStrength()
    {
        var profile = AddSelectedActivity(Profile(), WorkoutActivityType.Cycling) with
        {
            SelectedActivityTypes = new HashSet<int> { (int)WorkoutActivityType.GeneralStrength, (int)WorkoutActivityType.Cycling }
        };
        var request = Request() with { ExcludedExerciseIds = new HashSet<int> { 101, 102, 103, 104 } };
        var store = new ProposalStore();
        var proposal = await new CreateStrengthPlanProposalV020UseCase(
                new PreviewStrengthPlanV020UseCase(new Source(profile)), store)
            .ExecuteAsync(Guid.NewGuid(), request);

        Assert.False(proposal.IsPersisted);
        Assert.Equal("Limited", proposal.Preview.Status);
        Assert.Empty(proposal.Preview.Sessions);
        Assert.Contains(StrengthStyleSelectionRule.NoSafeStrengthStyleFitCode, proposal.Preview.LimitationCodes);
        Assert.NotNull(proposal.Preview.MixedActivitySchedule);
        Assert.Contains(proposal.Preview.MixedActivitySchedule!.Sessions,
            session => session.ActivityTypeId == (int)WorkoutActivityType.Cycling);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task ExhaustedStrengthWithoutOtherActivitiesDoesNotCreateEmptyPlan()
    {
        var profile = Profile() with { SelectedActivityTypes = new HashSet<int> { (int)WorkoutActivityType.GeneralStrength } };
        var request = Request() with { ExcludedExerciseIds = new HashSet<int> { 101, 102, 103, 104 } };
        var store = new ProposalStore();
        var proposal = await new CreateStrengthPlanProposalV020UseCase(new PreviewStrengthPlanV020UseCase(new Source(profile)), store)
            .ExecuteAsync(Guid.NewGuid(), request);
        Assert.False(proposal.IsPersisted);
        Assert.NotEqual("Feasible", proposal.Preview.Status);
        Assert.Contains(StrengthStyleSelectionRule.NoSafeStrengthStyleFitCode, proposal.Preview.LimitationCodes);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task ExhaustedStrengthWithBlockedOtherActivityDoesNotCreateEmptyPlan()
    {
        var profile = AddSelectedActivity(Profile(), WorkoutActivityType.RockClimbing);
        var request = Request() with
        {
            ExcludedExerciseIds = new HashSet<int> { 101, 102, 103, 104 },
            ActivityEligibilityEvidence =
            [new ActivityEligibilityInputV020(WorkoutActivityDto.RockClimbing, HasRopeClimbingCompetence: false)]
        };
        var store = new ProposalStore();
        var proposal = await new CreateStrengthPlanProposalV020UseCase(
                new PreviewStrengthPlanV020UseCase(new Source(profile)), store)
            .ExecuteAsync(Guid.NewGuid(), request);

        Assert.False(proposal.IsPersisted);
        Assert.NotEqual("Feasible", proposal.Preview.Status);
        Assert.Contains(StrengthStyleSelectionRule.NoSafeStrengthStyleFitCode, proposal.Preview.LimitationCodes);
        Assert.Equal(0, store.SaveCount);
    }

    [Theory]
    [InlineData(DayOfWeek.Saturday, DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Sunday, DayOfWeek.Monday)]
    public async Task Adjacent_days_cut_by_recovery_spacing_are_reported_after_reconcile(DayOfWeek first, DayOfWeek second)
    {
        var days = new HashSet<DayOfWeek> { first, second };
        var profile = Profile() with
        {
            AvailableWeekdays = days,
            SelectedActivityTypes = new HashSet<int> { (int)WorkoutActivityType.GeneralStrength }
        };

        var preview = await new PreviewStrengthPlanV020UseCase(new Source(profile))
            .ExecuteAsync(Guid.NewGuid(), Request() with { AvailableWeekdays = days });

        // Every synthetic exercise trains the same muscle, so only one of the adjacent days can hold strength.
        Assert.Single(preview.Sessions);
        Assert.NotEqual("Feasible", preview.Status);
        Assert.Contains(TenRegionMuscleCoverageRule.DaysInsufficientForCoverageCode, preview.LimitationCodes);
        Assert.True(preview.RequiresReducedVolumeAcknowledgment);
        Assert.Contains(StrengthPlanPreviewRule.ReducedVolumeNotice, preview.AdvisoryCodes);
        Assert.Contains(StrengthScheduleAssignmentRule.SessionsReducedForMuscleRecovery, preview.AdvisoryCodes);
    }

    [Fact]
    public async Task Missing_saved_equipment_and_goal_are_required_answers_not_defaults()
    {
        var profile = Profile() with { EquipmentAnswerMissing = true, GoalAnswerMissing = true };

        var preview = await new PreviewStrengthPlanV020UseCase(new Source(profile))
            .ExecuteAsync(Guid.NewGuid(), Request());

        Assert.Equal("RequiredAnswersMissing", preview.Status);
        Assert.Contains(PreviewStrengthPlanV020UseCase.EquipmentAnswerMissingCode, preview.LimitationCodes);
        Assert.Contains(PreviewStrengthPlanV020UseCase.GoalAnswerMissingCode, preview.LimitationCodes);
        Assert.Empty(preview.Sessions);
    }

    [Fact]
    public async Task Preview_carries_the_v2_generation_result_for_a_planned_preview()
    {
        var preview = await new PreviewStrengthPlanV020UseCase(new Source(Profile()))
            .ExecuteAsync(Guid.NewGuid(), Request());

        Assert.NotNull(preview.GenerationV2);
        Assert.Equal(WorkoutPolicyV2.Version, preview.GenerationV2!.PolicyVersion);
        Assert.Equal(WorkoutPolicyV2.InputContractVersion, preview.GenerationV2.InputContractVersion);
        Assert.Contains(preview.GenerationV2.Status, Enum.GetNames<WorkoutGenerationStatusV2>());
    }

    [Fact]
    public async Task Preview_refused_before_planning_carries_no_v2_generation()
    {
        var preview = await new PreviewStrengthPlanV020UseCase(new Source(Profile()))
            .ExecuteAsync(Guid.NewGuid(), Request() with { InputContractVersion = "stale-contract-version" });

        Assert.Equal("InvalidInput", preview.Status);
        Assert.Null(preview.GenerationV2);
    }

    [Fact]
    public async Task V2_generation_is_deterministic_for_the_same_preview_source()
    {
        var first = await new PreviewStrengthPlanV020UseCase(new Source(Profile()))
            .ExecuteAsync(Guid.NewGuid(), Request());
        var second = await new PreviewStrengthPlanV020UseCase(new Source(Profile()))
            .ExecuteAsync(Guid.NewGuid(), Request());

        Assert.NotNull(first.GenerationV2);
        Assert.Equal(first.GenerationV2!.PrescriptionHash, second.GenerationV2!.PrescriptionHash);
        Assert.Equal(first.GenerationV2.SessionCount, second.GenerationV2.SessionCount);
    }

    [Fact]
    public async Task Preview_surfaces_v2_movement_gaps_once_so_the_sheet_can_offer_one_action()
    {
        var source = Profile() with { ExcludedExerciseIds = new HashSet<int> { 101 } };
        var preview = await new PreviewStrengthPlanV020UseCase(new Source(source))
            .ExecuteAsync(Guid.NewGuid(), Request() with { AvailableWeekdays = null });

        Assert.NotNull(preview.GenerationV2);
        Assert.Contains(WeeklyMovementCoverageRule.MissingKneeExtension, preview.GenerationV2!.MissingGaps);
        Assert.Equal(preview.GenerationV2.MissingGaps.Distinct(StringComparer.Ordinal).Count(),
            preview.GenerationV2.MissingGaps.Count);
        Assert.Equal(preview.GenerationV2.MissingGaps.OrderBy(code => code, StringComparer.Ordinal),
            preview.GenerationV2.MissingGaps);
    }

    private static StrengthPlanPreviewRequestV020 Request() => new(
        WorkoutActivityDto.GeneralStrength, LocalDate,
        HistoryFor(WorkoutActivityType.GeneralStrength),
        WarmupMinutes: NoWarmupOrCooldownMinutes, CooldownMinutes: NoWarmupOrCooldownMinutes)
    {
        ExplicitStrengthStyleChoice = WorkoutActivityDto.GeneralStrength,
        AvailableWeekdays = new HashSet<DayOfWeek>
        {
            DayOfWeek.Monday,
            DayOfWeek.Wednesday,
            DayOfWeek.Friday
        }
    };

    private static ActivityTrainingHistoryInputV020 HistoryFor(WorkoutActivityType activity) => new(
        activity.ToString(), HistoryConsistentWeeks, HistoryAverageSessions,
        LocalDate.AddDays(-PreviousSessionDays), true);

    private static StrengthPlanPreviewSourceV020 AddSelectedActivity(
        StrengthPlanPreviewSourceV020 profile,
        params WorkoutActivityType[] activities)
    {
        var selectedActivityTypes = profile.SelectedActivityTypes!.ToHashSet();
        var sessionDurations = profile.ActivitySessionDurationsMinutes!
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var activity in activities)
        {
            selectedActivityTypes.Add((int)activity);
            if (WorkoutPreferencesRule.RequiresSessionDuration(activity))
                sessionDurations[(int)activity] = WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes;
        }

        return profile with
        {
            SelectedActivityTypes = selectedActivityTypes,
            ActivitySessionDurationsMinutes = sessionDurations
        };
    }

    private static HiitEligibilityEvidenceInputV020 CompleteHiitEvidence(WorkoutActivityType activity) => new(
        new ActivityTrainingHistoryInputV020(activity.ToString(),
            ActivityExperienceRule.MinimumConsistentWeeksForEstablished,
            ActivityExperienceRule.EstablishedSessionsPerWeek,
            LocalDate.AddDays(-PreviousSessionDays), true),
        Enumerable.Range(1, ActivityPrescriptionRule.HiitRequiredPriorWeeks)
            .Select(weekOffset => new HiitAerobicWeekInputV020(weekOffset,
                ActivityPrescriptionRule.HiitRequiredAerobicSessionsPerWeek)).ToArray(),
        ExistingClearanceScreeningSatisfied: true);

    [Fact]
    public async Task MissingCompleteMuscleMetadataMakesPreviewLimited()
    {
        var source = Profile() with { MuscleGroupsByExerciseId = null };
        var preview = await new PreviewStrengthPlanV020UseCase(new Source(source))
            .ExecuteAsync(Guid.NewGuid(), Request());

        Assert.Equal("Limited", preview.Status);
        Assert.Contains(WorkoutScheduleValidationRule.StrengthMuscleMetadataMissing, preview.LimitationCodes);
    }

    [Fact]
    public async Task MissingMuscleReviewCannotBecomeAFeasibleNonStrengthRemainder()
    {
        var source = AddSelectedActivity(Profile(), WorkoutActivityType.Yoga) with
        {
            MuscleGroupsByExerciseId = null
        };
        var request = Request() with
        {
            ActivityEligibilityEvidence =
            [new ActivityEligibilityInputV020(WorkoutActivityDto.Yoga, IsDemandingYogaContent: false)]
        };
        var preview = await new PreviewStrengthPlanV020UseCase(new Source(source))
            .ExecuteAsync(Guid.NewGuid(), request);

        Assert.NotEqual("Feasible", preview.Status);
        Assert.Contains(WorkoutScheduleValidationRule.StrengthMuscleMetadataMissing, preview.LimitationCodes);
    }
    [Fact]
    public async Task ConsecutiveDaysPreviewPersistsOnlyTheSafeStrengthSession()
    {
        var days = new HashSet<DayOfWeek> { DayOfWeek.Sunday, DayOfWeek.Monday };
        var source = Profile() with { AvailableWeekdays = days };
        var preview = await new PreviewStrengthPlanV020UseCase(new Source(source))
            .ExecuteAsync(Guid.NewGuid(), Request() with { AvailableWeekdays = days });

        // G-02: the recovery-spacing cut is re-checked; one strength day is below weekly coverage.
        Assert.Equal("Limited", preview.Status);
        Assert.True(preview.RequiresReducedVolumeAcknowledgment);
        var strength = Assert.Single(preview.Sessions);
        var scheduled = Assert.Single(preview.MixedActivitySchedule!.Sessions,
            session => session.ActivityTypeId == (int)WorkoutActivityType.GeneralStrength);
        Assert.Equal(strength.PlannedWeekday, scheduled.Weekday);
        var decision = Assert.Single(preview.MixedActivitySchedule.Activities,
            item => item.ActivityTypeId == (int)WorkoutActivityType.GeneralStrength);
        Assert.Contains(StrengthScheduleAssignmentRule.SessionsReducedForMuscleRecovery, decision.ReasonCodes);
    }
    [Fact]
    public async Task Preview_stays_inside_weekly_capacity_for_four_days_with_several_companions()
    {
        // Beta proposal 01a1f65a: strength plus swimming/yoga/walking/gentle recovery over
        // Mon/Thu/Fri/Sat produced ten sessions over four days, so the accept guard refused the
        // stored week with ProposalMixedScheduleInvalid (capacity = days x 2).
        var days = new HashSet<DayOfWeek>
        {
            DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
        };
        var source = AddSelectedActivity(Profile(), WorkoutActivityType.GeneralStrength,
                WorkoutActivityType.HighVolumeStrength, WorkoutActivityType.Swimming,
                WorkoutActivityType.Yoga, WorkoutActivityType.Walking,
                WorkoutActivityType.GentleRecovery) with
        {
            AvailableWeekdays = days,
            ProfileTrainingExperience = TrainingExperienceLevel.Intermediate,
            DistinctCompletedWorkoutDays = 20,
            RequestedSessionsPerWeek = new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.GeneralStrength] = 4,
                [(int)WorkoutActivityType.Yoga] = 2,
                [(int)WorkoutActivityType.GentleRecovery] = 2
            }
        };
        var request = Request() with
        {
            AvailableWeekdays = days,
            ActivityEligibilityEvidence =
            [
                new ActivityEligibilityInputV020(WorkoutActivityDto.Swimming,
                    HasSwimmingFacilityAccess: true, CanSwimIndependently: true,
                    WantsContinuousSwimming: false),
                new ActivityEligibilityInputV020(WorkoutActivityDto.Yoga, IsDemandingYogaContent: false)
            ]
        };

        var preview = await new PreviewStrengthPlanV020UseCase(new Source(source))
            .ExecuteAsync(Guid.NewGuid(), request);
        Assert.True(preview.MixedActivitySchedule is not null,
            $"status={preview.Status} limits={string.Join(",", preview.LimitationCodes)} " +
            $"advisory={string.Join(",", preview.AdvisoryCodes)} sessions={preview.Sessions.Count}");
        var schedule = preview.MixedActivitySchedule!;
        var layout = string.Join(", ", schedule.Sessions
            .GroupBy(session => session.Weekday)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}:{group.Count()}[{string.Join("/", group.Select(s => s.ActivityTypeId))}]"));
        var decisions = string.Join(", ", schedule.Activities.Select(activity =>
            $"{activity.ActivityTypeId}={activity.Disposition}/{activity.ScheduledSessionsPerWeek}"));

        Assert.True(schedule.Sessions.Count <= days.Count * OwnerMaximumActivitiesPerDay,
            $"capacity: {layout} | decisions: {decisions} | reasons: {string.Join(",", schedule.InputReasonCodes)}");
        Assert.All(schedule.Sessions.GroupBy(session => session.Weekday), group =>
            Assert.True(group.Count() <= OwnerMaximumActivitiesPerDay,
                $"per-day: {layout} | decisions: {decisions}"));
    }

    private static StrengthPlanPreviewSourceV020 OneDayProfile()
    {
        var source = Profile();
        var template = Assert.Single(StrengthTemplateCatalog.ForSchedule(1));
        var allowed = source.ReviewedAllowedExerciseIdsByTemplateSlot.ToDictionary(x => x.Key, x => x.Value);
        var conditions = source.TemplateSlotReviewConditions.ToDictionary(x => x.Key, x => x.Value);
        foreach (var (slot, index) in template.Slots.Where(x => x.IsRequired).Select((slot, i) => (slot, i)))
        {
            var key = $"{template.TemplateId}|{slot.SlotId}";
            allowed[key] = new HashSet<int> { 101 + index };
            conditions[key] = "Reviewed slot conditions";
        }
        return source with
        {
            AvailableWeekdays = new HashSet<DayOfWeek> { DayOfWeek.Monday },
            SelectedActivityTypes = new HashSet<int> { (int)WorkoutActivityType.GeneralStrength },
            ReviewedAllowedExerciseIdsByTemplateSlot = allowed,
            TemplateSlotReviewConditions = conditions
        };
    }

    private static StrengthPlanPreviewSourceV020 Profile()
    {
        var catalog = new[]
        {
            Entry(101, MovementPattern.SquatPattern),
            Entry(102, MovementPattern.HipHinge),
            Entry(103, MovementPattern.HorizontalPush),
            Entry(104, MovementPattern.HorizontalPull)
        };
        var templates = StrengthTemplateCatalog.ForSchedule(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek);
        var allowed = templates.SelectMany(template => template.Slots.Where(slot => slot.IsRequired)
                .Select((slot, index) => (Key: $"{template.TemplateId}|{slot.SlotId}", Id: 101 + index)))
            .ToDictionary(item => item.Key,
                item => (IReadOnlySet<int>)new HashSet<int> { item.Id });
        var conditions = allowed.ToDictionary(item => item.Key, _ => "Reviewed slot conditions");
        return new StrengthPlanPreviewSourceV020(UserGoal.MaintainHealth, Equipment.Bodyweight,
            InjuryFlag.None, false, catalog, new Dictionary<int, int>(), new HashSet<int>(),
            new Dictionary<int, int>(), new HashSet<int>(),
            catalog.ToDictionary(entry => entry.Candidate.ExerciseId, _ => 4),
            catalog.ToDictionary(entry => entry.Candidate.ExerciseId,
                entry => entry.Candidate.Pattern.ToString()),
            allowed, conditions)
        {
            BirthDate = new DateOnly(1990, 1, 1),
            MuscleGroupsByExerciseId = catalog.ToDictionary(entry => entry.Candidate.ExerciseId,
                _ => (IReadOnlySet<string>)new HashSet<string> { "synthetic-muscle" }),
            AvailableWeekdays = new HashSet<DayOfWeek>
            {
                DayOfWeek.Monday,
                DayOfWeek.Wednesday,
                DayOfWeek.Friday
            },
            SelectedActivityTypes = new HashSet<int>
            {
                (int)WorkoutActivityType.GeneralStrength,
                (int)WorkoutActivityType.HighVolumeStrength,
                (int)WorkoutActivityType.StrengthHighIntensity
            },
            ActivitySessionDurationsMinutes = new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.GeneralStrength] = WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes,
                [(int)WorkoutActivityType.HighVolumeStrength] = WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes,
                [(int)WorkoutActivityType.StrengthHighIntensity] = WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes
            }
        };
    }

    private static StrengthExerciseCatalogEntry Entry(int id, MovementPattern pattern) => new(
        $"Exercise {id}", new ReviewedExerciseCandidate(id, "synthetic-catalog-v1", pattern,
            new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
            new HashSet<ActivityExperience> { ActivityExperience.Beginner },
            new HashSet<ActivityDemandTag>(), true, true, new HashSet<ActivityDemandTag>(),
            new HashSet<ExerciseTimingMethod> { ExerciseTimingMethod.StraightSets },
            1, new HashSet<int>(), InjuryFlag.None), []);

    private sealed class Source(StrengthPlanPreviewSourceV020 profile) : IStrengthPlanPreviewSourceV020
    {
        public Task<StrengthPlanPreviewSourceV020?> LoadAsync(Guid userId,
            CancellationToken cancellationToken) => Task.FromResult<StrengthPlanPreviewSourceV020?>(profile);
    }

    private sealed class ProposalStore : IStrengthPlanProposalStoreV020
    {
        public int SaveCount { get; private set; }

        public Task<StrengthPlanProposalCreatedV020> SaveAsync(Guid userId, StrengthPlanPreviewRequestV020 request,
            StrengthPlanPreviewResponseV020 preview, CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(new StrengthPlanProposalCreatedV020(Guid.NewGuid(), preview, true));
        }

        public Task<PlanningLeaseResult> RenewAsync(Guid userId, PlanningLeaseRequest request,
            CancellationToken cancellationToken) => Task.FromResult(new PlanningLeaseResult("Stale", null));

        public Task<PlanningLeaseResult> CloseAsync(Guid userId, PlanningLeaseRequest request,
            CancellationToken cancellationToken) => Task.FromResult(new PlanningLeaseResult("Stale", null));

        public Task<PlanningLeaseResult> TakeoverAsync(Guid userId, Guid proposalId,
            CancellationToken cancellationToken) => Task.FromResult(new PlanningLeaseResult("Stale", null));

        public Task<ActivePlanningPreviewDto> GetActivePreviewAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(new ActivePlanningPreviewDto(null, null));

        public Task<SavedActivityAnswersDto> ReadAnswersAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(new SavedActivityAnswersDto(0, [], null));

        public Task<SavedActivityAnswersDto> SaveAnswersAsync(Guid userId, SaveActivityAnswersRequest request,
            CancellationToken cancellationToken) => Task.FromResult(new SavedActivityAnswersDto(
                request.ExpectedVersion, request.ActivityEligibilityEvidence, request.HiitEligibilityEvidence));
    }
}
