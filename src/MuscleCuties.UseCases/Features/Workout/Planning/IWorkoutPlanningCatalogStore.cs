using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

public interface IWorkoutPlanningCatalogStore
{
    Task<WorkoutPlanningCatalogResponse> GetCatalogAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkoutExercisePreference>> GetUserExercisePreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
