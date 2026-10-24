using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>
/// Maps reviewed goal-tier rows and positive exercise muscle contributions to accessory ordering scores.
/// Tier A scores above B, and B above C; exercises with no matching weighted contribution are omitted.
/// </summary>
public static class GoalSpecificAccessoryPriorityRule
{
    private const int InvalidIdentifier = 0;
    private const int NoPriorityScore = 0;
    private const int TierAScore = 3;
    private const int TierBScore = 2;
    private const int TierCScore = 1;

    public static IReadOnlyDictionary<int, int> Resolve(
        UserGoal goal,
        IReadOnlyList<PlanningMuscleContribution>? muscleContributions,
        IReadOnlyList<PlanningGoalTierWeight>? goalTierWeights)
    {
        if (!Enum.IsDefined(goal) || muscleContributions is null || goalTierWeights is null)
            return new Dictionary<int, int>();

        var tierScoreByMuscleGroup = new Dictionary<int, int>();
        foreach (var weight in goalTierWeights)
        {
            if (weight is null || weight.Goal != goal || weight.MuscleGroupId <= InvalidIdentifier)
                continue;

            var score = ScoreFor(weight.Tier);
            if (score == NoPriorityScore)
                continue;

            if (!tierScoreByMuscleGroup.TryGetValue(weight.MuscleGroupId, out var currentScore) ||
                score > currentScore)
                tierScoreByMuscleGroup[weight.MuscleGroupId] = score;
        }

        var scoreByExercise = new Dictionary<int, int>();
        foreach (var contribution in muscleContributions)
        {
            if (contribution is null || contribution.ExerciseDefinitionId <= InvalidIdentifier ||
                contribution.MuscleGroupId <= InvalidIdentifier || !double.IsFinite(contribution.Fraction) ||
                contribution.Fraction <= 0 ||
                !tierScoreByMuscleGroup.TryGetValue(contribution.MuscleGroupId, out var score))
                continue;

            if (!scoreByExercise.TryGetValue(contribution.ExerciseDefinitionId, out var currentScore) ||
                score > currentScore)
                scoreByExercise[contribution.ExerciseDefinitionId] = score;
        }

        return scoreByExercise;
    }

    private static int ScoreFor(PriorityTier tier) => tier switch
    {
        PriorityTier.A => TierAScore,
        PriorityTier.B => TierBScore,
        PriorityTier.C => TierCScore,
        _ => NoPriorityScore
    };
}
