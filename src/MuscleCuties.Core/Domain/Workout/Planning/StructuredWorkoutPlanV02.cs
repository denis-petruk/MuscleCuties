using MuscleCuties.Core.Domain.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum SessionPurposeV02 { Strength, Conditioning, Mobility, Recovery, Skill, UserActivity, Climbing, Yoga, Rest }
public enum ActivityModalityV02 { Strength, Walking, Running, Cycling, Elliptical, Rowing, Swimming, Yoga, Mobility, Climbing, Other }
public enum IntendedIntensityV02 { Unspecified, Easy, Moderate, Vigorous }
public enum SessionTrainingStyleV02 { Unspecified, General, HighVolume, HighIntensity, Continuous, Intervals, Hiit, Gentle, Demanding }
public enum SessionBlockKindV02 { StraightSets, Superset, Circuit, Timed, Intervals, DistanceSets, MovementSequence }

public sealed record ExercisePrescriptionV02(
    int ExerciseId,
    int Sets,
    int? MinimumRepetitions,
    int? MaximumRepetitions,
    decimal? ActiveMinutesPerSet,
    int RestIntervals,
    decimal? RestMinutes,
    decimal? SetupAndTransitionMinutes,
    bool IsUnilateral,
    ActiveTimeCoverage? ActiveTimeCoverage,
    decimal? Load,
    string? LoadUnit);

public sealed record ActivityIntervalV02(
    int Repetitions,
    int Blocks,
    decimal? WorkMinutes,
    decimal? RecoveryMinutes,
    int RecoveryIntervalsPerBlock,
    decimal? BetweenBlockRecoveryMinutes,
    decimal? TransitionMinutes);

public sealed record DistanceSetV02(
    decimal DistancePerRepetition,
    int Repetitions,
    string DistanceUnit,
    decimal? MinutesPerDistanceUnit,
    int RestIntervals,
    decimal? RestMinutes,
    decimal? TransitionMinutes,
    decimal? PoolLength,
    string? PoolLengthUnit,
    string? StrokeOrDrillCode = null);

public sealed record MovementPrescriptionV02(
    string MovementCode,
    int? Repetitions,
    decimal? MinutesPerRepetition,
    decimal? HoldMinutes,
    decimal? TransitionMinutes);

public sealed class SessionBlockV02
{
    private SessionBlockV02(SessionBlockKindV02 kind, IReadOnlyList<ExercisePrescriptionV02> exercises,
        RoundBlockDurationInput? rounds, decimal? mainMinutes, decimal? transitionMinutes,
        ActivityIntervalV02? intervals, DistanceSetV02? distance,
        IReadOnlyList<MovementPrescriptionV02> movements, string? activityCode)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        ArgumentNullException.ThrowIfNull(movements);
        Kind = kind;
        Exercises = Array.AsReadOnly(exercises.ToArray());
        Rounds = rounds;
        MainMinutes = mainMinutes;
        TransitionMinutes = transitionMinutes;
        Intervals = intervals;
        Distance = distance;
        Movements = Array.AsReadOnly(movements.ToArray());
        ActivityCode = activityCode;
    }

    public SessionBlockKindV02 Kind { get; }
    public IReadOnlyList<ExercisePrescriptionV02> Exercises { get; }
    public RoundBlockDurationInput? Rounds { get; }
    public decimal? MainMinutes { get; }
    public decimal? TransitionMinutes { get; }
    public ActivityIntervalV02? Intervals { get; }
    public DistanceSetV02? Distance { get; }
    public IReadOnlyList<MovementPrescriptionV02> Movements { get; }
    public string? ActivityCode { get; }

    public static SessionBlockV02 StraightSets(IReadOnlyList<ExercisePrescriptionV02> exercises) =>
        new(SessionBlockKindV02.StraightSets, exercises, null, null, null, null, null, [], null);

    public static SessionBlockV02 Superset(RoundBlockDurationInput rounds,
        IReadOnlyList<ExercisePrescriptionV02> exercises) =>
        new(SessionBlockKindV02.Superset, exercises, rounds, null, null, null, null, [], null);

    public static SessionBlockV02 Circuit(RoundBlockDurationInput rounds,
        IReadOnlyList<ExercisePrescriptionV02> exercises) =>
        new(SessionBlockKindV02.Circuit, exercises, rounds, null, null, null, null, [], null);

    public static SessionBlockV02 Timed(decimal? mainMinutes, decimal? transitionMinutes,
        string? activityCode = null) =>
        new(SessionBlockKindV02.Timed, [], null, mainMinutes, transitionMinutes, null, null,
            [], activityCode);

    public static SessionBlockV02 IntervalTraining(ActivityIntervalV02 intervals,
        string? activityCode = null) =>
        new(SessionBlockKindV02.Intervals, [], null, null, null, intervals, null,
            [], activityCode);

    public static SessionBlockV02 DistanceSets(DistanceSetV02 distance,
        string? activityCode = null) =>
        new(SessionBlockKindV02.DistanceSets, [], null, null, null, null, distance,
            [], activityCode);

    public static SessionBlockV02 MovementSequence(IReadOnlyList<MovementPrescriptionV02> movements) =>
        new(SessionBlockKindV02.MovementSequence, [], null, null, null, null, null,
            movements, null);
}

