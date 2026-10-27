using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

public sealed record StrengthPlanProposalCreatedV020(
    Guid ProposalId,
    StrengthPlanPreviewResponseV020 Preview,
    bool IsPersisted,
    PlanningLeaseDto? Lease = null,
    int ProposalVersion = 0,
    bool ReplacesExistingPlan = false);

public sealed record PlanningLeaseDto(Guid LeaseId, long Fence, DateTimeOffset ExpiresAtUtc);
public sealed record PlanningLeaseRequest(Guid ProposalId, Guid LeaseId, long Fence);
public sealed record PlanningLeaseResult(string Status, PlanningLeaseDto? Lease);
public sealed record ActivePlanningPreviewDto(Guid? ProposalId, DateTimeOffset? ExpiresAtUtc);
public sealed record SavedActivityAnswersDto(long Version, IReadOnlyList<ActivityEligibilityInputV020> ActivityEligibilityEvidence,
    HiitEligibilityEvidenceInputV020? HiitEligibilityEvidence, bool SafetyPlanChanged = false)
{
    public WorkoutActivityDto? HiitUnderlyingCardioActivity { get; init; }
    public HiitEvidenceCaptureStamp? HiitCapture { get; init; }
}
public sealed record SaveActivityAnswersRequest(long ExpectedVersion, IReadOnlyList<ActivityEligibilityInputV020> ActivityEligibilityEvidence,
    HiitEligibilityEvidenceInputV020? HiitEligibilityEvidence)
{
    public WorkoutActivityDto? HiitUnderlyingCardioActivity { get; init; }
    public Guid? HiitCaptureChallengeId { get; init; }
}

public sealed record HiitEvidenceCaptureStamp(DateOnly LocalDate, long TimeZoneVersion);

public sealed class WorkoutPreviewAlreadyOpenException : InvalidOperationException
{
    public WorkoutPreviewAlreadyOpenException() : base("A workout plan preview is already open.") { }
}

public sealed class WorkoutPlanningInputsChangedException : Exception
{
    public WorkoutPlanningInputsChangedException() : base("Workout planning inputs changed. Please preview again.") { }
}

public interface IStrengthPlanProposalStoreV020
{
    Task<StrengthPlanProposalCreatedV020> SaveAsync(Guid userId, StrengthPlanPreviewRequestV020 request,
        StrengthPlanPreviewResponseV020 preview, CancellationToken cancellationToken);
    Task<PlanningLeaseResult> RenewAsync(Guid userId, PlanningLeaseRequest request, CancellationToken cancellationToken);
    Task<PlanningLeaseResult> CloseAsync(Guid userId, PlanningLeaseRequest request, CancellationToken cancellationToken);
    Task<PlanningLeaseResult> TakeoverAsync(Guid userId, Guid proposalId, CancellationToken cancellationToken);
    Task<ActivePlanningPreviewDto> GetActivePreviewAsync(Guid userId, CancellationToken cancellationToken);
    Task<SavedActivityAnswersDto> ReadAnswersAsync(Guid userId, CancellationToken cancellationToken);
    Task<SavedActivityAnswersDto> SaveAnswersAsync(Guid userId, SaveActivityAnswersRequest request, CancellationToken cancellationToken);
}

public sealed record AcceptStrengthPlanProposalRequestV020(Guid ProposalId, Guid LeaseId = default,
    long Fence = 0, int ProposalVersion = 0, bool ReplacementConfirmed = false,
    bool ReducedVolumeAcknowledged = false);

public sealed record AcceptStrengthPlanProposalResultV020(
    string Status,
    Guid? PlanId,
    Guid? RevisionId,
    string? LimitationCode);

public interface IStrengthPlanProposalClientV020
{
    Task<HiitEvidenceCaptureChallengeDto> IssueHiitCaptureChallengeAsync(CancellationToken cancellationToken = default);
    Task<StrengthPlanProposalCreatedV020> CreateAsync(StrengthPlanPreviewRequestV020 request,
        CancellationToken cancellationToken = default);

    Task<AcceptStrengthPlanProposalResultV020> AcceptAsync(AcceptStrengthPlanProposalRequestV020 request,
        CancellationToken cancellationToken = default);

    Task<PlanningLeaseResult> RenewAsync(PlanningLeaseRequest request, CancellationToken cancellationToken = default);
    Task<PlanningLeaseResult> CloseAsync(PlanningLeaseRequest request, CancellationToken cancellationToken = default);
    Task<PlanningLeaseResult> TakeoverAsync(Guid proposalId, CancellationToken cancellationToken = default);
    Task<ActivePlanningPreviewDto> GetActivePreviewAsync(CancellationToken cancellationToken = default);
    Task<SavedActivityAnswersDto> ReadAnswersAsync(CancellationToken cancellationToken = default);
    Task<SavedActivityAnswersDto> SaveAnswersAsync(SaveActivityAnswersRequest request, CancellationToken cancellationToken = default);
}

public interface IAcceptStrengthPlanProposalStoreV020
{
    Task<AcceptStrengthPlanProposalResultV020> AcceptAsync(Guid userId,
        AcceptStrengthPlanProposalRequestV020 request,
        CancellationToken cancellationToken);
}

public sealed class AcceptStrengthPlanProposalV020UseCase(IAcceptStrengthPlanProposalStoreV020 store)
{
    public Task<AcceptStrengthPlanProposalResultV020> ExecuteAsync(Guid userId,
        AcceptStrengthPlanProposalRequestV020 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId == Guid.Empty || request.ProposalId == Guid.Empty)
            return Task.FromResult(new AcceptStrengthPlanProposalResultV020("InvalidInput", null, null,
                "ProposalIdInvalid"));

        return store.AcceptAsync(userId, request, cancellationToken);
    }
}

/// <summary>Persists only feasible v0.2 previews as immutable proposals; it never activates them.</summary>
public sealed class CreateStrengthPlanProposalV020UseCase(
    PreviewStrengthPlanV020UseCase previewUseCase,
    IStrengthPlanProposalStoreV020 store)
{
    public async Task<StrengthPlanProposalCreatedV020> ExecuteAsync(Guid userId,
        StrengthPlanPreviewRequestV020 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var preview = await previewUseCase.ExecuteAsync(userId, request, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(preview.Status, "Feasible", StringComparison.Ordinal) ||
            preview.MixedActivitySchedule?.Sessions is not { Count: > 0 })
            return new StrengthPlanProposalCreatedV020(Guid.Empty, preview, false);

        return await store.SaveAsync(userId, request, preview, cancellationToken).ConfigureAwait(false);
    }
}
