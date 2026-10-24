using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Overview;

public interface IWorkoutOverviewUseCase
{
    Task<WorkoutOverviewResponse> LoadAsync(WorkoutOverviewRequest request,
        CancellationToken cancellationToken = default);

    Task RescheduleAsync(RescheduleWorkoutDayRequest request, CancellationToken cancellationToken = default);
    Task SetSessionOptionalAsync(SetWorkoutSessionOptionalRequest request, CancellationToken cancellationToken = default);
    Task<ReadinessAdjustmentProposalDto> ReviewReadinessAdjustmentAsync(ReviewReadinessAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<ReadinessAdjustmentDecisionDto> DecideReadinessAdjustmentAsync(DecideReadinessAdjustmentRequest request, bool apply, CancellationToken cancellationToken = default);
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

    public Task<ReadinessAdjustmentProposalDto> ReviewReadinessAdjustmentAsync(ReviewReadinessAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!WorkoutOverviewRule.IsValidReadinessAdjustmentRequest(request.LocalDate, request.TimeZoneId))
            throw new ArgumentException("A valid local date and time zone are required.", nameof(request));
        return store.ReviewReadinessAdjustmentAsync(request, cancellationToken);
    }

    public Task<ReadinessAdjustmentDecisionDto> DecideReadinessAdjustmentAsync(DecideReadinessAdjustmentRequest request,
        bool apply, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!WorkoutOverviewRule.IsValidReadinessAdjustmentDecision(
                request.ProposalId, request.LocalDate, request.TimeZoneId))
            throw new ArgumentException("A valid proposal, local date, and time zone are required.", nameof(request));
        return store.DecideReadinessAdjustmentAsync(request, apply, cancellationToken);
    }
}