public sealed class PlannedSessionV02
{
    public PlannedSessionV02(Guid id, string canonicalActivityId, DateOnly date, SessionPurposeV02 purpose,
        ActivityModalityV02 modality, IntendedIntensityV02 intensity, decimal? warmupMinutes,
        decimal? cooldownMinutes, IReadOnlyList<SessionBlockV02> blocks,
        IReadOnlySet<ActivityDemandTag> demandTags, bool demandTagsReviewed,
        TimeOnly? start = null, TimeOnly? end = null,
        PlannedSessionState state = PlannedSessionState.Planned,
        decimal? actualMinutes = null,
        SessionTrainingStyleV02 trainingStyle = SessionTrainingStyleV02.Unspecified)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(demandTags);
        Id = id;
        CanonicalActivityId = canonicalActivityId;
        Date = date;
        Purpose = purpose;
        Modality = modality;
        Intensity = intensity;
        TrainingStyle = trainingStyle;
        WarmupMinutes = warmupMinutes;
        CooldownMinutes = cooldownMinutes;
        Blocks = Array.AsReadOnly(blocks.ToArray());
        DemandTags = demandTags.ToHashSet();
        DemandTagsReviewed = demandTagsReviewed;
        Start = start;
        End = end;
        State = state;
        ActualMinutes = actualMinutes;
    }

    public Guid Id { get; }
    public string CanonicalActivityId { get; }
    public DateOnly Date { get; }
    public SessionPurposeV02 Purpose { get; }
    public ActivityModalityV02 Modality { get; }
    public IntendedIntensityV02 Intensity { get; }
    public SessionTrainingStyleV02 TrainingStyle { get; }
    public decimal? WarmupMinutes { get; }
    public decimal? CooldownMinutes { get; }
    public IReadOnlyList<SessionBlockV02> Blocks { get; }
    public IReadOnlySet<ActivityDemandTag> DemandTags { get; }
    public bool DemandTagsReviewed { get; }
    public TimeOnly? Start { get; }
    public TimeOnly? End { get; }
    public PlannedSessionState State { get; }
    public bool IsCompleted => State is PlannedSessionState.Completed or PlannedSessionState.PartiallyCompleted;
    public bool IsSkipped => State == PlannedSessionState.Skipped;
    public decimal? ActualMinutes { get; }

    public PlannedSessionV02 Schedule(TimeOnly start, TimeOnly end)
    {
        if (State != PlannedSessionState.Planned)
            throw new InvalidOperationException("Only a planned session can be scheduled.");
        return new(Id, CanonicalActivityId, Date, Purpose, Modality, Intensity, WarmupMinutes,
            CooldownMinutes, Blocks, DemandTags, DemandTagsReviewed, start, end,
            State, ActualMinutes, TrainingStyle);
    }

    public PlannedSessionV02 Skip()
    {
        if (State != PlannedSessionState.Planned)
            throw new InvalidOperationException("Only a planned session can be skipped.");
        return new(Id, CanonicalActivityId, Date, Purpose, Modality, Intensity, WarmupMinutes,
            CooldownMinutes, Blocks, DemandTags, DemandTagsReviewed, Start, End,
            PlannedSessionState.Skipped, trainingStyle: TrainingStyle);
    }
}

public sealed record PlanDayV02(DateOnly Date, DailyActivityAvailability Availability,
    IReadOnlyList<PlannedSessionV02> Sessions, IReadOnlyList<PlannedDailyActivity> OutsideActivities)
{
    public bool IsRestDay => Sessions.All(session => session.IsSkipped) &&
                             OutsideActivities.Count == 0;
}

public sealed record WeeklyPlanV02(Guid PlanId, Guid ProposalId, Guid ExpectedRevisionId,
    string RulesetVersion,
    string TrainingPolicyVersion, IReadOnlyList<PlanDayV02> Days,
    IReadOnlyList<string> CatalogVersions);
