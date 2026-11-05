using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>
/// Explicit application orchestration contract for workout planning.
/// Coordinates source loading, catalog retrieval, and proposal lifecycle while
/// delegating deterministic calculations inward to pure Core domain rules.
/// </summary>
public interface IWorkoutPlanningApplicationService
{
    Task<StrengthPlanPreviewResponseV020> PreviewPlanAsync(
        Guid userId,
        StrengthPlanPreviewRequestV020 request,
        CancellationToken cancellationToken = default);

    Task<StrengthPlanProposalCreatedV020> CreateProposalAsync(
        Guid userId,
        StrengthPlanPreviewRequestV020 request,
        CancellationToken cancellationToken = default);

    Task<AcceptStrengthPlanProposalResultV020> AcceptProposalAsync(
        Guid userId,
        AcceptStrengthPlanProposalRequestV020 request,
        CancellationToken cancellationToken = default);

    Task<WorkoutPlanningCatalogResponse> GetCatalogAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of <see cref="IWorkoutPlanningApplicationService"/> coordinating planning use cases over pure domain policies.
/// </summary>
public sealed class WorkoutPlanningApplicationService(
    PreviewStrengthPlanV020UseCase previewUseCase,
    CreateStrengthPlanProposalV020UseCase createProposalUseCase,
    AcceptStrengthPlanProposalV020UseCase acceptProposalUseCase,
    GetWorkoutPlanningCatalogUseCase catalogUseCase,
    IPlanningClockPort? clockPort = null,
    IPlanningSafetyPort? safetyPort = null) : IWorkoutPlanningApplicationService
{
    private readonly IPlanningClockPort _clockPort = clockPort ?? new SystemPlanningClockPort();
    private readonly IPlanningSafetyPort _safetyPort = safetyPort ?? new DefaultPlanningSafetyPort();

    public Task<StrengthPlanPreviewResponseV020> PreviewPlanAsync(
        Guid userId,
        StrengthPlanPreviewRequestV020 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Default local date from clock port if not explicitly set
        if (request.LocalDate == default)
        {
            request = request with { LocalDate = _clockPort.GetCurrentLocalDate() };
        }

        return previewUseCase.ExecuteAsync(userId, request, cancellationToken);
    }

    public Task<StrengthPlanProposalCreatedV020> CreateProposalAsync(
        Guid userId,
        StrengthPlanPreviewRequestV020 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.LocalDate == default)
        {
            request = request with { LocalDate = _clockPort.GetCurrentLocalDate() };
        }

        return createProposalUseCase.ExecuteAsync(userId, request, cancellationToken);
    }

    public Task<AcceptStrengthPlanProposalResultV020> AcceptProposalAsync(
        Guid userId,
        AcceptStrengthPlanProposalRequestV020 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return acceptProposalUseCase.ExecuteAsync(userId, request, cancellationToken);
    }

    public Task<WorkoutPlanningCatalogResponse> GetCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        return catalogUseCase.ExecuteAsync(cancellationToken);
    }
}
