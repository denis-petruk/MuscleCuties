using MuscleCuties.Core.Domain.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum TimingAssumptionV02
{
    ActiveMinutesPerSet,
    ActivityMinutesPerRound,
    MovingPace,
    MovementMinutesPerRepetition
}

public sealed record SessionDurationV02Result(Guid SessionId, PlannedSessionState State,
    DurationEstimate Estimate, decimal? ActualMinutes,
    IReadOnlySet<TimingAssumptionV02> Assumptions);

public sealed record DayDurationV02Result(
    decimal? PlannedSessionMinutes,
    decimal? CompletedActualMinutes,
    decimal? OccupiedMinutes,
    IReadOnlyList<SessionDurationV02Result> Sessions,
    DailyCapacityResult Capacity);

/// <summary>Calculates distinct blocks and checks the complete day. All time assumptions are supplied.</summary>
public static class StructuredWorkoutDurationV02Rule
{
    public static DurationEstimate EvaluateBlock(SessionBlockV02? block)
    {
        if (block is null)
            return Unavailable(DurationIssue.MissingBlock);

        switch (block.Kind)
        {
            case SessionBlockKindV02.StraightSets:
                if (block.Exercises.Count == 0 || block.Exercises.Any(exercise => exercise is null))
                    return Unavailable(DurationIssue.MissingBlock);
                return Sum(block.Exercises.Select(EvaluateExercise).ToArray());
            case SessionBlockKindV02.Superset or SessionBlockKindV02.Circuit:
                if (block.Exercises.Count == 0 || block.Rounds is null)
                    return Unavailable(DurationIssue.MissingBlock);
                // Round timing already includes child work and rests; never add exercise timing twice.
                return ExplicitSessionDurationRule.Rounds(block.Rounds);
            case SessionBlockKindV02.Timed:
                return ExplicitSessionDurationRule.Timed(block.MainMinutes, block.TransitionMinutes);
            case SessionBlockKindV02.Intervals:
                return EvaluateIntervals(block.Intervals);
            case SessionBlockKindV02.DistanceSets:
                return EvaluateDistance(block.Distance);
            case SessionBlockKindV02.MovementSequence:
                if (block.Movements.Count == 0 || block.Movements.Any(movement => movement is null))
                    return Unavailable(DurationIssue.MissingBlock);
                return Sum(block.Movements.Select(EvaluateMovement).ToArray());
            default:
                return Unavailable(DurationIssue.InvalidInput);
        }
    }

    public static DurationEstimate EvaluateSession(PlannedSessionV02? session)
    {
        if (session is null || session.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(session.CanonicalActivityId) ||
            !Enum.IsDefined(session.Purpose) || !Enum.IsDefined(session.Modality) ||
            !Enum.IsDefined(session.Intensity) || !Enum.IsDefined(session.State) ||
            session.Blocks.Count == 0 ||
            session.Blocks.Any(block => block is null) ||
            session.Start.HasValue != session.End.HasValue ||
            session.Start.HasValue && session.Start >= session.End ||
            session.ActualMinutes is < 0 ||
            session.IsCompleted != session.ActualMinutes.HasValue)
            return Unavailable(DurationIssue.InvalidInput);

        return ExplicitSessionDurationRule.Session(session.WarmupMinutes,
            session.Blocks.Select(EvaluateBlock).ToArray(), session.CooldownMinutes);
    }

