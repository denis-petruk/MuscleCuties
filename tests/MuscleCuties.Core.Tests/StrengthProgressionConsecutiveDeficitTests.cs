using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// OD-6 (owner option b): a load decrease needs two consecutive comparable, fully logged
/// under-target sessions. Skipped and incomplete sessions never count toward the streak.
/// </summary>
public sealed class StrengthProgressionConsecutiveDeficitTests
{
    private const decimal CurrentLoad = 60m;
    private const decimal CompoundIncrement = 2.5m;
    private const int CompoundPlannedReps = 10;
    private const int DeficitReps = 4;
    private const int InRangeReps = 10;
    private const int TopReps = 12;
    private const int TargetRir = StrengthProgressionRule.DefaultTargetRepsInReserve;
    private const int WorkingSetsPerSession = 2;
    private static readonly DateTime FirstSession = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void One_under_target_session_holds_the_load()
    {
        var decision = Decide(Week(1, InRangeReps), Week(2, DeficitReps));

        Assert.Equal(CurrentLoad, decision.PrescribedLoad);
        Assert.False(decision.LoadIncreased);
        Assert.Equal(StrengthProgressionRule.StatusMaintained, decision.ProgressionStatus);
        Assert.Equal(StrengthProgressionRule.SingleUnderTargetSessionHoldsLoad, decision.ReasonCode);
    }

    [Fact]
    public void An_earlier_single_deficit_followed_by_a_success_holds_the_load()
    {
        var decision = Decide(Week(1, DeficitReps), Week(2, InRangeReps));

        Assert.Equal(CurrentLoad, decision.PrescribedLoad);
        Assert.Equal(StrengthProgressionRule.StatusMaintained, decision.ProgressionStatus);
    }

    [Fact]
    public void Two_consecutive_under_target_sessions_decrease_the_load_by_one_step()
    {
        var decision = Decide(Week(1, DeficitReps), Week(2, DeficitReps));

        Assert.Equal(CurrentLoad - CompoundIncrement, decision.PrescribedLoad);
        Assert.Equal(StrengthProgressionRule.StatusRegressed, decision.ProgressionStatus);
        Assert.Equal(StrengthProgressionRule.RepOrRirDeficitRequiresLoadRegression, decision.ReasonCode);
    }

    [Fact]
    public void Effort_harder_than_target_twice_in_a_row_decreases_the_load()
    {
        var decision = Decide(
            Week(1, InRangeReps, rir: TargetRir - 1),
            Week(2, InRangeReps, rir: TargetRir - 1));

        Assert.Equal(StrengthProgressionRule.StatusRegressed, decision.ProgressionStatus);
    }

    [Fact]
    public void A_week_without_working_sets_is_not_an_under_target_result()
    {
        var decision = Decide(Week(1, DeficitReps), new DoubleProgressionWeekPerformance(2, []));

        Assert.Equal(CurrentLoad, decision.PrescribedLoad);
        Assert.NotEqual(StrengthProgressionRule.StatusRegressed, decision.ProgressionStatus);
    }

    [Fact]
    public void Deficit_success_deficit_from_persisted_sets_holds_the_load()
    {
        var rows = Session(0, DeficitReps).Concat(Session(1, InRangeReps)).Concat(Session(2, DeficitReps));

        var advice = Advise(rows);

        Assert.Equal(StrengthProgressionAdviceRule.MaintainCode, advice.RecommendationCode);
        Assert.Equal(CurrentLoad, advice.PrescribedLoad);
    }

    [Fact]
    public void Two_consecutive_deficits_from_persisted_sets_reduce_the_load()
    {
        var rows = Session(0, InRangeReps).Concat(Session(1, DeficitReps)).Concat(Session(2, DeficitReps));

        var advice = Advise(rows);

        Assert.Equal(StrengthProgressionAdviceRule.ReduceCode, advice.RecommendationCode);
        Assert.Equal(CurrentLoad - CompoundIncrement, advice.PrescribedLoad);
    }

