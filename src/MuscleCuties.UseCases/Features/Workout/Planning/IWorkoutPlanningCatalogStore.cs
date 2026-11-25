using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>The catalog read.</summary>
public interface IWorkoutPlanningCatalogStore
{
    Task<WorkoutPlanningCatalogResponse> GetCatalogAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkoutExercisePreference>> GetUserExercisePreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
