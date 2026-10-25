using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class StrengthPlanPreviewV020Tests
{
    private const int HistoryConsistentWeeks = 1;
    private const int HistoryAverageSessions = 1;
    private const int PreviousSessionDays = 1;
    private const int NoWarmupOrCooldownMinutes = 0;
    private const int HighIntensitySessionsPerWeek =
        ActivityPrescriptionRule.StrengthHighIntensityMaximumSessionsPerWeekInitially;
    private const string MixedScheduleIncompleteReason = "MixedActivityScheduleIncomplete";
    private static readonly DateOnly LocalDate = new(2026, 10, 23);

    [Fact]
    public async Task Reviewed_Required_Slots_Produce_Deterministic_Policy_Preview()
    {
        var source = new Source(Profile());
        var useCase = new PreviewStrengthPlanV020UseCase(source);
        var request = Request();

        var first = await useCase.ExecuteAsync(Guid.NewGuid(), request);
        var second = await useCase.ExecuteAsync(Guid.NewGuid(), request);

        Assert.Equal("Feasible", first.Status);
        Assert.Single(first.Sessions);
        Assert.Equal([101, 102, 103, 104], first.Sessions[0].Slots
            .Where(slot => slot.IsRequired).Select(slot => slot.ExerciseId));
        Assert.Equal(first.Sessions[0].Slots.Select(slot => slot.ExerciseId),
            second.Sessions[0].Slots.Select(slot => slot.ExerciseId));
        Assert.True(first.Sessions[0].EstimatedMinutes > 0);
        Assert.Contains("OneDayFullBodyHasLimitedWeeklyCoverage", first.AdvisoryCodes);
        Assert.NotNull(first.MixedActivitySchedule);
        Assert.Equal(first.MixedActivitySchedule!.Sessions
                .Where(session => session.ActivityTypeId == (int)WorkoutActivityType.GeneralStrength)
                .Select(session => (DayOfWeek?)session.Weekday)
                .ToArray(),
            first.Sessions.Select(session => session.PlannedWeekday).ToArray());
    }

    [Fact]
    public void Request_Can_Be_Serialized_And_Deserialized_Via_SystemTextJson()
    {
        var request = Request();
        var json = System.Text.Json.JsonSerializer.Serialize(request, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<StrengthPlanPreviewRequestV020>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(deserialized);
        Assert.Equal(request.Activity, deserialized.Activity);
        Assert.Equal(request.AvailableWeekdays, deserialized.AvailableWeekdays);
    }

    [Fact]
    public async Task Missing_Flexible_Activity_Frequency_Limits_The_Mixed_Plan_Without_Losing_Strength_Preview()
    {
        var profile = Profile();
        var activities = profile.SelectedActivityTypes!.Append((int)WorkoutActivityType.Yoga).ToHashSet();
        var durations = profile.ActivitySessionDurationsMinutes!.ToDictionary(pair => pair.Key, pair => pair.Value);
        durations[(int)WorkoutActivityType.Yoga] = ActivitySessionDurationRule.GentleYogaDefaultMinutes;
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

        Assert.Equal("Limited", result.Status);
        Assert.Single(result.Sessions);
        Assert.Contains(MixedScheduleIncompleteReason, result.LimitationCodes);
        var yogaDecision = Assert.Single(result.MixedActivitySchedule!.Activities,
            decision => decision.ActivityTypeId == (int)WorkoutActivityType.Yoga);
        Assert.Equal("Omitted", yogaDecision.Disposition);
        Assert.Contains("ActivityFrequencyPreferenceMissing", yogaDecision.ReasonCodes);
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
        var walkingDecision = Assert.Single(result.MixedActivitySchedule!.Activities,
            decision => decision.ActivityTypeId == (int)WorkoutActivityType.Walking);
        Assert.Equal("Scheduled", walkingDecision.Disposition);
        Assert.Equal(2, walkingDecision.ScheduledSessionsPerWeek);
    }

    [Theory]
    [InlineData(WorkoutActivityType.Cycling)]
    [InlineData(WorkoutActivityType.Running)]
    public async Task Explicit_Hiit_Evidence_Uses_Selected_Modality_And_Replaces_One_Continuous_Session(
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

        var hiitDecision = Assert.Single(result.MixedActivitySchedule!.Activities,
            decision => decision.ActivityTypeId == (int)WorkoutActivityType.Hiit);
        var cardioDecision = Assert.Single(result.MixedActivitySchedule.Activities,
            decision => decision.ActivityTypeId == (int)underlyingActivity);
        Assert.Equal("Scheduled", hiitDecision.Disposition);
        Assert.Equal(underlyingActivity.ToString(), hiitDecision.Modality);
        Assert.Equal(ActivityPrescriptionRule.HiitMaximumSessionsPerWeek,
            hiitDecision.ScheduledSessionsPerWeek);
        Assert.Equal(ActivityPrescriptionRule.EstablishedCardioSessionsPerWeekWithoutBaseline -
                     ActivityPrescriptionRule.HiitMaximumSessionsPerWeek,
            cardioDecision.TargetSessionsPerWeek);
        Assert.Contains("OneContinuousSessionReplacedByHiit", cardioDecision.ReasonCodes);
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
        Assert.Contains("HiitEligibilityNotEstablishedByRequiredEvidence", hiitDecision.ReasonCodes);
    }

    [Fact]
    public async Task Missing_Restriction_Review_Leaves_Only_Affected_Strength_Preview_Limited()
    {
        var profile = Profile();
        var catalog = profile.Catalog.Select(entry => entry.Candidate.ExerciseId == 101
            ? entry with { Candidate = entry.Candidate with { RestrictionMappingsReviewed = false } }
            : entry).ToArray();
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(profile with { Catalog = catalog }));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request());

        Assert.Equal("Limited", result.Status);
        Assert.Contains(result.Sessions[0].Slots,
            slot => slot.SlotId == "knee-dominant" && slot.ExerciseId is null);
    }

    [Fact]
    public async Task High_Intensity_Uses_Its_Two_Session_Activity_Policy()
    {
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(Profile()));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request() with
        {
            Activity = WorkoutActivityDto.StrengthHighIntensity,
            ActivityHistory = HistoryFor(WorkoutActivityType.StrengthHighIntensity)
        });

        Assert.Equal(HighIntensitySessionsPerWeek, result.Sessions.Count);
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
    public async Task Preview_Succeeds_When_Profile_Source_Has_Missing_Weekdays_And_Unselected_Activity()
    {
        var profile = Profile() with
        {
            AvailableWeekdays = null,
            SelectedActivityTypes = null,
            ActivitySessionDurationsMinutes = null
        };
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(profile));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request() with
        {
            AvailableWeekdays = null
        });

        Assert.Equal("Feasible", result.Status);
        Assert.Single(result.Sessions);
    }

    private static StrengthPlanPreviewRequestV020 Request() => new(
        WorkoutActivityDto.GeneralStrength, LocalDate,
        HistoryFor(WorkoutActivityType.GeneralStrength),
        WarmupMinutes: NoWarmupOrCooldownMinutes, CooldownMinutes: NoWarmupOrCooldownMinutes)
    {
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

    private static StrengthPlanPreviewSourceV020 Profile()
    {
        var catalog = new[]
        {
            Entry(101, MovementPattern.SquatPattern),
            Entry(102, MovementPattern.HipHinge),
            Entry(103, MovementPattern.HorizontalPush),
            Entry(104, MovementPattern.HorizontalPull)
        };
        var template = StrengthTemplateCatalog.ForSchedule(1).Single();
        var allowed = template.Slots.Where(slot => slot.IsRequired)
            .Select((slot, index) => (Key: $"{template.TemplateId}|{slot.SlotId}", Id: 101 + index))
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
}
