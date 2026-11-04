using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class GoalSpecificAccessoryPriorityRuleTests
{
    private const int TierAExerciseId = 101;
    private const int TierBExerciseId = 102;
    private const int TierCExerciseId = 103;
    private const int OtherGoalExerciseId = 104;
    private const int UnweightedExerciseId = 105;
    private const int ZeroContributionExerciseId = 106;
    private const int TierAWeightId = 1;
    private const int TierBWeightId = 2;
    private const int TierCWeightId = 3;
    private const int OtherGoalWeightId = 4;
    private const int TierAContributionId = 1;
    private const int TierBContributionId = 2;
    private const int TierCContributionId = 3;
    private const int OtherGoalContributionId = 4;
    private const int UnweightedContributionId = 5;
    private const int ZeroContributionId = 6;
    private const int TierAMuscleGroupId = 11;
    private const int TierBMuscleGroupId = 12;
    private const int TierCMuscleGroupId = 13;
    private const int OtherGoalMuscleGroupId = 14;
    private const int UnweightedMuscleGroupId = 15;
    private const double PositiveContribution = 0.25;
    private const double NoContribution = 0;

    [Fact]
    public void Assigns_A_B_C_Order_And_Leaves_Unweighted_Exercises_Neutral()
    {
        var weights = new PlanningGoalTierWeight[]
        {
            new(TierAWeightId, UserGoal.Strength, TierAMuscleGroupId, PriorityTier.A),
            new(TierBWeightId, UserGoal.Strength, TierBMuscleGroupId, PriorityTier.B),
            new(TierCWeightId, UserGoal.Strength, TierCMuscleGroupId, PriorityTier.C),
            new(OtherGoalWeightId, UserGoal.MaintainHealth, OtherGoalMuscleGroupId, PriorityTier.A)
        };
        var contributions = new PlanningMuscleContribution[]
        {
            new(TierAContributionId, TierAExerciseId, TierAMuscleGroupId, PositiveContribution),
            new(TierBContributionId, TierBExerciseId, TierBMuscleGroupId, PositiveContribution),
            new(TierCContributionId, TierCExerciseId, TierCMuscleGroupId, PositiveContribution),
            new(OtherGoalContributionId, OtherGoalExerciseId, OtherGoalMuscleGroupId, PositiveContribution),
            new(UnweightedContributionId, UnweightedExerciseId, UnweightedMuscleGroupId, PositiveContribution),
            new(ZeroContributionId, ZeroContributionExerciseId, TierAMuscleGroupId, NoContribution)
        };

        var priorities = GoalSpecificAccessoryPriorityRule.Resolve(UserGoal.Strength,
            contributions, weights);

        Assert.Equal(TierAScore, priorities[TierAExerciseId]);
        Assert.Equal(TierBScore, priorities[TierBExerciseId]);
        Assert.Equal(TierCScore, priorities[TierCExerciseId]);
        Assert.DoesNotContain(OtherGoalExerciseId, priorities.Keys);
        Assert.DoesNotContain(UnweightedExerciseId, priorities.Keys);
        Assert.DoesNotContain(ZeroContributionExerciseId, priorities.Keys);
    }

    [Fact]
    public void Missing_Goal_Tier_Rows_Produces_No_Priority_Entries()
    {
        var priorities = GoalSpecificAccessoryPriorityRule.Resolve(UserGoal.Strength,
            Array.Empty<PlanningMuscleContribution>(), Array.Empty<PlanningGoalTierWeight>());

        Assert.Empty(priorities);
    }

    private const int TierAScore = 3;
    private const int TierBScore = 2;
    private const int TierCScore = 1;
}
