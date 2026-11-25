using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>The planning catalog: exercises, templates and budgets the planner chooses from.</summary>
public sealed class GetWorkoutPlanningCatalogUseCase(IWorkoutPlanningCatalogStore store)
{
    /// <summary>The planning catalog: activities, equipment and templates.</summary>
    public Task<WorkoutPlanningCatalogResponse> ExecuteAsync(CancellationToken cancellationToken = default) =>
        store.GetCatalogAsync(cancellationToken);
}
