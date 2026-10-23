using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Overview;

public interface IWorkoutOverviewStore
{
    Task<WorkoutOverviewResponse> LoadAsync(WorkoutOverviewRequest request, CancellationToken cancellationToken);
    Task RescheduleAsync(RescheduleWorkoutDayRequest request, CancellationToken cancellationToken);
}