    [Fact]
    public void An_incomplete_log_between_two_deficits_neither_counts_nor_breaks_the_streak()
    {
        var rows = Session(0, DeficitReps)
            .Concat(Session(1, InRangeReps, rir: null))
            .Concat(Session(2, DeficitReps));

        var weeks = ObservedSetProgressionHistoryBuilder.Build(rows);
        var advice = Advise(rows);

        Assert.Equal(2, weeks.Count);
        Assert.All(weeks, week => Assert.All(week.WorkingSets, set => Assert.Equal(DeficitReps, set.RepsCompleted)));
        Assert.Equal(StrengthProgressionAdviceRule.ReduceCode, advice.RecommendationCode);
    }

    [Fact]
    public void A_skipped_session_with_no_working_sets_does_not_count_as_a_deficit()
    {
        var rows = Session(0, InRangeReps)
            .Concat(Session(1, DeficitReps))
            .Concat(Session(2, repsPerSet: 0));

        var advice = Advise(rows);

        Assert.Equal(StrengthProgressionAdviceRule.MaintainCode, advice.RecommendationCode);
        Assert.Equal(CurrentLoad, advice.PrescribedLoad);
    }

    [Fact]
    public void Two_qualifying_workouts_then_an_incomplete_log_still_earn_the_smallest_step()
    {
        var rows = Session(0, TopReps).Concat(Session(1, TopReps)).Concat(Session(2, TopReps, rir: null));

        var advice = Advise(rows);

        Assert.Equal(StrengthProgressionAdviceRule.EarnedCode, advice.RecommendationCode);
        Assert.True(advice.LoadIncreased);
        Assert.Equal(CurrentLoad + CompoundIncrement, advice.PrescribedLoad);
    }

    [Fact]
    public void A_single_complete_deficit_session_never_reduces_the_load()
    {
        var advice = Advise(Session(0, DeficitReps));

        Assert.Equal(StrengthProgressionAdviceRule.LogEachSetCode, advice.RecommendationCode);
        Assert.Null(advice.PrescribedLoad);
    }

    private static MuscleProgressionDecision Decide(params DoubleProgressionWeekPerformance[] weeks) =>
        Assert.Single(StrengthProgressionRule.ResolveDoubleProgression(
            EffectiveExperienceTier.Tier1BeginnerOrNew,
            [
                new MuscleProgressionCandidate(MajorMuscleGroup.Quads, ExerciseId: 124, CurrentLoad, CompoundIncrement,
                    ExerciseMovementCategory.Compound, weeks, WorkingSetsPerSession, WorkingSetsPerSession)
            ]));

    private static StrengthProgressionAdvice Advise(IEnumerable<ObservedSetHistoryRow> rows) =>
        StrengthProgressionAdviceRule.Resolve(MajorMuscleGroup.Quads, exerciseId: 124, CurrentLoad,
            CompoundPlannedReps, EffectiveExperienceTier.Tier1BeginnerOrNew,
            ObservedSetProgressionHistoryBuilder.Build(rows.ToArray()));

    private static DoubleProgressionWeekPerformance Week(int number, int reps, int rir = TargetRir) =>
        new(number, Enumerable.Range(0, WorkingSetsPerSession)
            .Select(_ => new CompletedWorkingSet(reps, CurrentLoad, rir)).ToArray());

    private static IEnumerable<ObservedSetHistoryRow> Session(int index, int repsPerSet, int? rir = TargetRir)
    {
        var sessionId = Guid.NewGuid();
        var completedAt = FirstSession.AddDays(index * 7);
        return Enumerable.Range(1, WorkingSetsPerSession)
            .Select(setNumber => new ObservedSetHistoryRow(sessionId, completedAt, setNumber, repsPerSet,
                CurrentLoad, rir))
            .ToArray();
    }
}
