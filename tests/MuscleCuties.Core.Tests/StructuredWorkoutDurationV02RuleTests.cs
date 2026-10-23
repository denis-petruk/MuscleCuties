using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class StructuredWorkoutDurationV02RuleTests
{
    private const int Sets = 3;
    private const decimal ActiveMinutes = 1m;
    private const decimal RestMinutes = 2m;
    private const decimal SetupMinutes = 1m;
    private const decimal WarmupMinutes = 5m;
    private const decimal CooldownMinutes = 3m;
    private const decimal CardioMinutes = 20m;
    private const decimal CardioTransitionMinutes = 2m;
    private const decimal DailyCapacityMinutes = 60m;
    private const decimal StrengthMinutes = 16m;
    private const decimal CombinedMinutes = 38m;
    private const int WorkRepetitions = 4;
    private const int WorkBlocks = 2;
    private const int RecoveryIntervalsWithoutFinal = 3;
    private const int RecoveryIntervalsWithFinal = 4;
    private const decimal WorkIntervalMinutes = 1m;
    private const decimal RecoveryIntervalMinutes = 2m;
    private const decimal BetweenBlockMinutes = 3m;
    private const decimal IntervalTransitionMinutes = 1m;
    private const decimal WithoutFinalRecoveryMinutes = 24m;
    private const decimal WithFinalRecoveryMinutes = 28m;
    private const int MorningHour = 8;
    private const int CompletedSessionWindowMinutes = 30;
    private static readonly DateOnly SampleDate = new(2026, 10, 23);

    [Fact]
    public void Strength_And_Cardio_On_One_Day_Are_Counted_Once()
    {
        var strength = Session(SessionPurposeV02.Strength, ActivityModalityV02.Strength,
            [SessionBlockV02.StraightSets([Exercise()])], WarmupMinutes, CooldownMinutes,
            new HashSet<ActivityDemandTag> { ActivityDemandTag.LowerBody });
        var cardio = Session(SessionPurposeV02.Conditioning, ActivityModalityV02.Cycling,
            [SessionBlockV02.Timed(CardioMinutes, CardioTransitionMinutes)], 0m, 0m,
            new HashSet<ActivityDemandTag> { ActivityDemandTag.Conditioning });

        var result = StructuredWorkoutDurationV02Rule.EvaluateDay(Day([strength, cardio]));

        Assert.Equal(StrengthMinutes, result.Sessions[0].Estimate.Minutes);
        Assert.Equal(CombinedMinutes, result.PlannedSessionMinutes);
        Assert.Equal(CombinedMinutes, result.OccupiedMinutes);
        Assert.Equal(DailyCapacityStatus.Feasible, result.Capacity.Status);
    }

    [Fact]
    public void Swimming_Distance_Requires_Pace_And_Does_Not_Convert_Without_It()
    {
        const decimal distance = 50m;
        const decimal paceMinutesPerMeter = 0.04m;
        const decimal expectedSwimmingMinutes = 6m;
        var unknown = SessionBlockV02.DistanceSets(new DistanceSetV02(distance, 2,
            "meters", null, 1, 1m, 1m, 25m, "meters"));
        var known = SessionBlockV02.DistanceSets(new DistanceSetV02(distance, 2,
            "meters", paceMinutesPerMeter, 1, 1m, 1m, 25m, "meters"));
        var mobility = Session(SessionPurposeV02.Mobility, ActivityModalityV02.Mobility,
            [SessionBlockV02.Timed(10m, 0m)], 0m, 0m,
            new HashSet<ActivityDemandTag> { ActivityDemandTag.Mobility });

        Assert.Contains(DurationIssue.MissingInput,
            StructuredWorkoutDurationV02Rule.EvaluateBlock(unknown).Issues);
        var swimming = Session(SessionPurposeV02.Skill, ActivityModalityV02.Swimming,
            [known], 0m, 0m,
            new HashSet<ActivityDemandTag> { ActivityDemandTag.Conditioning });
        var result = StructuredWorkoutDurationV02Rule.EvaluateDay(Day([swimming, mobility]));

        Assert.Equal(expectedSwimmingMinutes, result.Sessions[0].Estimate.Minutes);
        Assert.Contains(TimingAssumptionV02.MovingPace, result.Sessions[0].Assumptions);
        Assert.Equal(16m, result.PlannedSessionMinutes);
    }

    [Fact]
    public void Interval_Recovery_Count_Is_Explicit_And_Final_Recovery_Is_Optional()
    {
        var withoutFinal = SessionBlockV02.IntervalTraining(new ActivityIntervalV02(
            WorkRepetitions, WorkBlocks, WorkIntervalMinutes, RecoveryIntervalMinutes,
            RecoveryIntervalsWithoutFinal, BetweenBlockMinutes, IntervalTransitionMinutes));
        var withFinal = SessionBlockV02.IntervalTraining(new ActivityIntervalV02(
            WorkRepetitions, WorkBlocks, WorkIntervalMinutes, RecoveryIntervalMinutes,
            RecoveryIntervalsWithFinal, BetweenBlockMinutes, IntervalTransitionMinutes));

        Assert.Equal(WithoutFinalRecoveryMinutes,
            StructuredWorkoutDurationV02Rule.EvaluateBlock(withoutFinal).Minutes);
        Assert.Equal(WithFinalRecoveryMinutes,
            StructuredWorkoutDurationV02Rule.EvaluateBlock(withFinal).Minutes);
    }

    [Fact]
    public void Hiit_UsesThreeRecoveriesAndTransitionsDirectlyToCooldown()
    {
        var hiit = Session(SessionPurposeV02.Conditioning, ActivityModalityV02.Cycling,
            [SessionBlockV02.IntervalTraining(new ActivityIntervalV02(
                WorkoutTrainingPolicyV020.HiitWorkIntervals, 1, 0.5m, 1.5m,
                WorkoutTrainingPolicyV020.HiitRecoveryIntervals, 0m, 0m), "stationary-cycling-hiit")],
            WorkoutTrainingPolicyV020.HiitWarmupMinutes, WorkoutTrainingPolicyV020.HiitCooldownMinutes,
            new HashSet<ActivityDemandTag> { ActivityDemandTag.Conditioning }, IntendedIntensityV02.Vigorous);

        var estimate = StructuredWorkoutDurationV02Rule.EvaluateSession(hiit);
        Assert.Equal(WorkoutTrainingPolicyV020.HiitSessionDuration,
            TimeSpan.FromMinutes((double)estimate.Minutes!.Value));
    }

    [Fact]
    public void Yoga_Modality_Does_Not_Force_Recovery_Purpose_Or_Easy_Intensity()
    {
        var yoga = Session(SessionPurposeV02.Skill, ActivityModalityV02.Yoga,
            [SessionBlockV02.Timed(30m, 2m)], 0m, 0m,
            new HashSet<ActivityDemandTag> { ActivityDemandTag.Mobility },
            IntendedIntensityV02.Vigorous);

        Assert.Equal(SessionPurposeV02.Skill, yoga.Purpose);
        Assert.Equal(IntendedIntensityV02.Vigorous, yoga.Intensity);
        Assert.Equal(32m, StructuredWorkoutDurationV02Rule.EvaluateSession(yoga).Minutes);
    }

    [Fact]
    public void Completed_Climbing_Consumes_Actual_Time_And_Flags_Pulling_Overlap()
    {
        var strength = Session(SessionPurposeV02.Strength, ActivityModalityV02.Strength,
            [SessionBlockV02.StraightSets([Exercise()])], WarmupMinutes, CooldownMinutes,
            new HashSet<ActivityDemandTag> { ActivityDemandTag.Pulling });
        var climbing = new PlannedDailyActivity("climbing-entry", SampleDate, 35m,
            new HashSet<ActivityDemandTag> { ActivityDemandTag.Pulling, ActivityDemandTag.Grip },
            true, true, true, false, null, null);

        var result = StructuredWorkoutDurationV02Rule.EvaluateDay(Day([strength], [climbing]));

        Assert.Equal(DailyCapacityStatus.NeedsReview, result.Capacity.Status);
        Assert.Contains(DailyCapacityReason.OutsideActivityDemandOverlap, result.Capacity.Reasons);
        Assert.Equal(51m, result.Capacity.PlannedMinutes);
        Assert.Equal(51m, result.OccupiedMinutes);
    }

    [Fact]
    public void Duplicate_Outside_Import_Is_Counted_Once()
    {
        var outside = new PlannedDailyActivity("swim-log", SampleDate, 20m,
            new HashSet<ActivityDemandTag> { ActivityDemandTag.Conditioning },
            true, true, true, false, null, null);
        var result = StructuredWorkoutDurationV02Rule.EvaluateDay(Day([], [outside, outside]));

        Assert.Equal(20m, result.Capacity.PlannedMinutes);
        Assert.Single(result.Capacity.DistinctActivities);
    }

    [Fact]
    public void Superset_Uses_Round_Rests_Without_Double_Counting_Child_Rests()
    {
        const decimal expectedMinutes = 13m;
        var round = new RoundBlockDurationInput(3, 2m, 0.5m, 2m, 1.5m);
        var block = SessionBlockV02.Superset(round, [Exercise(), Exercise()]);

        Assert.Equal(expectedMinutes, StructuredWorkoutDurationV02Rule.EvaluateBlock(block).Minutes);
    }

    [Fact]
    public void Completed_Session_Uses_Actual_Time_For_Daily_Capacity_Without_Changing_Plan()
    {
        const decimal actualMinutes = 30m;
        var completed = new PlannedSessionV02(Guid.NewGuid(), "completed-strength", SampleDate,
            SessionPurposeV02.Strength, ActivityModalityV02.Strength, IntendedIntensityV02.Moderate,
            WarmupMinutes, CooldownMinutes, [SessionBlockV02.StraightSets([Exercise()])],
            new HashSet<ActivityDemandTag> { ActivityDemandTag.LowerBody }, true,
            state: PlannedSessionState.Completed, actualMinutes: actualMinutes);
        var result = StructuredWorkoutDurationV02Rule.EvaluateDay(Day([completed]));

        Assert.Equal(StrengthMinutes, result.PlannedSessionMinutes);
        Assert.Equal(actualMinutes, result.CompletedActualMinutes);
        Assert.Equal(actualMinutes, result.OccupiedMinutes);
        Assert.Equal(actualMinutes, result.Capacity.PlannedMinutes);
    }

    [Fact]
    public void Skipped_Session_Does_Not_Consume_Time_Or_Create_Catch_Up_Work()
    {
        var skipped = Session(SessionPurposeV02.Strength, ActivityModalityV02.Strength,
            [SessionBlockV02.StraightSets([Exercise()])], WarmupMinutes, CooldownMinutes,
            new HashSet<ActivityDemandTag> { ActivityDemandTag.LowerBody }).Skip();
        var result = StructuredWorkoutDurationV02Rule.EvaluateDay(Day([skipped]));

        Assert.Equal(0m, result.PlannedSessionMinutes);
        Assert.Equal(0m, result.OccupiedMinutes);
        Assert.Empty(result.Capacity.DistinctActivities);
        Assert.True(Day([skipped]).IsRestDay);
    }

    [Fact]
    public void Completed_Work_Cannot_Be_Skipped_Or_Rescheduled()
    {
        var completed = new PlannedSessionV02(Guid.NewGuid(), "completed-strength", SampleDate,
            SessionPurposeV02.Strength, ActivityModalityV02.Strength, IntendedIntensityV02.Moderate,
            WarmupMinutes, CooldownMinutes, [SessionBlockV02.StraightSets([Exercise()])],
            new HashSet<ActivityDemandTag> { ActivityDemandTag.LowerBody }, true,
            state: PlannedSessionState.Completed, actualMinutes: StrengthMinutes);

        Assert.Throws<InvalidOperationException>(() => completed.Skip());
        Assert.Throws<InvalidOperationException>(() => completed.Schedule(
            new TimeOnly(MorningHour, 0),
            new TimeOnly(MorningHour, CompletedSessionWindowMinutes)));
    }

    [Fact]
    public void Ordered_Movement_Blocks_Include_Holds_Repetitions_And_Transitions()
    {
        const decimal holdMinutes = 2m;
        const decimal transitionMinutes = 0.5m;
        const int movementRepetitions = 4;
        const decimal minutesPerRepetition = 0.25m;
        const decimal expectedMinutes = 4m;
        var movements = SessionBlockV02.MovementSequence(
        [
            new MovementPrescriptionV02("synthetic-hold", null, null,
                holdMinutes, transitionMinutes),
            new MovementPrescriptionV02("synthetic-repetitions", movementRepetitions,
                minutesPerRepetition, null, transitionMinutes)
        ]);

        Assert.Equal(expectedMinutes, StructuredWorkoutDurationV02Rule.EvaluateBlock(movements).Minutes);
    }

    private static ExercisePrescriptionV02 Exercise() => new(
        1, Sets, 8, 12, ActiveMinutes,
        ExplicitSessionDurationRule.RestIntervalsBetweenSets(Sets), RestMinutes,
        SetupMinutes, false, ActiveTimeCoverage.BothSidesIncluded, null, null);

    private static PlannedSessionV02 Session(SessionPurposeV02 purpose, ActivityModalityV02 modality,
        IReadOnlyList<SessionBlockV02> blocks, decimal? warmup, decimal? cooldown,
        IReadOnlySet<ActivityDemandTag> demand, IntendedIntensityV02 intensity = IntendedIntensityV02.Moderate) =>
        new(Guid.NewGuid(), Guid.NewGuid().ToString(), SampleDate, purpose, modality, intensity,
            warmup, cooldown, blocks, demand, true);

    private static PlanDayV02 Day(IReadOnlyList<PlannedSessionV02> sessions,
        IReadOnlyList<PlannedDailyActivity>? outside = null) =>
        new(SampleDate, new DailyActivityAvailability(SampleDate, DailyCapacityMinutes, []),
            sessions, outside ?? []);
}