    public static DayDurationV02Result EvaluateDay(PlanDayV02? day)
    {
        if (day is null || day.Sessions is null || day.OutsideActivities is null ||
            day.Availability is null || day.Availability.Date != day.Date ||
            day.Sessions.Any(session => session is null || session.Date != day.Date) ||
            day.Sessions.Select(session => session.Id).Distinct().Count() != day.Sessions.Count)
            return InvalidDay();

        var estimates = day.Sessions.Select(session =>
            new SessionDurationV02Result(session.Id, session.State, EvaluateSession(session),
                session.ActualMinutes,
                GetAssumptions(session))).ToArray();

        decimal? planned = null;
        decimal? completed = null;
        try
        {
            if (estimates.Where((_, index) => !day.Sessions[index].IsSkipped)
                .All(item => item.Estimate.IsAvailable))
                planned = estimates.Where((_, index) => !day.Sessions[index].IsSkipped)
                    .Sum(item => item.Estimate.Minutes!.Value);
            if (day.Sessions.Where(session => session.IsCompleted).All(session => session.ActualMinutes.HasValue))
                completed = day.Sessions.Where(session => session.IsCompleted)
                    .Sum(session => session.ActualMinutes!.Value);
        }
        catch (OverflowException)
        {
            return InvalidDay();
        }

        var activityRows = day.Sessions.Select((session, index) => (session, index))
            .Where(item => !item.session.IsSkipped)
            .Select(item => new PlannedDailyActivity(
                item.session.CanonicalActivityId, day.Date,
                item.session.IsCompleted ? item.session.ActualMinutes : estimates[item.index].Estimate.Minutes,
                item.session.DemandTags, item.session.DemandTagsReviewed,
                IsOutsideActivity: false, item.session.IsCompleted, IsOptional: false,
                item.session.Start, item.session.End))
            .Concat(day.OutsideActivities)
            .ToArray();
        var capacity = DailyActivityCapacityRule.Evaluate(day.Availability, activityRows);
        return new DayDurationV02Result(planned, completed, capacity.PlannedMinutes, estimates, capacity);
    }

    private static DurationEstimate EvaluateExercise(ExercisePrescriptionV02 exercise)
    {
        if (exercise.ExerciseId <= 0 || exercise.Sets <= 0 || exercise.RestIntervals < 0 ||
            exercise.RestIntervals > exercise.Sets || exercise.MinimumRepetitions is < 0 ||
            exercise.MaximumRepetitions is < 0 ||
            exercise.MinimumRepetitions > exercise.MaximumRepetitions ||
            exercise.Load is < 0 || exercise.Load.HasValue && string.IsNullOrWhiteSpace(exercise.LoadUnit))
            return Unavailable(DurationIssue.InvalidInput);

        return ExplicitSessionDurationRule.StraightSets(new StraightSetDurationInput(
            exercise.Sets, exercise.ActiveMinutesPerSet, exercise.RestIntervals,
            exercise.RestMinutes, exercise.SetupAndTransitionMinutes, exercise.IsUnilateral,
            exercise.ActiveTimeCoverage));
    }

    private static DurationEstimate EvaluateIntervals(ActivityIntervalV02? interval)
    {
        if (interval is null || interval.WorkMinutes is null || interval.RecoveryMinutes is null ||
            interval.BetweenBlockRecoveryMinutes is null || interval.TransitionMinutes is null)
            return Unavailable(DurationIssue.MissingInput);
        if (interval.Repetitions <= 0 || interval.Blocks <= 0 ||
            interval.RecoveryIntervalsPerBlock < 0 ||
            interval.RecoveryIntervalsPerBlock > interval.Repetitions ||
            interval.WorkMinutes < 0 || interval.RecoveryMinutes < 0 ||
            interval.BetweenBlockRecoveryMinutes < 0 || interval.TransitionMinutes < 0)
            return Unavailable(DurationIssue.InvalidInput);

        try
        {
            var minutes = interval.Blocks * interval.Repetitions * interval.WorkMinutes.Value +
                          interval.Blocks * interval.RecoveryIntervalsPerBlock * interval.RecoveryMinutes.Value +
                          ExplicitSessionDurationRule.RestIntervalsBetweenSets(interval.Blocks) *
                          interval.BetweenBlockRecoveryMinutes.Value + interval.TransitionMinutes.Value;
            return Available(minutes);
        }
        catch (OverflowException)
        {
            return Unavailable(DurationIssue.InvalidInput);
        }
    }

