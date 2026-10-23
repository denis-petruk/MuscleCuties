using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

public sealed class GetWorkoutPlanningCatalogUseCase(IWorkoutPlanningCatalogStore store)
{
    public Task<WorkoutPlanningCatalogResponse> ExecuteAsync(CancellationToken cancellationToken = default) =>
        store.GetCatalogAsync(cancellationToken);
}
