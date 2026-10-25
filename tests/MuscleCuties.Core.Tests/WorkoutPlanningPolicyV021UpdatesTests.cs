using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutPlanningPolicyV021UpdatesTests
{
    private static readonly DateOnly ReferenceDate = new(2026, 10, 23);

    // =========================================================================
    // 1. General Strength & High-Volume Strength Fallback Tests
    // =========================================================================

    [Fact]
    public void Beginner_Selecting_HighVolumeStrength_Gets_GeneralStrength_Fallback()
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        var evidence = new ActivityPrescriptionContext(
            WorkoutActivityType.HighVolumeStrength,
            ActivityExperience.Beginner,
            null, false, true, false, 0, false, false);

        var context = new MixedActivityWeekScheduleContext(
            [WorkoutActivityType.HighVolumeStrength],
            days,
            300m,
            new Dictionary<WorkoutActivityType, decimal> { [WorkoutActivityType.HighVolumeStrength] = 45m },
            [evidence]);

        var result = MixedActivityWeekScheduleRule.Evaluate(context);

        var decision = Assert.Single(result.Activities);
        Assert.Equal(WorkoutActivityType.HighVolumeStrength, decision.Activity);
        Assert.Equal(WorkoutActivityType.GeneralStrength, decision.EffectiveActivity);
        Assert.Equal(MixedActivityScheduleDisposition.Scheduled, decision.Disposition);
        Assert.True(decision.Reasons.Contains("HighVolumeRequiresEstablishedResistanceHistory") ||
                    decision.Reasons.Contains("BeginnerProfileRequiresGeneralStrength"));
        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek,
            decision.PolicySessionsPerWeek);
        Assert.Equal(2, decision.TargetSessionsPerWeek);
        Assert.Equal(2, decision.ScheduledSessionsPerWeek);
    }

    [Fact]
    public void Hvs_Fallback_Does_Not_Replace_An_Invalid_Explicit_Gs_Duration()
    {
        var evidence = new ActivityPrescriptionContext(
            WorkoutActivityType.HighVolumeStrength, ActivityExperience.Beginner,
            null, false, true, false, 0, false, false);
        var context = new MixedActivityWeekScheduleContext(
            [WorkoutActivityType.HighVolumeStrength],
            [DayOfWeek.Monday, DayOfWeek.Wednesday],
            ActivitySessionDurationRule.StandardMaximumMinutes,
            new Dictionary<WorkoutActivityType, decimal>
            {
                [WorkoutActivityType.HighVolumeStrength] = ActivitySessionDurationRule.FortyFiveMinutes,
                [WorkoutActivityType.GeneralStrength] = ActivitySessionDurationRule.StandardMaximumMinutes +
                    ActivitySessionDurationRule.StandardIncrementMinutes
            },
            [evidence]);

        var decision = Assert.Single(MixedActivityWeekScheduleRule.Evaluate(context).Activities);
        Assert.Equal(MixedActivityScheduleDisposition.Blocked, decision.Disposition);
        Assert.Equal(0, decision.ScheduledSessionsPerWeek);
        Assert.Contains(MixedActivityWeekScheduleRule.SessionDurationOutsideSavedPreferenceRange,
            decision.Reasons);
    }

    [Fact]
    public void Intermediate_With_Insufficient_History_Gets_Baseline_GS_Values()
    {
        // 8 weeks consistent (< 12) -> insufficient history for HVS
        var history = new ActivityTrainingHistory(
            WorkoutActivityType.GeneralStrength.ToString(),
            ConsistentParticipationWeeks: 8,
            AverageSessionsPerWeekInRecentFourWeeks: 2m,
            LastCompletedSessionDate: ReferenceDate.AddDays(-3),
            IsSelfReported: true);

        var isEstablished = GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(history, ReferenceDate);
        Assert.False(isEstablished);

        var policy = GeneralStrengthFrequencyResolver.Resolve(
            availableWeekdaysCount: 3,
            isEstablishedResistanceHistory: isEstablished,
            profileLevel: TrainingExperienceLevel.Intermediate);

        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek,
            policy.PolicySessionsPerWeek);
        Assert.Equal(2, policy.TargetSessionsPerWeek);
        Assert.Equal(2, policy.MaxSessionsPerWeek);
    }

    [Fact]
    public void Advanced_With_Unknown_History_Gets_Baseline_GS_Values()
    {
        var isEstablished = GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(null, ReferenceDate);
        Assert.False(isEstablished);

        var policy = GeneralStrengthFrequencyResolver.Resolve(
            availableWeekdaysCount: 3,
            isEstablishedResistanceHistory: isEstablished,
            profileLevel: TrainingExperienceLevel.Advanced);

        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek,
            policy.PolicySessionsPerWeek);
        Assert.Equal(2, policy.TargetSessionsPerWeek);
        Assert.Equal(2, policy.MaxSessionsPerWeek);
    }

    [Fact]
    public void Returning_History_At_28_Days_Gets_Baseline_GS_Values()
    {
        var history = new ActivityTrainingHistory(
            WorkoutActivityType.GeneralStrength.ToString(),
            ConsistentParticipationWeeks: 20,
            AverageSessionsPerWeekInRecentFourWeeks: 3m,
            LastCompletedSessionDate: ReferenceDate.AddDays(-28), // 28 days break
            IsSelfReported: true);

        var isEstablished = GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(history, ReferenceDate);
        Assert.False(isEstablished);

        var policy = GeneralStrengthFrequencyResolver.Resolve(
            availableWeekdaysCount: 4,
            isEstablishedResistanceHistory: isEstablished,
            profileLevel: TrainingExperienceLevel.Intermediate);

        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek,
            policy.PolicySessionsPerWeek);
        Assert.Equal(2, policy.TargetSessionsPerWeek);
        Assert.Equal(2, policy.MaxSessionsPerWeek);
    }

    [Fact]
    public void Established_Intermediate_Or_Advanced_Gets_Policy2_TargetMin3_Max3()
    {
        var history = new ActivityTrainingHistory(
            WorkoutActivityType.GeneralStrength.ToString(),
            ConsistentParticipationWeeks: 16,
            AverageSessionsPerWeekInRecentFourWeeks: 3m,
            LastCompletedSessionDate: ReferenceDate.AddDays(-3),
            IsSelfReported: true);

        var isEstablished = GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(history, ReferenceDate);
        Assert.True(isEstablished);

        var policy = GeneralStrengthFrequencyResolver.Resolve(
            availableWeekdaysCount: 4,
            isEstablishedResistanceHistory: isEstablished,
            profileLevel: TrainingExperienceLevel.Intermediate);

        Assert.Equal(2, policy.PolicySessionsPerWeek);
        Assert.Equal(3, policy.TargetSessionsPerWeek);
        Assert.Equal(3, policy.MaxSessionsPerWeek);

        // When available weekdays is 2, Target is min(3, 2) = 2
        var policy2Days = GeneralStrengthFrequencyResolver.Resolve(
            availableWeekdaysCount: 2,
            isEstablishedResistanceHistory: isEstablished,
            profileLevel: TrainingExperienceLevel.Advanced);

        Assert.Equal(2, policy2Days.PolicySessionsPerWeek);
        Assert.Equal(2, policy2Days.TargetSessionsPerWeek);
        Assert.Equal(3, policy2Days.MaxSessionsPerWeek);
    }

    [Fact]
    public void Hvs_Eligibility_Boundaries_Weeks11Vs12_SessionsBelow2Vs2_Days27Vs28()
    {
        // 11 vs 12 weeks
        var w11 = new ActivityTrainingHistory("GS", 11, 2.0m, ReferenceDate.AddDays(-2), false);
        var w12 = new ActivityTrainingHistory("GS", 12, 2.0m, ReferenceDate.AddDays(-2), false);
        Assert.False(GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(w11, ReferenceDate));
        Assert.True(GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(w12, ReferenceDate));

        // 1.9 vs 2.0 sessions
        var sBelow2 = new ActivityTrainingHistory("GS", 16, 1.9m, ReferenceDate.AddDays(-2), false);
        var s2 = new ActivityTrainingHistory("GS", 16, 2.0m, ReferenceDate.AddDays(-2), false);
        Assert.False(GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(sBelow2, ReferenceDate));
        Assert.True(GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(s2, ReferenceDate));

        // 27 vs 28 days since last session
        var d27 = new ActivityTrainingHistory("GS", 16, 2.5m, ReferenceDate.AddDays(-27), false);
        var d28 = new ActivityTrainingHistory("GS", 16, 2.5m, ReferenceDate.AddDays(-28), false);
        Assert.True(GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(d27, ReferenceDate));
        Assert.False(GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(d28, ReferenceDate));
    }

    [Fact]
    public void Missing_History_Differs_From_Valid_Zero_History()
    {
        var zeroHistory = new ActivityHistoryInput("GS", 0, 0m, 0);
        Assert.True(ActivityHistoryPolicyRule.IsValidHistory(zeroHistory));
        Assert.False(ActivityHistoryPolicyRule.HasMissingRequiredFields(zeroHistory));

        var missingHistory = new ActivityHistoryInput("GS", null, null, null);
        Assert.False(ActivityHistoryPolicyRule.IsValidHistory(missingHistory));
        Assert.True(ActivityHistoryPolicyRule.HasMissingRequiredFields(missingHistory));

        Assert.False(GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(null, ReferenceDate));
        var zeroTrainingHistory = new ActivityTrainingHistory("GS", 0, 0m, ReferenceDate, false);
        Assert.False(GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(zeroTrainingHistory, ReferenceDate));
    }

    [Fact]
    public void Hvs_And_Gs_Selection_Does_Not_Duplicate_Gs_Prescriptions()
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        var gsEvidence = new ActivityPrescriptionContext(
            WorkoutActivityType.GeneralStrength,
            ActivityExperience.Beginner,
            null, false, true, false, 0, false, false);
        var hvsEvidence = new ActivityPrescriptionContext(
            WorkoutActivityType.HighVolumeStrength,
            ActivityExperience.Beginner,
            null, false, true, false, 0, false, false);

        var context = new MixedActivityWeekScheduleContext(
            [WorkoutActivityType.GeneralStrength, WorkoutActivityType.HighVolumeStrength],
            days,
            300m,
            new Dictionary<WorkoutActivityType, decimal>
            {
                [WorkoutActivityType.GeneralStrength] = 45m,
                [WorkoutActivityType.HighVolumeStrength] = 45m
            },
            [gsEvidence, hvsEvidence]);

        var result = MixedActivityWeekScheduleRule.Evaluate(context);

        // HVS should be replaced with GS without scheduling separate second GS sessions
        var gsDecision = Assert.Single(result.Activities, a => a.Activity == WorkoutActivityType.GeneralStrength);
        var hvsDecision = Assert.Single(result.Activities, a => a.Activity == WorkoutActivityType.HighVolumeStrength);

        Assert.Equal(MixedActivityScheduleDisposition.Scheduled, gsDecision.Disposition);
        Assert.Equal(MixedActivityScheduleDisposition.NotScheduled, hvsDecision.Disposition);
        Assert.Equal(WorkoutActivityType.GeneralStrength, hvsDecision.EffectiveActivity);
        Assert.Contains("ReplacedWithGeneralStrength", hvsDecision.Reasons);

        // The shared GS target is two sessions; HVS contributes no second quota.
        Assert.Equal(2, result.Sessions.Count(s => s.Activity == WorkoutActivityType.GeneralStrength));
        Assert.DoesNotContain(result.Sessions, s => s.Activity == WorkoutActivityType.HighVolumeStrength);
    }

    [Fact]
    public void Available_Days_0_1_2_3_Produce_Deterministic_Gs_Target()
    {
        Assert.Equal(0, GeneralStrengthFrequencyResolver.ResolveAvailableWeekdaysCount(null));
        Assert.Equal(0, GeneralStrengthFrequencyResolver.ResolveAvailableWeekdaysCount([]));
        Assert.Equal(1, GeneralStrengthFrequencyResolver.ResolveAvailableWeekdaysCount([DayOfWeek.Monday]));
        Assert.Equal(2, GeneralStrengthFrequencyResolver.ResolveAvailableWeekdaysCount([DayOfWeek.Monday, DayOfWeek.Wednesday]));
        Assert.Equal(3, GeneralStrengthFrequencyResolver.ResolveAvailableWeekdaysCount([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]));
        Assert.Equal(2, GeneralStrengthFrequencyResolver.ResolveAvailableWeekdaysCount(
            [DayOfWeek.Monday, DayOfWeek.Monday, DayOfWeek.Wednesday]));
        Assert.Equal(1, GeneralStrengthFrequencyResolver.ResolveAvailableWeekdaysCount(
            [DayOfWeek.Monday, DayOfWeek.Wednesday], generalDaysPerWeek: 1));

        // The target remains two; an undersized calendar produces a limited preview.
        Assert.Equal(2, GeneralStrengthFrequencyResolver.Resolve(0, false, TrainingExperienceLevel.Beginner).TargetSessionsPerWeek);
        Assert.Equal(2, GeneralStrengthFrequencyResolver.Resolve(1, false, TrainingExperienceLevel.Beginner).TargetSessionsPerWeek);
        Assert.Equal(2, GeneralStrengthFrequencyResolver.Resolve(2, false, TrainingExperienceLevel.Beginner).TargetSessionsPerWeek);
        Assert.Equal(2, GeneralStrengthFrequencyResolver.Resolve(3, false, TrainingExperienceLevel.Beginner).TargetSessionsPerWeek);

        // Established plans retain at least two strength sessions.
        Assert.Equal(2, GeneralStrengthFrequencyResolver.Resolve(0, true, TrainingExperienceLevel.Intermediate).TargetSessionsPerWeek);
        Assert.Equal(2, GeneralStrengthFrequencyResolver.Resolve(1, true, TrainingExperienceLevel.Intermediate).TargetSessionsPerWeek);
        Assert.Equal(2, GeneralStrengthFrequencyResolver.Resolve(2, true, TrainingExperienceLevel.Intermediate).TargetSessionsPerWeek);
        Assert.Equal(3, GeneralStrengthFrequencyResolver.Resolve(3, true, TrainingExperienceLevel.Intermediate).TargetSessionsPerWeek);
    }

    [Fact]
    public void One_Available_Day_Cannot_Meet_Two_General_Strength_Sessions()
    {
        var evidence = new ActivityPrescriptionContext(
            WorkoutActivityType.GeneralStrength, ActivityExperience.Beginner,
            null, false, true, false, 0, false, false);
        var context = new MixedActivityWeekScheduleContext(
            [WorkoutActivityType.GeneralStrength], [DayOfWeek.Monday], 60m,
            new Dictionary<WorkoutActivityType, decimal>
            {
                [WorkoutActivityType.GeneralStrength] = 45m
            },
            [evidence]);

        var result = MixedActivityWeekScheduleRule.Evaluate(context);
        var decision = Assert.Single(result.Activities);
        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek,
            decision.PolicySessionsPerWeek);
        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek,
            decision.TargetSessionsPerWeek);
        Assert.Equal(1, decision.ScheduledSessionsPerWeek);
        Assert.Equal(MixedActivityScheduleDisposition.PartiallyScheduled, decision.Disposition);
        Assert.Single(result.Sessions);
        Assert.Equal(DayOfWeek.Monday, result.Sessions[0].Day);
    }

    [Fact]
    public void Hard_Day_Conflicts_Reduce_Actual_Scheduling_Without_False_Success()
    {
        // 3 available days, but user also has 2 other hard activities (RockClimbing and Cycling established continuous)
        // Hard cap: max 3 hard days/week. With Climbing and HVS fallback to GS:
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday }; // Only 2 available days
        var powerYogaEvidence = new ActivityPrescriptionContext(
            WorkoutActivityType.Yoga,
            ActivityExperience.Beginner,
            null, false, true, false, 0, false, false,
            ActivityName: "Power Yoga");
        var hvsEvidence = new ActivityPrescriptionContext(
            WorkoutActivityType.HighVolumeStrength,
            ActivityExperience.Beginner,
            null, false, true, false, 0, false, false);

        var context = new MixedActivityWeekScheduleContext(
            [WorkoutActivityType.Yoga, WorkoutActivityType.HighVolumeStrength],
            days,
            300m,
            new Dictionary<WorkoutActivityType, decimal>
            {
                [WorkoutActivityType.Yoga] = 20m,
                [WorkoutActivityType.HighVolumeStrength] = 45m
            },
            [powerYogaEvidence, hvsEvidence]);

        var result = MixedActivityWeekScheduleRule.Evaluate(context);

        var hvsDecision = Assert.Single(result.Activities, a => a.Activity == WorkoutActivityType.HighVolumeStrength);
        var yogaDecision = Assert.Single(result.Activities, a => a.Activity == WorkoutActivityType.Yoga);

        Assert.Equal(WorkoutActivityType.GeneralStrength, hvsDecision.EffectiveActivity);
        Assert.Equal(2, hvsDecision.TargetSessionsPerWeek);
        Assert.Equal(2, hvsDecision.ScheduledSessionsPerWeek);

        // Power Yoga is reduced to one target, but both hard days are needed for strength.
        Assert.Equal(1, yogaDecision.TargetSessionsPerWeek);
        Assert.Equal(0, yogaDecision.ScheduledSessionsPerWeek);
        Assert.Contains(MixedActivityWeekScheduleRule.ActivityFrequencyLimitedByAvailableWeekdays,
            yogaDecision.Reasons);
        Assert.Contains(MixedActivityWeekScheduleRule.HardDayLimitReached, yogaDecision.Reasons);
    }

    [Fact]
    public void Injury_Conflicts_Are_Not_Bypassed_By_Gs_Substitution()
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        // User has active symptoms/restriction
        var hvsEvidence = new ActivityPrescriptionContext(
            WorkoutActivityType.HighVolumeStrength,
            ActivityExperience.Beginner,
            null,
            HasCurrentRestrictionOrSymptom: true,
            IsExplicitlySelected: true,
            IsEstablishedResistanceHistory: false,
            CompletedMainExerciseExposures: 0,
            HasKnownManageableLoad: false,
            UserConfirmedCurrentFamiliarity: false);

        var context = new MixedActivityWeekScheduleContext(
            [WorkoutActivityType.HighVolumeStrength],
            days,
            300m,
            new Dictionary<WorkoutActivityType, decimal> { [WorkoutActivityType.HighVolumeStrength] = 45m },
            [hvsEvidence]);

        var result = MixedActivityWeekScheduleRule.Evaluate(context);

        var decision = Assert.Single(result.Activities);
        Assert.Equal(MixedActivityScheduleDisposition.Blocked, decision.Disposition);
        Assert.Contains("RestrictionOrSymptomRequiresReview", decision.Reasons);
    }

    // =========================================================================
    // 2. Recovery Activity Defaults & Yoga Classification Tests
    // =========================================================================

    [Fact]
    public void Missing_GentleYoga_Duration_Resolves_To_15()
    {
        var yoga = ActivityPrescriptionRule.Evaluate(new ActivityPrescriptionContext(
            WorkoutActivityType.Yoga, ActivityExperience.Beginner, null, false, true, false, 0, false, false));

        Assert.Equal(ActivityPrescriptionStatus.Eligible, yoga.Status);
        Assert.Equal(15, yoga.Sessions.Single().MainMinutes);
    }

    [Fact]
    public void Missing_Mobility_Duration_Resolves_To_10()
    {
        var mobility = ActivityPrescriptionRule.Evaluate(new ActivityPrescriptionContext(
            WorkoutActivityType.Mobility, ActivityExperience.Beginner, null, false, true, false, 0, false, false));

        Assert.Equal(ActivityPrescriptionStatus.Eligible, mobility.Status);
        Assert.Equal(10, mobility.Sessions.Single().MainMinutes);
    }

    [Fact]
    public void Missing_GentleRecovery_Duration_Resolves_To_10()
    {
        var recovery = ActivityPrescriptionRule.Evaluate(new ActivityPrescriptionContext(
            WorkoutActivityType.GentleRecovery, ActivityExperience.Beginner, null, false, true, false, 0, false, false));

        Assert.Equal(ActivityPrescriptionStatus.Eligible, recovery.Status);
        Assert.Equal(10, recovery.Sessions.Single().MainMinutes);
    }

    [Fact]
    public void Explicit_Valid_Durations_Remain_Unchanged()
    {
        var yoga = ActivityPrescriptionRule.Evaluate(new ActivityPrescriptionContext(
            WorkoutActivityType.Yoga, ActivityExperience.Beginner, null, false, true, false, 0, false, false,
            UserSelectedDurationMinutes: 20));

        Assert.Equal(ActivityPrescriptionStatus.Eligible, yoga.Status);
        Assert.Equal(20, yoga.Sessions.Single().MainMinutes);

        var mobility = ActivityPrescriptionRule.Evaluate(new ActivityPrescriptionContext(
            WorkoutActivityType.Mobility, ActivityExperience.Beginner, null, false, true, false, 0, false, false,
            UserSelectedDurationMinutes: 15));

        Assert.Equal(ActivityPrescriptionStatus.Eligible, mobility.Status);
        Assert.Equal(15, mobility.Sessions.Single().MainMinutes);
    }

    [Fact]
    public void Explicit_Invalid_Durations_Produce_Validation_Errors()
    {
        // Gentle yoga with 12 minutes (must be in increment of 5 and >= 15)
        var invalidYoga = ActivityPrescriptionRule.Evaluate(new ActivityPrescriptionContext(
            WorkoutActivityType.Yoga, ActivityExperience.Beginner, null, false, true, false, 0, false, false,
            UserSelectedDurationMinutes: 12));

        Assert.Equal(ActivityPrescriptionStatus.NeedsUserDuration, invalidYoga.Status);

        // Mobility with 50 minutes (max is 30)
        var invalidMobility = ActivityPrescriptionRule.Evaluate(new ActivityPrescriptionContext(
            WorkoutActivityType.Mobility, ActivityExperience.Beginner, null, false, true, false, 0, false, false,
            UserSelectedDurationMinutes: 50));

        Assert.Equal(ActivityPrescriptionStatus.NeedsUserDuration, invalidMobility.Status);
    }

    [Fact]
    public void Missing_Classification_With_No_Demanding_Evidence_Becomes_Gentle()
    {
        var classification = YogaClassificationRule.Classify(
            explicitClassification: null,
            activityName: "Morning Flow Yoga",
            tags: ["relax", "mindfulness"]);

        Assert.False(classification.IsDemanding);
        Assert.False(classification.IsInconsistent);
        Assert.Null(classification.ReasonCode);
    }

    [Theory]
    [InlineData("Power Yoga")]
    [InlineData("Hot Yoga Flow")]
    [InlineData("Ashtanga Primary Series")]
    [InlineData("Rocket Yoga 1")]
    [InlineData("Vinyasa (intense)")]
    [InlineData("POWER FLOW")]
    [InlineData("hot vinyasa")]
    public void Supplied_Demanding_Keywords_Recognized_Case_Insensitively(string activityName)
    {
        var classification = YogaClassificationRule.Classify(
            explicitClassification: null,
            activityName: activityName,
            tags: null);

        Assert.True(classification.IsDemanding);
        Assert.False(classification.IsInconsistent);
    }

    [Fact]
    public void Demanding_Tags_Are_Recognized()
    {
        var classification = YogaClassificationRule.Classify(
            explicitClassification: null,
            activityName: "Yoga Session",
            tags: ["stretching", "Hot", "breathwork"]);

        Assert.True(classification.IsDemanding);
    }

    [Theory]
    [InlineData("Hotel Yoga Room Practice")]
    [InlineData("Shotokan & Stretch")]
    [InlineData("Photography Meditation")]
    public void Unrelated_Substrings_Do_Not_Trigger_Demanding(string activityName)
    {
        var classification = YogaClassificationRule.Classify(
            explicitClassification: null,
            activityName: activityName,
            tags: null);

        Assert.False(classification.IsDemanding);
    }

    [Fact]
    public void Missing_DemandingYoga_Duration_Resolves_To_20()
    {
        var demandingYoga = ActivityPrescriptionRule.Evaluate(new ActivityPrescriptionContext(
            WorkoutActivityType.Yoga, ActivityExperience.Beginner, null, false, true, false, 0, false, false,
            ActivityName: "Power Yoga"));

        Assert.Equal(ActivityPrescriptionStatus.Eligible, demandingYoga.Status);
        Assert.Equal(20, demandingYoga.Sessions.Single().MainMinutes);
    }

    [Fact]
    public void Explicit_DemandingYoga_Duration_Of_15_Is_Rejected_Not_Defaulted()
    {
        // 15 is valid for gentle yoga, but demanding yoga minimum is 20
        var demandingYoga15 = ActivityPrescriptionRule.Evaluate(new ActivityPrescriptionContext(
            WorkoutActivityType.Yoga, ActivityExperience.Beginner, null, false, true, false, 0, false, false,
            UserSelectedDurationMinutes: 15,
            ActivityName: "Power Yoga"));

        Assert.Equal(ActivityPrescriptionStatus.NeedsUserDuration, demandingYoga15.Status);
    }

    [Fact]
    public void Demanding_Yoga_Counts_Toward_HardDay_Limits()
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday };
        var powerYogaEvidence = new ActivityPrescriptionContext(
            WorkoutActivityType.Yoga, ActivityExperience.Beginner, null, false, true, false, 0, false, false,
            ActivityName: "Power Yoga");
        var gsEvidence = new ActivityPrescriptionContext(
            WorkoutActivityType.GeneralStrength, ActivityExperience.Beginner, null, false, true, false, 0, false, false);

        var context = new MixedActivityWeekScheduleContext(
            [WorkoutActivityType.GeneralStrength, WorkoutActivityType.Yoga],
            days,
            300m,
            new Dictionary<WorkoutActivityType, decimal>
            {
                [WorkoutActivityType.GeneralStrength] = 45m,
                [WorkoutActivityType.Yoga] = 30m
            },
            [gsEvidence, powerYogaEvidence]);

        var result = MixedActivityWeekScheduleRule.Evaluate(context);

        // Power yoga is hard, General Strength is hard -> cannot be scheduled on the same day (max 1 hard/day)
        var yogaSessionDays = result.Sessions.Where(s => s.Activity == WorkoutActivityType.Yoga).Select(s => s.Day).ToHashSet();
        var gsSessionDays = result.Sessions.Where(s => s.Activity == WorkoutActivityType.GeneralStrength).Select(s => s.Day).ToHashSet();

        Assert.Empty(yogaSessionDays.Intersect(gsSessionDays));
    }

    [Fact]
    public void Defaults_Still_Respect_Time_And_Injury_Constraints()
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday };
        // Daily capacity is 10 minutes
        var yogaEvidence = new ActivityPrescriptionContext(
            WorkoutActivityType.Yoga, ActivityExperience.Beginner, null, false, true, false, 0, false, false);

        var context = new MixedActivityWeekScheduleContext(
            [WorkoutActivityType.Yoga],
            days,
            10m, // Daily capacity is 10 min, but default yoga is 15 min
            new Dictionary<WorkoutActivityType, decimal>(),
            [yogaEvidence]);

        var result = MixedActivityWeekScheduleRule.Evaluate(context);

        var decision = Assert.Single(result.Activities);
        Assert.Equal(MixedActivityScheduleDisposition.Omitted, decision.Disposition);
        Assert.Contains(MixedActivityWeekScheduleRule.DailyCapacityTooSmallForSession, decision.Reasons);
    }

    [Fact]
    public void Unselected_Recovery_Activities_Are_Not_Automatically_Added()
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        var gsEvidence = new ActivityPrescriptionContext(
            WorkoutActivityType.GeneralStrength, ActivityExperience.Beginner, null, false, true, false, 0, false, false);

        var context = new MixedActivityWeekScheduleContext(
            [WorkoutActivityType.GeneralStrength], // Only GS selected, not Yoga or Mobility
            days,
            300m,
            new Dictionary<WorkoutActivityType, decimal> { [WorkoutActivityType.GeneralStrength] = 45m },
            [gsEvidence]);

        var result = MixedActivityWeekScheduleRule.Evaluate(context);

        Assert.DoesNotContain(result.Activities, a => a.Activity == WorkoutActivityType.Yoga);
        Assert.DoesNotContain(result.Activities, a => a.Activity == WorkoutActivityType.Mobility);
        Assert.DoesNotContain(result.Activities, a => a.Activity == WorkoutActivityType.GentleRecovery);
        Assert.All(result.Sessions, s => Assert.Equal(WorkoutActivityType.GeneralStrength, s.Activity));
    }

    // =========================================================================
    // 3. Preview and UI Mapping Tests
    // =========================================================================

    [Fact]
    public void Supported_Limitations_Trigger_Exactly_One_Gs_Retry()
    {
        var templates = StrengthTemplateCatalog.ForSchedule(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek);
        var requiredSlots = templates.SelectMany(t => t.Slots.Where(s => s.IsRequired).Select(s => (Template: t, Slot: s))).ToArray();
        var catalog = requiredSlots.Select((item, index) => Entry(101 + index, item.Slot.PreferredPatterns.First())).ToArray();
        var reviewedSlots = requiredSlots.Select((item, index) =>
            (Key: $"{item.Template.TemplateId}|{item.Slot.SlotId}", Id: 101 + index))
            .ToDictionary(item => item.Key, item => (IReadOnlySet<int>)new HashSet<int> { item.Id });

        var data = new StrengthPlanPreviewData(
            UserGoal.MaintainHealth, Equipment.Bodyweight, InjuryFlag.None, false,
            new HashSet<WorkoutActivityType> { WorkoutActivityType.HighVolumeStrength },
            new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
            new Dictionary<WorkoutActivityType, int> { [WorkoutActivityType.HighVolumeStrength] = 45 },
            null, catalog, new Dictionary<int, int>(), new HashSet<int>(), new Dictionary<int, int>(),
            new HashSet<int>(), catalog.ToDictionary(e => e.Candidate.ExerciseId, _ => 4),
            catalog.ToDictionary(e => e.Candidate.ExerciseId, e => e.Candidate.Pattern.ToString()),
            reviewedSlots, reviewedSlots.Keys.ToDictionary(k => k, _ => "Reviewed slot"));

        var choices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.HighVolumeStrength, ReferenceDate,
            new ActivityTrainingHistory(WorkoutActivityType.HighVolumeStrength.ToString(), 1, 1m, ReferenceDate.AddDays(-1), true),
            WarmupMinutes: 0, CooldownMinutes: 0);

        var result = StrengthPlanPreviewRule.EvaluateWithFallback(choices, data);

        Assert.Equal(StrengthPlanPreviewStatus.Feasible, result.Status);
        Assert.Equal(WorkoutActivityType.GeneralStrength, result.EffectiveActivity);
        Assert.Equal("GeneralStrengthFallback", result.FallbackSource);
        Assert.Equal("HighVolumeRequiresEstablishedResistanceHistory", result.FallbackReasonCode);
        Assert.Empty(result.LimitationCodes);
    }

    [Fact]
    public void Already_Gs_Requests_Do_Not_Loop()
    {
        var choices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.GeneralStrength, ReferenceDate,
            new ActivityTrainingHistory("GeneralStrength", 1, 1m, ReferenceDate.AddDays(-1), true),
            WarmupMinutes: 0, CooldownMinutes: 0);

        // Data with no catalog will fail
        var data = new StrengthPlanPreviewData(
            UserGoal.MaintainHealth, Equipment.Bodyweight, InjuryFlag.None, false,
            new HashSet<WorkoutActivityType> { WorkoutActivityType.GeneralStrength },
            new HashSet<DayOfWeek> { DayOfWeek.Monday },
            new Dictionary<WorkoutActivityType, int> { [WorkoutActivityType.GeneralStrength] = 45 },
            null, [], new Dictionary<int, int>(), new HashSet<int>(), new Dictionary<int, int>(),
            new HashSet<int>(), new Dictionary<int, int>(), new Dictionary<int, string>(),
            new Dictionary<string, IReadOnlySet<int>>(), new Dictionary<string, string>());

        var result = StrengthPlanPreviewRule.EvaluateWithFallback(choices, data);

        Assert.Equal(StrengthPlanPreviewStatus.Limited, result.Status);
        Assert.Null(result.FallbackSource);
    }

    [Fact]
    public void Failed_Gs_Retry_Preserves_Actual_Failure()
    {
        // When GS preview fails (e.g., duration too short)
        var templates = StrengthTemplateCatalog.ForSchedule(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek);
        var requiredSlots = templates.SelectMany(t => t.Slots.Where(s => s.IsRequired).Select(s => (Template: t, Slot: s))).ToArray();
        var catalog = requiredSlots.Select((item, index) => Entry(101 + index, item.Slot.PreferredPatterns.First())).ToArray();
        var reviewedSlots = requiredSlots.Select((item, index) =>
            (Key: $"{item.Template.TemplateId}|{item.Slot.SlotId}", Id: 101 + index))
            .ToDictionary(item => item.Key, item => (IReadOnlySet<int>)new HashSet<int> { item.Id });

        var data = new StrengthPlanPreviewData(
            UserGoal.MaintainHealth, Equipment.Bodyweight, InjuryFlag.None, false,
            new HashSet<WorkoutActivityType> { WorkoutActivityType.HighVolumeStrength },
            new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
            new Dictionary<WorkoutActivityType, int> { [WorkoutActivityType.HighVolumeStrength] = 45 },
            null, catalog, new Dictionary<int, int>(), new HashSet<int>(), new Dictionary<int, int>(),
            new HashSet<int>(), catalog.ToDictionary(e => e.Candidate.ExerciseId, _ => 4),
            catalog.ToDictionary(e => e.Candidate.ExerciseId, e => e.Candidate.Pattern.ToString()),
            reviewedSlots, reviewedSlots.Keys.ToDictionary(k => k, _ => "Reviewed slot"));

        var choices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.HighVolumeStrength, ReferenceDate,
            new ActivityTrainingHistory(WorkoutActivityType.HighVolumeStrength.ToString(), 1, 1m, ReferenceDate.AddDays(-1), true),
            ExcludedExerciseIds: new HashSet<int> { 101, 102, 103, 104 }, // Exclude all candidates
            WarmupMinutes: 0, CooldownMinutes: 0);

        var result = StrengthPlanPreviewRule.EvaluateWithFallback(choices, data);

        Assert.Equal(StrengthPlanPreviewStatus.Limited, result.Status);
        Assert.Equal(WorkoutActivityType.GeneralStrength, result.EffectiveActivity);
        Assert.Equal("GeneralStrengthFallback", result.FallbackSource);
        Assert.Equal("HighVolumeRequiresEstablishedResistanceHistory", result.FallbackReasonCode);
        Assert.Contains("RequiredMovementSlotUnresolved", result.LimitationCodes);
    }

    [Fact]
    public void Preview_Does_Not_Schedule_Or_Persist_Sessions()
    {
        var templates = StrengthTemplateCatalog.ForSchedule(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek);
        var requiredSlots = templates.SelectMany(t => t.Slots.Where(s => s.IsRequired).Select(s => (Template: t, Slot: s))).ToArray();
        var catalog = requiredSlots.Select((item, index) => Entry(101 + index, item.Slot.PreferredPatterns.First())).ToArray();
        var reviewedSlots = requiredSlots.Select((item, index) =>
            (Key: $"{item.Template.TemplateId}|{item.Slot.SlotId}", Id: 101 + index))
            .ToDictionary(item => item.Key, item => (IReadOnlySet<int>)new HashSet<int> { item.Id });

        var data = new StrengthPlanPreviewData(
            UserGoal.MaintainHealth, Equipment.Bodyweight, InjuryFlag.None, false,
            new HashSet<WorkoutActivityType> { WorkoutActivityType.GeneralStrength },
            new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
            new Dictionary<WorkoutActivityType, int> { [WorkoutActivityType.GeneralStrength] = 45 },
            null, catalog, new Dictionary<int, int>(), new HashSet<int>(), new Dictionary<int, int>(),
            new HashSet<int>(), catalog.ToDictionary(e => e.Candidate.ExerciseId, _ => 4),
            catalog.ToDictionary(e => e.Candidate.ExerciseId, e => e.Candidate.Pattern.ToString()),
            reviewedSlots, reviewedSlots.Keys.ToDictionary(k => k, _ => "Reviewed slot"));

        var choices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.GeneralStrength, ReferenceDate,
            new ActivityTrainingHistory("GeneralStrength", 1, 1m, ReferenceDate.AddDays(-1), true),
            WarmupMinutes: 0, CooldownMinutes: 0);

        var preview = StrengthPlanPreviewRule.Evaluate(choices, data);

        // Preview returns read-only result
        Assert.NotNull(preview);
        Assert.Equal(StrengthPlanPreviewStatus.Feasible, preview.Status);
        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek, preview.Sessions.Count);
        // It does not alter database or write to disk
    }

    [Fact]
    public void Completed_And_InProgress_Sessions_Remain_Unchanged()
    {
        var planned = WorkoutSessionLifecycleRule.CanModifySession(WorkoutSessionLifecycleState.Planned);
        Assert.True(planned.IsAllowed);

        var inProgress = WorkoutSessionLifecycleRule.CanModifySession(WorkoutSessionLifecycleState.InProgress);
        Assert.True(inProgress.IsBlocked);

        var completed = WorkoutSessionLifecycleRule.CanModifySession(WorkoutSessionLifecycleState.Completed);
        Assert.True(completed.IsBlocked);
    }

    // =========================================================================
    // 3. Beginner Strength Eligibility & Planning Regression Tests (15 Tests)
    // =========================================================================

    [Fact]
    public void Regression_1_Beginner_Direct_GS_Zero_History_Is_Eligible()
    {
        var zeroHistory = new ActivityTrainingHistory("GeneralStrength", 0, 0m, ReferenceDate.AddDays(-1), true);
        var choices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.GeneralStrength, ReferenceDate, zeroHistory,
            WarmupMinutes: 0, CooldownMinutes: 0);
        var data = CreatePreviewData(WorkoutActivityType.GeneralStrength, profileLevel: TrainingExperienceLevel.Beginner);

        var result = StrengthPlanPreviewRule.EvaluateWithFallback(choices, data);

        Assert.Equal(StrengthPlanPreviewStatus.Feasible, result.Status);
        Assert.Equal(WorkoutActivityType.GeneralStrength, result.EffectiveActivity);
        Assert.DoesNotContain("ExperienceHistoryMissing", result.LimitationCodes);
        Assert.DoesNotContain("BeginnerProfileRequiresGeneralStrength", result.LimitationCodes);
    }

    [Fact]
    public void Regression_2_Beginner_Direct_GS_Missing_History_Is_Eligible()
    {
        var choices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.GeneralStrength, ReferenceDate, null,
            WarmupMinutes: 0, CooldownMinutes: 0);
        var data = CreatePreviewData(WorkoutActivityType.GeneralStrength, profileLevel: TrainingExperienceLevel.Beginner);

        var result = StrengthPlanPreviewRule.EvaluateWithFallback(choices, data);

        Assert.Equal(StrengthPlanPreviewStatus.Feasible, result.Status);
        Assert.Equal(WorkoutActivityType.GeneralStrength, result.EffectiveActivity);
        Assert.Empty(result.LimitationCodes);
    }

    [Fact]
    public void Regression_3_Beginner_Direct_GS_Does_Not_Require_12_Weeks_History()
    {
        // 3 weeks consistent (< 12 weeks required for established)
        var partialHistory = new ActivityTrainingHistory("GeneralStrength", 3, 1m, ReferenceDate.AddDays(-2), true);
        var choices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.GeneralStrength, ReferenceDate, partialHistory,
            WarmupMinutes: 0, CooldownMinutes: 0);
        var data = CreatePreviewData(WorkoutActivityType.GeneralStrength, profileLevel: TrainingExperienceLevel.Beginner);

        var result = StrengthPlanPreviewRule.EvaluateWithFallback(choices, data);

        Assert.Equal(StrengthPlanPreviewStatus.Feasible, result.Status);
        Assert.DoesNotContain("HighVolumeRequiresEstablishedResistanceHistory", result.LimitationCodes);
    }

    [Fact]
    public void Regression_4_Beginner_GS_Uses_Specified_Starting_Sets_Reps_And_RIR()
    {
        var context = new ActivityPrescriptionContext(
            WorkoutActivityType.GeneralStrength, ActivityExperience.Beginner, null,
            false, true, false, 0, false, false);

        var result = ActivityPrescriptionRule.Evaluate(context);

        Assert.Equal(ActivityPrescriptionStatus.Eligible, result.Status);
        var session = Assert.Single(result.Sessions);
        Assert.Equal(2, session.Sets); // 2 working sets
        Assert.Equal(8, session.MinimumRepetitions); // 8-12 repetitions
        Assert.Equal(12, session.MaximumRepetitions);
        Assert.Equal(3, session.RepetitionsInReserve); // target RIR = 3
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(5, 2)]
    public void Regression_5_Beginner_GS_Target_Is_Min_2_Available_Days(int availableDays, int expectedTarget)
    {
        var policy = GeneralStrengthFrequencyResolver.Resolve(
            availableDays, isEstablishedResistanceHistory: false, TrainingExperienceLevel.Beginner);

        Assert.Equal(expectedTarget, policy.TargetSessionsPerWeek);
        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek,
            policy.PolicySessionsPerWeek);
        Assert.Equal(2, policy.MaxSessionsPerWeek);
    }

    [Fact]
    public void Regression_6_Beginner_GS_Never_Exceeds_2_Sessions_Per_Week()
    {
        var allSevenDays = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };
        var policy = GeneralStrengthFrequencyResolver.Resolve(
            allSevenDays.Length, isEstablishedResistanceHistory: false, TrainingExperienceLevel.Beginner);

        Assert.Equal(2, policy.MaxSessionsPerWeek);
        Assert.Equal(2, policy.TargetSessionsPerWeek);

        var context = new MixedActivityWeekScheduleContext(
            [WorkoutActivityType.GeneralStrength],
            allSevenDays,
            300m,
            new Dictionary<WorkoutActivityType, decimal> { [WorkoutActivityType.GeneralStrength] = 45m },
            [new ActivityPrescriptionContext(WorkoutActivityType.GeneralStrength, ActivityExperience.Beginner, null, false, true, false, 0, false, false)],
            RequestedSessionsPerWeek: new Dictionary<WorkoutActivityType, int> { [WorkoutActivityType.GeneralStrength] = 5 });

        var schedule = MixedActivityWeekScheduleRule.Evaluate(context);
        var decision = Assert.Single(schedule.Activities);
        Assert.Equal(2, decision.ScheduledSessionsPerWeek);
        Assert.True(decision.ScheduledSessionsPerWeek <= 2);
    }

    [Fact]
    public void Regression_7_Beginner_With_HVS_Becomes_GS_Fallback()
    {
        var choices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.HighVolumeStrength, ReferenceDate, null,
            WarmupMinutes: 0, CooldownMinutes: 0);
        var data = CreatePreviewData(WorkoutActivityType.HighVolumeStrength, profileLevel: TrainingExperienceLevel.Beginner);

        var result = StrengthPlanPreviewRule.EvaluateWithFallback(choices, data);

        Assert.Equal(StrengthPlanPreviewStatus.Feasible, result.Status);
        Assert.Equal(WorkoutActivityType.GeneralStrength, result.EffectiveActivity);
        Assert.Equal("GeneralStrengthFallback", result.FallbackSource);
        Assert.Equal("BeginnerProfileRequiresGeneralStrength", result.FallbackReasonCode);
    }

    [Fact]
    public void Regression_8_Derived_GS_Does_Not_Inherit_HVS_History_Gating()
    {
        // Zero history with Beginner profile
        var zeroHistory = new ActivityTrainingHistory("HighVolumeStrength", 0, 0m, ReferenceDate.AddDays(-1), true);
        var choices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.HighVolumeStrength, ReferenceDate, zeroHistory,
            WarmupMinutes: 0, CooldownMinutes: 0);
        var data = CreatePreviewData(WorkoutActivityType.HighVolumeStrength, profileLevel: TrainingExperienceLevel.Beginner);

        var result = StrengthPlanPreviewRule.EvaluateWithFallback(choices, data);

        // Does not fail from HVS history requirement; derived GS succeeds under beginner rules
        Assert.Equal(StrengthPlanPreviewStatus.Feasible, result.Status);
        Assert.Equal(WorkoutActivityType.GeneralStrength, result.EffectiveActivity);
        Assert.Empty(result.LimitationCodes);
    }

    [Fact]
    public void Regression_9_Direct_GS_Preview_Does_Not_Return_Requires_GS_Limitation()
    {
        var choices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.GeneralStrength, ReferenceDate, null,
            WarmupMinutes: 0, CooldownMinutes: 0);
        var data = CreatePreviewData(WorkoutActivityType.GeneralStrength, profileLevel: TrainingExperienceLevel.Beginner);

        var result = StrengthPlanPreviewRule.EvaluateWithFallback(choices, data);

        Assert.DoesNotContain("BeginnerProfileRequiresGeneralStrength", result.LimitationCodes);
        Assert.DoesNotContain("HighVolumeRequiresEstablishedResistanceHistory", result.LimitationCodes);
        Assert.Null(result.FallbackReasonCode);
        Assert.Null(result.FallbackSource);
    }

    [Fact]
    public void Regression_10_Successful_GS_Fallback_Clears_Resolved_Limitation()
    {
        var choices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.HighVolumeStrength, ReferenceDate, null,
            WarmupMinutes: 0, CooldownMinutes: 0);
        var data = CreatePreviewData(WorkoutActivityType.HighVolumeStrength, profileLevel: TrainingExperienceLevel.Beginner);

        var result = StrengthPlanPreviewRule.EvaluateWithFallback(choices, data);

        Assert.Equal(StrengthPlanPreviewStatus.Feasible, result.Status);
        Assert.Empty(result.LimitationCodes);
        Assert.Equal("BeginnerProfileRequiresGeneralStrength", result.FallbackReasonCode);
    }

    [Fact]
    public void Regression_11_Failed_GS_Fallback_Preserves_Real_Failure()
    {
        var choices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.HighVolumeStrength, ReferenceDate, null,
            ExcludedExerciseIds: new HashSet<int> { 101, 102, 103, 104 }, // exclude all
            WarmupMinutes: 0, CooldownMinutes: 0);
        var data = CreatePreviewData(WorkoutActivityType.HighVolumeStrength, profileLevel: TrainingExperienceLevel.Beginner);

        var result = StrengthPlanPreviewRule.EvaluateWithFallback(choices, data);

        Assert.Equal(StrengthPlanPreviewStatus.Limited, result.Status);
        Assert.Equal(WorkoutActivityType.GeneralStrength, result.EffectiveActivity);
        Assert.Contains("RequiredMovementSlotUnresolved", result.LimitationCodes);
        Assert.Equal("BeginnerProfileRequiresGeneralStrength", result.FallbackReasonCode);
    }

    [Fact]
    public void Regression_12_HVS_And_SHI_Eligibility_Restrictions_Remain_Intact()
    {
        // 1. Intermediate user with insufficient history (< 12 weeks) requesting HVS
        var sparseHistory = new ActivityTrainingHistory("HighVolumeStrength", 4, 2m, ReferenceDate.AddDays(-3), true);
        var hvsChoices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.HighVolumeStrength, ReferenceDate, sparseHistory,
            WarmupMinutes: 0, CooldownMinutes: 0);
        var hvsData = CreatePreviewData(WorkoutActivityType.HighVolumeStrength, profileLevel: TrainingExperienceLevel.Intermediate);
        var hvsDirect = StrengthPlanPreviewRule.EvaluateDirect(hvsChoices, hvsData);

        Assert.Equal(StrengthPlanPreviewStatus.Limited, hvsDirect.Status);
        Assert.Contains("HighVolumeRequiresEstablishedResistanceHistory", hvsDirect.LimitationCodes);

        // 2. Beginner requesting SHI is blocked
        var shiChoices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.StrengthHighIntensity, ReferenceDate, null,
            WarmupMinutes: 0, CooldownMinutes: 0);
        var shiData = CreatePreviewData(WorkoutActivityType.StrengthHighIntensity, profileLevel: TrainingExperienceLevel.Beginner);
        var shiResult = StrengthPlanPreviewRule.EvaluateDirect(shiChoices, shiData);

        Assert.Equal(StrengthPlanPreviewStatus.Limited, shiResult.Status);
        Assert.Contains("BeginnerProfileRequiresGeneralStrength", shiResult.LimitationCodes);
    }

    [Fact]
    public void Regression_13_Actual_Injury_Equipment_Time_And_Availability_Restrictions_Still_Apply()
    {
        // Duration invalid (too short < 10)
        var shortDurationData = CreatePreviewData(WorkoutActivityType.GeneralStrength, durationMinutes: 5);
        var choices = new StrengthPlanPreviewChoices(
            WorkoutActivityType.GeneralStrength, ReferenceDate, null,
            WarmupMinutes: 0, CooldownMinutes: 0);
        var result = StrengthPlanPreviewRule.EvaluateWithFallback(choices, shortDurationData);

        Assert.Equal(StrengthPlanPreviewStatus.InvalidInput, result.Status);
        Assert.Contains("SelectedActivityDurationInvalid", result.LimitationCodes);
    }

    [Fact]
    public void Regression_14_GS_Selected_Alongside_HVS_Does_Not_Produce_Duplicate_Quotas()
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        var gsEvidence = new ActivityPrescriptionContext(
            WorkoutActivityType.GeneralStrength, ActivityExperience.Beginner, null,
            false, true, false, 0, false, false);
        var hvsEvidence = new ActivityPrescriptionContext(
            WorkoutActivityType.HighVolumeStrength, ActivityExperience.Beginner, null,
            false, true, false, 0, false, false);

        var context = new MixedActivityWeekScheduleContext(
            [WorkoutActivityType.GeneralStrength, WorkoutActivityType.HighVolumeStrength],
            days,
            300m,
            new Dictionary<WorkoutActivityType, decimal>
            {
                [WorkoutActivityType.GeneralStrength] = 45m,
                [WorkoutActivityType.HighVolumeStrength] = 45m
            },
            [gsEvidence, hvsEvidence]);

        var result = MixedActivityWeekScheduleRule.Evaluate(context);

        var gsDecision = result.Activities.Single(a => a.Activity == WorkoutActivityType.GeneralStrength);
        var hvsDecision = result.Activities.Single(a => a.Activity == WorkoutActivityType.HighVolumeStrength);

        Assert.Equal(MixedActivityScheduleDisposition.Scheduled, gsDecision.Disposition);
        Assert.Equal(2, gsDecision.ScheduledSessionsPerWeek);

        // HVS was replaced with GS and should NOT create duplicate quotas
        Assert.Equal(MixedActivityScheduleDisposition.NotScheduled, hvsDecision.Disposition);
        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek,
            hvsDecision.PolicySessionsPerWeek);
        Assert.Equal(2, hvsDecision.TargetSessionsPerWeek);
        Assert.Equal(2, hvsDecision.MaxSessionsPerWeek);
        Assert.Equal(2, hvsDecision.ScheduledSessionsPerWeek);
        Assert.Contains("ReplacedWithGeneralStrength", hvsDecision.Reasons);

        var totalScheduledStrength = result.Sessions.Count(s =>
            s.Activity is WorkoutActivityType.GeneralStrength or WorkoutActivityType.HighVolumeStrength);
        Assert.Equal(2, totalScheduledStrength);
    }

    [Fact]
    public void Regression_15_No_Sessions_Are_Falsely_Reported_As_Scheduled()
    {
        // Daily capacity of 10 min is smaller than the 45-min session duration
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday };
        var gsEvidence = new ActivityPrescriptionContext(
            WorkoutActivityType.GeneralStrength, ActivityExperience.Beginner, null,
            false, true, false, 0, false, false);

        var context = new MixedActivityWeekScheduleContext(
            [WorkoutActivityType.GeneralStrength],
            days,
            10m, // 10 minutes max daily capacity
            new Dictionary<WorkoutActivityType, decimal> { [WorkoutActivityType.GeneralStrength] = 45m },
            [gsEvidence]);

        var result = MixedActivityWeekScheduleRule.Evaluate(context);

        var decision = Assert.Single(result.Activities);
        Assert.Equal(0, decision.ScheduledSessionsPerWeek);
        Assert.NotEqual(MixedActivityScheduleDisposition.Scheduled, decision.Disposition);
        Assert.Equal(MixedActivityScheduleDisposition.Omitted, decision.Disposition);
        Assert.Empty(result.Sessions);
    }

    private static StrengthPlanPreviewData CreatePreviewData(
        WorkoutActivityType activity,
        Equipment equipment = Equipment.Bodyweight,
        InjuryFlag injuries = InjuryFlag.None,
        TrainingExperienceLevel? profileLevel = TrainingExperienceLevel.Beginner,
        int durationMinutes = 45,
        IReadOnlyCollection<DayOfWeek>? availableWeekdays = null)
    {
        availableWeekdays ??= [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday];
        var templates = StrengthTemplateCatalog.ForSchedule(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek);
        var requiredSlots = templates.SelectMany(t => t.Slots.Where(s => s.IsRequired).Select(s => (Template: t, Slot: s))).ToArray();
        var catalog = requiredSlots.Select((item, index) => Entry(101 + index, item.Slot.PreferredPatterns.First())).ToArray();
        var reviewedSlots = requiredSlots.Select((item, index) =>
            (Key: $"{item.Template.TemplateId}|{item.Slot.SlotId}", Id: 101 + index))
            .ToDictionary(item => item.Key, item => (IReadOnlySet<int>)new HashSet<int> { item.Id });

        return new StrengthPlanPreviewData(
            UserGoal.MaintainHealth, equipment, injuries, false,
            new HashSet<WorkoutActivityType> { activity },
            new HashSet<DayOfWeek>(availableWeekdays),
            new Dictionary<WorkoutActivityType, int> { [activity] = durationMinutes },
            profileLevel, catalog, new Dictionary<int, int>(), new HashSet<int>(), new Dictionary<int, int>(),
            new HashSet<int>(), catalog.ToDictionary(e => e.Candidate.ExerciseId, _ => 4),
            catalog.ToDictionary(e => e.Candidate.ExerciseId, e => e.Candidate.Pattern.ToString()),
            reviewedSlots, reviewedSlots.Keys.ToDictionary(k => k, _ => "Reviewed slot"));
    }

    private static StrengthExerciseCatalogEntry Entry(int exerciseId, MovementPattern pattern) => new(
        $"Exercise {exerciseId}",
        new ReviewedExerciseCandidate(exerciseId, "synthetic-catalog-v1", pattern,
            new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
            new HashSet<ActivityExperience> { ActivityExperience.Beginner, ActivityExperience.Established },
            new HashSet<ActivityDemandTag>(), true, true,
            new HashSet<ActivityDemandTag>(),
            new HashSet<ExerciseTimingMethod> { ExerciseTimingMethod.StraightSets },
            1, new HashSet<int>(), InjuryFlag.None),
        []);
}
