using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class StrengthPlanContinuityRuleTests
{
    private const int TwoSets = 2;
    private const int NoWorkingSets = 0;
    private const int RehabMuscleId = 0;
    private const int PrimaryMuscleId = 12;
    private const int RehabExerciseId = 101;
    private const int StrengthExerciseId = 202;
    private const int SecondRehabExerciseId = 303;

    [Fact]
    public void Rehab_only_session_is_withheld()
    {
        var decision = StrengthPlanContinuityRule.Evaluate([Rehab(RehabExerciseId), Rehab(SecondRehabExerciseId)]);

        Assert.False(decision.IsApplicable);
        Assert.Equal(0, decision.StrengthExerciseCount);
        Assert.Equal(StrengthPlanContinuityRule.SessionWithheldNoApplicableStrength, decision.ReasonCode);
    }

    [Fact]
    public void Strength_with_added_rehab_keeps_the_strength_work()
    {
        var decision = StrengthPlanContinuityRule.Evaluate(
            [Strength(StrengthExerciseId, TwoSets), Rehab(RehabExerciseId)]);

        Assert.True(decision.IsApplicable);
        Assert.Equal(1, decision.StrengthExerciseCount);
        Assert.Equal(TwoSets, decision.WorkingSets);
        Assert.Equal(StrengthPlanContinuityRule.StrengthPreserved, decision.ReasonCode);
    }

    [Fact]
    public void Empty_session_is_withheld()
    {
        Assert.False(StrengthPlanContinuityRule.Evaluate([]).IsApplicable);
        Assert.False(StrengthPlanContinuityRule.Evaluate(null).IsApplicable);
        Assert.False(StrengthPlanContinuityRule.HasStrengthWork([]));
    }

    [Fact]
    public void Strength_without_working_sets_is_withheld()
    {
        var decision = StrengthPlanContinuityRule.Evaluate([Strength(StrengthExerciseId, NoWorkingSets)]);

        Assert.False(decision.IsApplicable);
        Assert.Equal(StrengthPlanContinuityRule.SessionWithheldNoApplicableStrength, decision.ReasonCode);
    }

    [Fact]
    public void EnsureApplicable_refuses_a_rehab_only_session_but_keeps_a_strength_session()
    {
        var rehabOnly = new PrescribedSession { Exercises = [Rehab(RehabExerciseId), Rehab(SecondRehabExerciseId)] };
        var withStrength = new PrescribedSession
        {
            Exercises = [Strength(StrengthExerciseId, TwoSets), Rehab(RehabExerciseId)]
        };

        Assert.Null(StrengthPlanContinuityRule.EnsureApplicable(rehabOnly));
        Assert.Same(withStrength, StrengthPlanContinuityRule.EnsureApplicable(withStrength));
        Assert.Null(StrengthPlanContinuityRule.EnsureApplicable(null));
    }

    private static PickedExercise Strength(int exerciseId, int sets) => new()
    {
        ExerciseId = exerciseId,
        PrimaryMuscleId = PrimaryMuscleId,
        Sets = sets
    };

    private static PickedExercise Rehab(int exerciseId) => new()
    {
        ExerciseId = exerciseId,
        PrimaryMuscleId = RehabMuscleId,
        Sets = TwoSets,
        Droppable = true
    };
}
