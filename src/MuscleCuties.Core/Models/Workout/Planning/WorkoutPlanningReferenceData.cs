using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Models.Workout.Planning;

public sealed record PlanningMuscleContribution(int Id, int ExerciseDefinitionId, int MuscleGroupId, double Fraction);

public sealed record PlanningGoalTierWeight(int Id, UserGoal Goal, int MuscleGroupId, PriorityTier Tier);

