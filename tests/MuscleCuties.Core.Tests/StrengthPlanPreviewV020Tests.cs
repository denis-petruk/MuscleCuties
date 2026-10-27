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
    private const int SelectedDemandingYogaDurationMinutes = 35;
    private const int SelectedFallbackStrengthMinutes = 60;
    private const int StaleGeneralStrengthMinutes = 90;
    private const int ExpectedSavedProposalCount = 1;
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
        var yogaSession = Assert.Single(result.MixedActivitySchedule!.Sessions,
            session => session.ActivityTypeId == (int)WorkoutActivityType.Yoga);
        Assert.Equal((decimal)SelectedDemandingYogaDurationMinutes, yogaSession.DurationMinutes);
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
                MuscleGroupsByExerciseId = catalog.ToDictionary(entry => entry.Candidate.ExerciseId,
                entry => (IReadOnlySet<string>)new HashSet<string> { entry.Candidate.Pattern.ToString() }),
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
                MixedActivityWeekScheduleRule.MaximumSessionsPerDay);
            if (day.Count() > 1)
            {
                var sessions = day.ToArray();
                Assert.True(MixedActivityWeekScheduleRule.IsSupportedSameDayStrengthCompanion(
                    (WorkoutActivityType)sessions[0].ActivityTypeId,
                    sessions[0].IsHardSession,
                    (WorkoutActivityType)sessions[1].ActivityTypeId,
                    sessions[1].IsHardSession));
            }
        });
        Assert.Equal(ExpectedSavedProposalCount, store.SaveCount);
    }

    [Fact]
    public async Task Optional_Activity_Without_A_Safe_Day_Does_Not_Block_The_Strength_Plan()
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
        Assert.DoesNotContain(schedule.Sessions,
            session => session.ActivityTypeId == (int)WorkoutActivityType.Mobility);
        Assert.Contains(schedule.Activities, decision =>
            decision.ActivityTypeId == (int)WorkoutActivityType.Mobility &&
            decision.ScheduledSessionsPerWeek == 0);
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

        var yoga = Assert.Single(result.MixedActivitySchedule!.Sessions,
            session => session.ActivityTypeId == (int)WorkoutActivityType.Yoga);
        Assert.Equal(ActivitySessionDurationRule.DemandingYogaDurationMinutesDefault, yoga.DurationMinutes);
        Assert.True(yoga.IsHardSession);
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
        Assert.Contains(result.MixedActivitySchedule!.Sessions,
            session => session.ActivityTypeId == (int)WorkoutActivityType.Yoga &&
                       session.DurationMinutes == ActivitySessionDurationRule.DemandingYogaDefaultMinutes);
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
        Assert.Contains(result.MixedActivitySchedule!.Sessions,
            session => session.ActivityTypeId == (int)WorkoutActivityType.Yoga &&
                       session.DurationMinutes == duration &&
                       session.IsHardSession);
    }

    [Fact]
    public async Task Selected_Yoga_Without_Additional_Answers_Uses_Gentle_Default()
    {
        var profile = AddSelectedActivity(Profile(), WorkoutActivityType.Yoga);
        var result = await new PreviewStrengthPlanV020UseCase(new Source(profile))
            .ExecuteAsync(Guid.NewGuid(), Request());

        Assert.Equal("Feasible", result.Status);
        Assert.Contains(result.MixedActivitySchedule!.Sessions, session =>
            session.ActivityTypeId == (int)WorkoutActivityType.Yoga &&
            session.DurationMinutes == ActivitySessionDurationRule.GentleYogaDefaultMinutes &&
            !session.IsHardSession);
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
        Assert.Equal(1, walkingDecision.ScheduledSessionsPerWeek);
        Assert.Contains(MixedActivityWeekScheduleRule.ActivityFrequencyLimitedByAvailableWeekdays,
            walkingDecision.ReasonCodes);
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
    public async Task High_Intensity_Is_Limited_Until_Safer_Prescription_Is_Reviewed()
    {
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(Profile()));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request() with
        {
            Activity = WorkoutActivityDto.StrengthHighIntensity,
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
            ActivityHistory = new ActivityTrainingHistoryInputV020(
                WorkoutActivityType.HighVolumeStrength.ToString(), HistoryConsistentWeeks,
                HistoryAverageSessions, LocalDate.AddDays(-PreviousSessionDays), true),
            ActivitySessionDurationsMinutes = new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.GeneralStrength] = StaleGeneralStrengthMinutes
            }
        };

        var result = await new PreviewStrengthPlanV020UseCase(new Source(Profile()))
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
    public async Task Preview_Cuts_Volume_When_Available_Days_Are_Fewer_Than_Template_Sessions_And_Enables_Apply()
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

        Assert.True(proposal.IsPersisted);
        Assert.Equal("Feasible", proposal.Preview.Status);
        Assert.Single(proposal.Preview.Sessions);
        Assert.Equal(DayOfWeek.Tuesday, proposal.Preview.Sessions[0].PlannedWeekday);
        Assert.DoesNotContain(MixedScheduleIncompleteReason, proposal.Preview.LimitationCodes);
        Assert.Equal(1, store.SaveCount);
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
    public async Task ExhaustedStrengthCanLeaveSafeCyclingRemainder()
    {
        var profile = AddSelectedActivity(Profile(), WorkoutActivityType.Cycling) with
        {
            SelectedActivityTypes = new HashSet<int> { (int)WorkoutActivityType.GeneralStrength, (int)WorkoutActivityType.Cycling }
        };
        var request = Request() with { ExcludedExerciseIds = new HashSet<int> { 101, 102, 103, 104 } };
        var preview = await new PreviewStrengthPlanV020UseCase(new Source(profile)).ExecuteAsync(Guid.NewGuid(), request);
        Assert.Equal("Feasible", preview.Status);
        Assert.Empty(preview.Sessions);
        Assert.NotNull(preview.MixedActivitySchedule);
        Assert.Contains(preview.MixedActivitySchedule!.Sessions, session => session.ActivityTypeId == (int)WorkoutActivityType.Cycling);
        Assert.Contains(preview.MixedActivitySchedule.Activities, activity => activity.ActivityTypeId == (int)WorkoutActivityType.GeneralStrength && activity.Disposition == "Omitted");
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
        Assert.Equal(0, store.SaveCount);
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

        Assert.Equal("Feasible", preview.Status);
        var strength = Assert.Single(preview.Sessions);
        var scheduled = Assert.Single(preview.MixedActivitySchedule!.Sessions,
            session => session.ActivityTypeId == (int)WorkoutActivityType.GeneralStrength);
        Assert.Equal(strength.PlannedWeekday, scheduled.Weekday);
        var decision = Assert.Single(preview.MixedActivitySchedule.Activities,
            item => item.ActivityTypeId == (int)WorkoutActivityType.GeneralStrength);
        Assert.Contains(StrengthScheduleAssignmentRule.SessionsReducedForMuscleRecovery, decision.ReasonCodes);
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
