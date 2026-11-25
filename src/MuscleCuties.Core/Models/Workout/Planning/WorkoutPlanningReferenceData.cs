using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Models.Workout.Planning;

/// <summary>How much one exercise contributes to one muscle group, which volume accounting sums.</summary>
public sealed record PlanningMuscleContribution(int Id, int ExerciseDefinitionId, int MuscleGroupId, double Fraction);

/// <summary>The volume weighting for one goal tier.</summary>
public sealed record PlanningGoalTierWeight(int Id, UserGoal Goal, int MuscleGroupId, PriorityTier Tier);

