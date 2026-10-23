namespace MuscleCuties.Core.Domain.Workout;

/// <summary>Describes whether one set's active time already includes both sides.</summary>
public enum ActiveTimeCoverage
{
    BothSidesIncluded,
    PerSide
}

public enum DurationIssue
{
    MissingInput,
    InvalidInput,
    MissingBlock
}

public sealed class DurationEstimate
{
    internal DurationEstimate(decimal? minutes, IReadOnlyList<DurationIssue> issues)
    {
        Minutes = minutes;
        Issues = issues;
    }

    public decimal? Minutes { get; }
    public IReadOnlyList<DurationIssue> Issues { get; }
    public bool IsAvailable => Minutes.HasValue && Issues.Count == 0;
}

public sealed record StraightSetDurationInput(
    int Sets,
    decimal? ActiveMinutesPerSet,
    int PlannedRestIntervals,
    decimal? MinutesPerRestInterval,
    decimal? SetupAndTransitionMinutes,
    bool IsUnilateral,
    ActiveTimeCoverage? ActiveTimeCoverage);

public sealed record RoundBlockDurationInput(
    int Rounds,
    decimal? ActivityMinutesPerRound,
    decimal? IntraRoundRestMinutesPerRound,
    decimal? InterRoundRestMinutes,
    decimal? TransitionMinutes);

/// <summary>
/// Calculates planned work only. Callers supply each distinct block once; actual elapsed time is separate.
/// Missing timing data never becomes zero, and this rule supplies no unreviewed timing defaults.
/// </summary>
public static class ExplicitSessionDurationRule
{
    private const int SidesInUnilateralSet = 2;

    public static int RestIntervalsBetweenSets(int sets) => sets <= 1 ? 0 : sets - 1;

    public static DurationEstimate StraightSets(StraightSetDurationInput? input)
    {
        if (input is null)
            return Unavailable(DurationIssue.MissingInput);

        if (input.ActiveMinutesPerSet is null || input.MinutesPerRestInterval is null ||
            input.SetupAndTransitionMinutes is null || input.IsUnilateral && input.ActiveTimeCoverage is null)
            return Unavailable(DurationIssue.MissingInput);

        if (input.Sets <= 0 || input.PlannedRestIntervals < 0 ||
            input.ActiveMinutesPerSet < 0 || input.MinutesPerRestInterval < 0 ||
            input.SetupAndTransitionMinutes < 0 ||
            input.IsUnilateral && !Enum.IsDefined(input.ActiveTimeCoverage!.Value))
            return Unavailable(DurationIssue.InvalidInput);

        try
        {
            var sides = input.IsUnilateral && input.ActiveTimeCoverage == ActiveTimeCoverage.PerSide
                ? SidesInUnilateralSet
                : 1;
            var minutes = input.Sets * input.ActiveMinutesPerSet.Value * sides +
                          input.PlannedRestIntervals * input.MinutesPerRestInterval.Value +
                          input.SetupAndTransitionMinutes.Value;
            return Available(minutes);
        }
        catch (OverflowException)
        {
            return Unavailable(DurationIssue.InvalidInput);
        }
    }

    /// <summary>Round activity includes every child exercise; no child rest is added again.</summary>
    public static DurationEstimate Rounds(RoundBlockDurationInput? input)
    {
        if (input is null || input.ActivityMinutesPerRound is null ||
            input.IntraRoundRestMinutesPerRound is null || input.InterRoundRestMinutes is null ||
            input.TransitionMinutes is null)
            return Unavailable(DurationIssue.MissingInput);

        if (input.Rounds <= 0 || input.ActivityMinutesPerRound < 0 ||
            input.IntraRoundRestMinutesPerRound < 0 || input.InterRoundRestMinutes < 0 ||
            input.TransitionMinutes < 0)
            return Unavailable(DurationIssue.InvalidInput);

        try
        {
            var minutes = input.Rounds * (input.ActivityMinutesPerRound.Value +
                                          input.IntraRoundRestMinutesPerRound.Value) +
                          RestIntervalsBetweenSets(input.Rounds) * input.InterRoundRestMinutes.Value +
                          input.TransitionMinutes.Value;
            return Available(minutes);
        }
        catch (OverflowException)
        {
            return Unavailable(DurationIssue.InvalidInput);
        }
    }

    public static DurationEstimate Timed(decimal? mainMinutes, decimal? transitionMinutes)
    {
        if (mainMinutes is null || transitionMinutes is null)
            return Unavailable(DurationIssue.MissingInput);
        if (mainMinutes < 0 || transitionMinutes < 0)
            return Unavailable(DurationIssue.InvalidInput);

        try
        {
            return Available(mainMinutes.Value + transitionMinutes.Value);
        }
        catch (OverflowException)
        {
            return Unavailable(DurationIssue.InvalidInput);
        }
    }

    public static DurationEstimate Session(decimal? warmupMinutes, IReadOnlyList<DurationEstimate>? blocks,
        decimal? cooldownMinutes)
    {
        if (warmupMinutes is null || cooldownMinutes is null || blocks is null)
            return Unavailable(DurationIssue.MissingInput);
        if (warmupMinutes < 0 || cooldownMinutes < 0)
            return Unavailable(DurationIssue.InvalidInput);
        if (blocks.Any(block => block is null))
            return Unavailable(DurationIssue.MissingBlock);
        if (blocks.Any(block => !block.IsAvailable))
            return new DurationEstimate(null, blocks.SelectMany(block => block.Issues).Distinct().ToArray());

        try
        {
            return Available(warmupMinutes.Value + blocks.Sum(block => block.Minutes!.Value) + cooldownMinutes.Value);
        }
        catch (OverflowException)
        {
            return Unavailable(DurationIssue.InvalidInput);
        }
    }

    private static DurationEstimate Available(decimal minutes) => new(minutes, []);

    private static DurationEstimate Unavailable(DurationIssue issue) => new(null, [issue]);
}
