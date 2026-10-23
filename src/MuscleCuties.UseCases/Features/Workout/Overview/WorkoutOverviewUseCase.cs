using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Overview;

public interface IWorkoutOverviewUseCase
{
    Task<WorkoutOverviewResponse> LoadAsync(WorkoutOverviewRequest request,
        CancellationToken cancellationToken = default);

    Task RescheduleAsync(RescheduleWorkoutDayRequest request, CancellationToken cancellationToken = default);
}

public sealed class WorkoutOverviewUseCase(IWorkoutOverviewStore store) : IWorkoutOverviewUseCase
{
    public Task<WorkoutOverviewResponse> LoadAsync(WorkoutOverviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.LoadAsync(request, cancellationToken);
    }

    public Task RescheduleAsync(RescheduleWorkoutDayRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        WorkoutScheduleMathRule.EnsureValidReschedule(request.WorkoutDayId, request.TargetDate);
        return store.RescheduleAsync(request, cancellationToken);
    }
}
