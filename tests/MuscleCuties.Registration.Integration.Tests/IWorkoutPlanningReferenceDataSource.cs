using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>Async inward port for planner reference data; adapters own persistence and mapping.</summary>
public interface IWorkoutPlanningReferenceDataSource
{
    Task<IReadOnlyList<PlanningExercise>> GetExercisesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanningExerciseDefinition>> GetExerciseDefinitionsAsync(
        IReadOnlyCollection<MovementPattern>? patterns = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanningArchetype>> GetArchetypesAsync(
        IReadOnlyCollection<int> archetypeIds,
        CancellationToken cancellationToken = default);

    Task<PlanningWeekTemplate?> FindWeekTemplateAsync(int daysPerWeek, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanningVolumeBudget>> GetVolumeBudgetsAsync(
        int daysPerWeek,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanningGoalTierWeight>> GetGoalTierWeightsAsync(
        UserGoal goal,
        bool tierCOnly,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanningExercisePreference>> GetExercisePreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
