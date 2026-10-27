using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>
/// Keeps automatic changes pending until the canonical planner can regenerate a safe revision.
/// A copied revision or unsuppression of existing days does not establish current safety.
/// </summary>
public sealed class PostgresWorkoutPlanReconciler : IWorkoutPlanReconciler
{
    public Task ReconcileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
