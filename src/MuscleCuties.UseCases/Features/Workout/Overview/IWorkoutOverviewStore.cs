using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Overview;

/// <summary>The overview's reads.</summary>
public interface IWorkoutOverviewStore
{
    Task<WorkoutOverviewResponse> LoadAsync(WorkoutOverviewRequest request, CancellationToken cancellationToken);
    Task RescheduleAsync(RescheduleWorkoutDayRequest request, CancellationToken cancellationToken);
    Task SetSessionOptionalAsync(SetWorkoutSessionOptionalRequest request, CancellationToken cancellationToken);
}
