using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Server.Workout.Planning;

/// <summary>
/// Server-side entry point for a trusted scheduler or queue consumer. It is deliberately
/// not mapped to an HTTP route; deployment must provide authenticated first-party delivery
/// of the account ID and retry failed or still-pending work.
/// </summary>
public sealed class WorkoutPendingRevisionJobHandler(IWorkoutPlanReconciler reconciler)
{
    public Task<WorkoutReconciliationOutcome> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new ArgumentException("An account is required.", nameof(userId));
        return reconciler.ReconcileAsync(userId, cancellationToken);
    }
}
