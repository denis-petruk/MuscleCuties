using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Overview;

/// <summary>The workout overview.</summary>
public interface IWorkoutOverviewUseCase
{
    Task<WorkoutOverviewResponse> LoadAsync(WorkoutOverviewRequest request,
        CancellationToken cancellationToken = default);

    Task RescheduleAsync(RescheduleWorkoutDayRequest request, CancellationToken cancellationToken = default);
    Task SetSessionOptionalAsync(SetWorkoutSessionOptionalRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Assembles the overview and applies reschedules, keeping the week's ordering intact.</summary>
public sealed class WorkoutOverviewUseCase(IWorkoutOverviewStore store) : IWorkoutOverviewUseCase
{
    /// <summary>Loads the workout overview for the requested week.</summary>
    public Task<WorkoutOverviewResponse> LoadAsync(WorkoutOverviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.LoadAsync(request, cancellationToken);
    }

    /// <summary>Moves a day and shifts the following ones, keeping the week's order and count intact.</summary>
    public Task RescheduleAsync(RescheduleWorkoutDayRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        WorkoutScheduleMathRule.EnsureValidReschedule(request.WorkoutDayId, request.TargetDate);
        return store.RescheduleAsync(request, cancellationToken);
    }

    /// <summary>Marks a session optional or required.</summary>
    public Task SetSessionOptionalAsync(SetWorkoutSessionOptionalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!WorkoutOverviewRule.IsValidSessionOptionality(
                request.WorkoutDayId,
                request.IsOptional,
                request.IsEasierAlternative))
            throw new ArgumentException("An easier alternative must be explicitly optional.", nameof(request));
        return store.SetSessionOptionalAsync(request, cancellationToken);
    }
}
