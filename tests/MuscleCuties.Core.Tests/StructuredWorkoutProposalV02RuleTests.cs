using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Domain.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class StructuredWorkoutProposalV02RuleTests
{
    private const string PolicyVersion = "synthetic-policy-v1";
    private const string ReviewReference = "synthetic-review-fixture";
    private const string TemplateId = "synthetic-template";
    private const string StrengthRuleId = "synthetic-strength";
    private const string CardioRuleId = "synthetic-cardio";
    private const decimal SessionMinutes = 20m;
    private const decimal DailyMinutes = 60m;
    private const decimal InsufficientMinutes = 10m;
    private const int SyntheticExerciseId = 1;
    private const int StrengthSets = 3;
    private const decimal ActiveMinutesPerSet = 5m;
    private const decimal RestMinutesPerSet = 2m;
    private const decimal SetupMinutes = 1m;
    private const int WeekLength = 7;
    private const int MorningHour = 8;
    private const int EveningHour = 18;
    private const int WindowMinutes = 25;
    private const int SingleWindowMinutes = 30;
    private static readonly DateOnly Monday = new(2026, 10, 19);
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlanId = Guid.Parse("f0fd4a53-d45c-4c3c-8f52-8325460cf99a");

    [Fact]
    public void Missing_Or_Unapproved_Policy_Fails_Closed()
    {
        var input = Input();
        var missing = StructuredWorkoutProposalV02Rule.Generate(input, null);
        var unapproved = StructuredWorkoutProposalV02Rule.Generate(input,
            Policy() with { Approval = Policy().Approval with { CoveredSections = new HashSet<PlanningReviewSection>() } });

        Assert.Equal(StructuredProposalStatusV02.NeedsProgrammingReview, missing.Status);
        Assert.Contains(StructuredProposalReasonV02.MissingPolicy, missing.Reasons);
        Assert.Null(missing.Plan);
        Assert.Equal(StructuredProposalStatusV02.NeedsProgrammingReview, unapproved.Status);
        Assert.Null(unapproved.Plan);
    }

    [Fact]
    public void Seven_Available_Days_Use_Only_Reviewed_Template_Sessions()
    {
        var first = StructuredWorkoutProposalV02Rule.Generate(Input(), Policy());
        var again = StructuredWorkoutProposalV02Rule.Generate(Input(), Policy());

        Assert.Equal(StructuredProposalStatusV02.Proposed, first.Status);
        Assert.NotNull(first.Plan);
        Assert.Equal(WeekLength, first.Plan.Days.Count);
        Assert.Equal(2, first.Plan.Days.Sum(day => day.Sessions.Count));
        Assert.Equal(5, first.Plan.Days.Count(day => day.IsRestDay));
        Assert.Equal(first.Plan.ProposalId, again.Plan!.ProposalId);
        Assert.Equal(first.Plan.Days.SelectMany(day => day.Sessions).Select(session => session.Id),
            again.Plan.Days.SelectMany(day => day.Sessions).Select(session => session.Id));
    }

    [Fact]
    public void Two_Different_Sessions_Can_Share_One_Day()
    {
        var policy = Policy() with
        {
            Templates = [Template(
                Pattern(DayOfWeek.Monday, SessionPurposeV02.Strength,
                    ActivityModalityV02.Strength, StrengthRuleId, ActivityDemandTag.UpperBody),
                Pattern(DayOfWeek.Monday, SessionPurposeV02.Conditioning,
                    ActivityModalityV02.Cycling, CardioRuleId, ActivityDemandTag.Conditioning))]
        };
        var result = StructuredWorkoutProposalV02Rule.Generate(Input(), policy);

        Assert.Equal(StructuredProposalStatusV02.Proposed, result.Status);
        Assert.Equal(2, result.Plan!.Days[0].Sessions.Count);
        Assert.Equal(40m, result.DayChecks[0].PlannedSessionMinutes);
    }

    [Fact]
    public void Separate_Morning_And_Evening_Windows_Do_Not_Count_The_Gap()
    {
        var input = Input() with
        {
            Availability = Windows(
                new ActivityWindow(new TimeOnly(MorningHour, 0),
                    new TimeOnly(MorningHour, WindowMinutes)),
                new ActivityWindow(new TimeOnly(EveningHour, 0),
                    new TimeOnly(EveningHour, WindowMinutes)))
        };
        var result = StructuredWorkoutProposalV02Rule.Generate(input, TwoSessionPolicy());

        Assert.Equal(StructuredProposalStatusV02.Proposed, result.Status);
        Assert.Equal(new TimeOnly(MorningHour, 0), result.Plan!.Days[0].Sessions[0].Start);
        Assert.Equal(new TimeOnly(EveningHour, 0), result.Plan.Days[0].Sessions[1].Start);
        Assert.Equal(40m, result.DayChecks[0].OccupiedMinutes);
    }

    [Fact]
    public void Sessions_That_Do_Not_Fit_The_Window_Return_A_Blocked_Reason()
    {
        var input = Input() with
        {
            Availability = Windows(new ActivityWindow(new TimeOnly(MorningHour, 0),
                new TimeOnly(MorningHour, SingleWindowMinutes)))
        };
        var result = StructuredWorkoutProposalV02Rule.Generate(input, TwoSessionPolicy());

        Assert.Equal(StructuredProposalStatusV02.Blocked, result.Status);
        Assert.Contains(StructuredProposalReasonV02.DailyWindowConflict, result.Reasons);
        Assert.Null(result.Plan);
    }

    [Fact]
    public void Time_Restrictions_And_Current_Symptoms_Override_Template()
    {
        var insufficient = StructuredWorkoutProposalV02Rule.Generate(Input(InsufficientMinutes), Policy());
        var excluded = StructuredWorkoutProposalV02Rule.Generate(Input() with
        {
            ExcludedModalities = new HashSet<ActivityModalityV02> { ActivityModalityV02.Strength }
        }, Policy());
        var symptoms = StructuredWorkoutProposalV02Rule.Generate(Input() with
        {
            HasCurrentPainOrConcerningSymptoms = true
        }, Policy());

        Assert.Equal(StructuredProposalStatusV02.Limited, insufficient.Status);
        Assert.Contains(StructuredProposalReasonV02.DailyTimeExceeded, insufficient.Reasons);
        Assert.Equal(StructuredProposalStatusV02.Blocked, excluded.Status);
        Assert.Contains(StructuredProposalReasonV02.ExplicitRestrictionConflict, excluded.Reasons);
        Assert.Equal(StructuredProposalStatusV02.Blocked, symptoms.Status);
        Assert.Contains(StructuredProposalReasonV02.CurrentSymptomsNeedReviewedPathway, symptoms.Reasons);
    }

    [Fact]
    public void Outside_Climbing_Requires_Demand_Review()
    {
        var climbing = new PlannedDailyActivity("climbing-entry", Monday, SessionMinutes,
            new HashSet<ActivityDemandTag> { ActivityDemandTag.Pulling }, true,
            true, true, false, null, null);
        var policy = Policy() with { Templates = [Template(
            Pattern(DayOfWeek.Monday, SessionPurposeV02.Strength,
                ActivityModalityV02.Strength, StrengthRuleId, ActivityDemandTag.Pulling))] };

        var result = StructuredWorkoutProposalV02Rule.Generate(Input() with
        {
            OutsideActivities = [climbing]
        }, policy);

        Assert.Equal(StructuredProposalStatusV02.NeedsDemandReview, result.Status);
        Assert.Contains(StructuredProposalReasonV02.DemandOverlapNeedsReview, result.Reasons);
    }

    [Fact]
    public void Yoga_Can_Be_A_Vigorous_Skill_Session()
    {
        var yoga = Pattern(DayOfWeek.Monday, SessionPurposeV02.Skill,
            ActivityModalityV02.Yoga, "synthetic-yoga", ActivityDemandTag.Mobility) with
        {
            Intensity = IntendedIntensityV02.Vigorous
        };
        var result = StructuredWorkoutProposalV02Rule.Generate(Input(),
            Policy() with { Templates = [Template(yoga)] });

        Assert.Equal(StructuredProposalStatusV02.Proposed, result.Status);
        Assert.Equal(SessionPurposeV02.Skill, result.Plan!.Days[0].Sessions[0].Purpose);
        Assert.Equal(IntendedIntensityV02.Vigorous, result.Plan.Days[0].Sessions[0].Intensity);
    }

    [Fact]
    public void Missing_Eligible_Strength_Exercise_Blocks_Generation()
    {
        var policy = Policy() with { ExerciseCatalog = [] };
        var result = StructuredWorkoutProposalV02Rule.Generate(Input(), policy);

        Assert.Equal(StructuredProposalStatusV02.NoEligibleTemplate, result.Status);
        Assert.Contains(StructuredProposalReasonV02.NoEligibleExercise, result.Reasons);
        Assert.Null(result.Plan);
    }

    [Fact]
    public void Proposal_Id_Changes_With_Expected_Revision_And_Input_Snapshot()
    {
        var initial = StructuredWorkoutProposalV02Rule.Generate(Input(), Policy());
        var newerRevision = StructuredWorkoutProposalV02Rule.Generate(Input() with
        {
            ExpectedRevisionId = Guid.Parse("3e6ac9eb-2e15-42ba-8fca-c1bf4e1b9879")
        }, Policy());
        var differentAvailability = StructuredWorkoutProposalV02Rule.Generate(
            Input(DailyMinutes - 1m), Policy());

        Assert.Equal(StructuredProposalStatusV02.Proposed, initial.Status);
        Assert.Equal(StructuredProposalStatusV02.Proposed, newerRevision.Status);
        Assert.Equal(StructuredProposalStatusV02.Proposed, differentAvailability.Status);
        Assert.NotEqual(initial.Plan!.ProposalId, newerRevision.Plan!.ProposalId);
        Assert.NotEqual(initial.Plan.ProposalId, differentAvailability.Plan!.ProposalId);
    }

    [Fact]
    public void Malformed_Outside_Activity_Returns_Invalid_Input_Not_Window_Conflict()
    {
        var malformed = new PlannedDailyActivity("", Monday, SessionMinutes,
            new HashSet<ActivityDemandTag> { ActivityDemandTag.Conditioning }, true,
            true, true, false, null, null);
        var result = StructuredWorkoutProposalV02Rule.Generate(Input() with
        {
            OutsideActivities = [malformed]
        }, Policy());

        Assert.Equal(StructuredProposalStatusV02.InvalidInput, result.Status);
        Assert.Contains(StructuredProposalReasonV02.InvalidSchedule, result.Reasons);
    }

    private static StructuredPlanInputV02 Input(decimal dailyMinutes = DailyMinutes) => new(
        PlanId, Guid.Empty, Monday, UserGoal.Strength, TrainingExperienceLevel.Beginner,
        Enumerable.Range(0, WeekLength).Select(offset =>
            new DailyActivityAvailability(Monday.AddDays(offset), dailyMinutes, [])).ToArray(),
        [], new HashSet<ActivityModalityV02>(), new HashSet<ActivityDemandTag>(), false, Now,
        new ExerciseEligibilityContext(
            new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
            TrainingExperienceLevel.Beginner, new HashSet<int>(),
            new HashSet<MovementPattern>(), new HashSet<ActivityDemandTag>(), false));

    private static ReviewedWorkoutPolicyV02 Policy() => new(
        PolicyVersion, TodayCheckInScoreRule.RulesetVersion, Now.AddDays(-1), null,
        new PlanningReviewApproval(TodayCheckInScoreRule.RulesetVersion, ReviewReference,
            Now.AddDays(-1), new HashSet<PlanningReviewSection>
            {
                PlanningReviewSection.TrainingPrescriptions
            }, PolicyVersion),
        [Template(
            Pattern(DayOfWeek.Monday, SessionPurposeV02.Strength,
                ActivityModalityV02.Strength, StrengthRuleId, ActivityDemandTag.UpperBody),
            Pattern(DayOfWeek.Thursday, SessionPurposeV02.Conditioning,
                ActivityModalityV02.Cycling, CardioRuleId, ActivityDemandTag.Conditioning))],
        [new ReviewedExerciseCandidate(SyntheticExerciseId, "synthetic-catalog-v1",
            MovementPattern.HorizontalPush,
            new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
            TrainingExperienceLevel.Beginner,
            new HashSet<ActivityDemandTag> { ActivityDemandTag.UpperBody }, true, true,
            new HashSet<ActivityDemandTag>(),
            new HashSet<ExerciseTimingMethod> { ExerciseTimingMethod.StraightSets },
            SetupMinutes, new HashSet<int>())]);

    private static ReviewedWorkoutPolicyV02 TwoSessionPolicy() => Policy() with
    {
        Templates = [Template(
            Pattern(DayOfWeek.Monday, SessionPurposeV02.Strength,
                ActivityModalityV02.Strength, StrengthRuleId, ActivityDemandTag.UpperBody),
            Pattern(DayOfWeek.Monday, SessionPurposeV02.Conditioning,
                ActivityModalityV02.Cycling, CardioRuleId, ActivityDemandTag.Conditioning))]
    };

    private static IReadOnlyList<DailyActivityAvailability> Windows(params ActivityWindow[] mondayWindows) =>
        Enumerable.Range(0, WeekLength).Select(offset =>
            new DailyActivityAvailability(Monday.AddDays(offset), DailyMinutes,
                offset == 0 ? mondayWindows : [])).ToArray();

    private static ReviewedWeekTemplateV02 Template(params ReviewedSessionPatternV02[] sessions) =>
        new(TemplateId, UserGoal.Strength, TrainingExperienceLevel.Beginner, sessions);

    private static ReviewedSessionPatternV02 Pattern(DayOfWeek day, SessionPurposeV02 purpose,
        ActivityModalityV02 modality, string ruleId, ActivityDemandTag demand) =>
        new(day, purpose, modality, IntendedIntensityV02.Moderate, 0m, 0m,
            purpose == SessionPurposeV02.Strength
                ? [SessionBlockV02.StraightSets([new ExercisePrescriptionV02(
                    SyntheticExerciseId, StrengthSets, 8, 12, ActiveMinutesPerSet,
                    ExplicitSessionDurationRule.RestIntervalsBetweenSets(StrengthSets),
                    RestMinutesPerSet, SetupMinutes, false,
                    ActiveTimeCoverage.BothSidesIncluded, null, null)])]
                : [SessionBlockV02.Timed(SessionMinutes, 0m, ruleId)],
            new HashSet<ActivityDemandTag> { demand }, true, ruleId);
}