    private static DurationEstimate EvaluateDistance(DistanceSetV02? distance)
    {
        if (distance is null || distance.MinutesPerDistanceUnit is null ||
            distance.RestMinutes is null || distance.TransitionMinutes is null)
            return Unavailable(DurationIssue.MissingInput);
        if (distance.DistancePerRepetition <= 0 || distance.Repetitions <= 0 ||
            string.IsNullOrWhiteSpace(distance.DistanceUnit) ||
            distance.MinutesPerDistanceUnit <= 0 || distance.RestIntervals < 0 ||
            distance.RestIntervals > distance.Repetitions || distance.RestMinutes < 0 ||
            distance.TransitionMinutes < 0 || distance.PoolLength is <= 0 ||
            distance.PoolLength.HasValue && string.IsNullOrWhiteSpace(distance.PoolLengthUnit))
            return Unavailable(DurationIssue.InvalidInput);

        try
        {
            var minutes = distance.Repetitions * distance.DistancePerRepetition *
                          distance.MinutesPerDistanceUnit.Value +
                          distance.RestIntervals * distance.RestMinutes.Value +
                          distance.TransitionMinutes.Value;
            return Available(minutes);
        }
        catch (OverflowException)
        {
            return Unavailable(DurationIssue.InvalidInput);
        }
    }

    private static DurationEstimate EvaluateMovement(MovementPrescriptionV02 movement)
    {
        if (string.IsNullOrWhiteSpace(movement.MovementCode) ||
            movement.Repetitions is <= 0 || movement.MinutesPerRepetition is < 0 ||
            movement.HoldMinutes is < 0 || movement.TransitionMinutes is < 0 ||
            movement.HoldMinutes.HasValue &&
            (movement.Repetitions.HasValue || movement.MinutesPerRepetition.HasValue) ||
            movement.Repetitions.HasValue != movement.MinutesPerRepetition.HasValue)
            return Unavailable(DurationIssue.InvalidInput);
        if (movement.TransitionMinutes is null ||
            movement.HoldMinutes is null && movement.Repetitions is null)
            return Unavailable(DurationIssue.MissingInput);

        try
        {
            var activity = movement.HoldMinutes ??
                movement.Repetitions!.Value * movement.MinutesPerRepetition!.Value;
            return Available(activity + movement.TransitionMinutes.Value);
        }
        catch (OverflowException)
        {
            return Unavailable(DurationIssue.InvalidInput);
        }
    }

    private static DurationEstimate Sum(IReadOnlyList<DurationEstimate> estimates) =>
        ExplicitSessionDurationRule.Session(0m, estimates, 0m);

    private static HashSet<TimingAssumptionV02> GetAssumptions(PlannedSessionV02 session)
    {
        var assumptions = new HashSet<TimingAssumptionV02>();
        foreach (var block in session.Blocks)
        {
            if (block is null)
                continue;
            switch (block.Kind)
            {
                case SessionBlockKindV02.StraightSets:
                    assumptions.Add(TimingAssumptionV02.ActiveMinutesPerSet);
                    break;
                case SessionBlockKindV02.Superset or SessionBlockKindV02.Circuit:
                    assumptions.Add(TimingAssumptionV02.ActivityMinutesPerRound);
                    break;
                case SessionBlockKindV02.DistanceSets:
                    assumptions.Add(TimingAssumptionV02.MovingPace);
                    break;
                case SessionBlockKindV02.MovementSequence:
                    if (block.Movements.Any(movement => movement is not null &&
                                                        movement.Repetitions.HasValue))
                        assumptions.Add(TimingAssumptionV02.MovementMinutesPerRepetition);
                    break;
            }
        }

        return assumptions;
    }

    private static DurationEstimate Available(decimal minutes) => new(minutes, []);
    private static DurationEstimate Unavailable(DurationIssue issue) => new(null, [issue]);

    private static DayDurationV02Result InvalidDay() =>
        new(null, null, null, [], DailyActivityCapacityRule.Evaluate(null, null));
}
