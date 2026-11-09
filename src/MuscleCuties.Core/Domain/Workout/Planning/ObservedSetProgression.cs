namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>
/// A persisted working set used by double progression. RIR and technique are required:
/// a week with an unknown RIR is incomplete and must not be treated as a qualifying week.
/// </summary>
public sealed record ObservedSetHistoryRow(
    Guid SessionId,
    DateTime CompletedAt,
    int SetNumber,
    int RepsCompleted,
    decimal LoadKg,
    int? RirActual,
    bool TechniqueAcceptable = true,
    bool IsWarmup = false);

public static class ObservedSetProgressionHistoryBuilder
{
    public const int RequiredConsecutiveWeeks = StrengthProgressionRule.TwoConsecutiveWeeksForDoubleProgression;

    /// <summary>
    /// Builds the two most recent qualifying weeks for one exercise. A session with any
    /// missing RIR or no working sets is omitted rather than filled with aggregate values, and it
    /// does not displace older complete sessions: skipped or incomplete logs preserve prior valid
    /// progress instead of counting as a result.
    /// </summary>
    public static IReadOnlyList<DoubleProgressionWeekPerformance> Build(IEnumerable<ObservedSetHistoryRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var completeSessions = rows
            .GroupBy(row => row.SessionId)
            .Select(group => new
            {
                CompletedAt = group.Max(row => row.CompletedAt),
                WorkingSets = group
                    .Where(set => !set.IsWarmup && set.RepsCompleted > 0)
                    .OrderBy(set => set.SetNumber)
                    .ToList()
            })
            // Do not silently drop a set with a missing RIR: that would turn an incomplete
            // week into a fabricated qualifying week.
            .Where(session => session.WorkingSets.Count > 0 &&
                              session.WorkingSets.All(set => set.RirActual.HasValue))
            .OrderByDescending(session => session.CompletedAt)
            .Take(RequiredConsecutiveWeeks)
            .OrderBy(session => session.CompletedAt)
            .ToList();

        var weeks = new List<DoubleProgressionWeekPerformance>(completeSessions.Count);
        foreach (var session in completeSessions)
        {
            weeks.Add(new DoubleProgressionWeekPerformance(
                weeks.Count + 1,
                session.WorkingSets.Select(set => new CompletedWorkingSet(
                    set.RepsCompleted,
                    set.LoadKg,
                    set.RirActual!.Value,
                    set.TechniqueAcceptable)).ToArray()));
        }

        return weeks;
    }
}

public sealed record StrengthProgressionAdvice(
    string RecommendationCode,
    decimal? PrescribedLoad,
    bool LoadIncreased,
    MajorMuscleGroup? Muscle = null,
    EffectiveExperienceTier? Tier = null);

public static class StrengthProgressionAdviceRule
{
    public const string StartingWeightCode = "Workout_Recommendation_StartingWeight";
    public const string EnduranceCode = "Workout_Recommendation_Endurance";
    public const string LogEachSetCode = "Workout_Recommendation_LogEachSet";
    public const string EarnedCode = "Workout_Recommendation_EarnedFormat";
    public const string MaintainCode = "Workout_Recommendation_MaintainFormat";
    public const string ReduceCode = "Workout_Recommendation_ReduceFormat";

    public static StrengthProgressionAdvice StartingWeight() =>
        new(StartingWeightCode, null, LoadIncreased: false);

    public static StrengthProgressionAdvice Endurance() =>
        new(EnduranceCode, null, LoadIncreased: false);

    public static StrengthProgressionAdvice LogEachSet() =>
        new(LogEachSetCode, null, LoadIncreased: false);

    /// <summary>
    /// Resolves a double-progression prescription from real per-set weeks, the exercise's
    /// real major muscle group, and the user's effective experience tier. No aggregate or
    /// synthetic history is used here.
    /// </summary>
    public static StrengthProgressionAdvice Resolve(
        MajorMuscleGroup muscle,
        int exerciseId,
        decimal currentLoad,
        int plannedReps,
        EffectiveExperienceTier tier,
        IReadOnlyList<DoubleProgressionWeekPerformance>? weeks)
    {
        if (currentLoad <= 0m)
            return StartingWeight();

        if (weeks is null || weeks.Count < ObservedSetProgressionHistoryBuilder.RequiredConsecutiveWeeks)
            return LogEachSet();

        var category = plannedReps is > 0 and <= StrengthProgressionRule.CompoundRepMaximum
            ? ExerciseMovementCategory.Compound
            : ExerciseMovementCategory.Accessory;
        var increment = category == ExerciseMovementCategory.Compound ? 2.5m : 1.25m;
        var currentSets = weeks[weeks.Count - 1].WorkingSets.Count;
        var previousSets = weeks[weeks.Count - 2].WorkingSets.Count;

        var decisions = StrengthProgressionRule.ResolveDoubleProgression(
            tier,
            [
                new MuscleProgressionCandidate(
                    muscle,
                    exerciseId,
                    currentLoad,
                    increment,
                    category,
                    weeks,
                    CurrentWeeklyVolume: currentSets,
                    PreviousWeeklyVolume: previousSets)
            ]);

        if (decisions.Count == 0)
            return LogEachSet();

        var decision = decisions[0];

        if (decision.LoadIncreased)
            return new StrengthProgressionAdvice(EarnedCode, decision.PrescribedLoad, LoadIncreased: true, muscle, tier);

        if (decision.ProgressionStatus == StrengthProgressionRule.StatusRegressed)
            return new StrengthProgressionAdvice(ReduceCode, decision.PrescribedLoad, LoadIncreased: false, muscle, tier);

        if (decision.ReasonCode == StrengthProgressionRule.IncompleteHistoryMaintainsLoad)
            return LogEachSet();

        return new StrengthProgressionAdvice(MaintainCode, decision.PrescribedLoad, LoadIncreased: false, muscle, tier);
    }
}
