using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>A proposal that was stored, with the preview handle that reads it back.</summary>
public sealed record StrengthPlanProposalCreatedV020(
    Guid ProposalId,
    StrengthPlanPreviewResponseV020 Preview,
    bool IsPersisted,
    PlanningLeaseDto? Lease = null,
    int ProposalVersion = 0,
    bool ReplacesExistingPlan = false);

/// <summary>The lease that makes one device the planner for a short window.</summary>
public sealed record PlanningLeaseDto(Guid LeaseId, long Fence, DateTimeOffset ExpiresAtUtc);
/// <summary>A request for that lease.</summary>
public sealed record PlanningLeaseRequest(Guid ProposalId, Guid LeaseId, long Fence);
/// <summary>Whether the lease was granted, and when it expires.</summary>
public sealed record PlanningLeaseResult(string Status, PlanningLeaseDto? Lease);
/// <summary>A preview that is currently open, so a second one cannot be started over it.</summary>
public sealed record ActivePlanningPreviewDto(Guid? ProposalId, DateTimeOffset? ExpiresAtUtc);
/// <summary>The answers she gave for an activity, kept so the preview can be rebuilt.</summary>
public sealed record SavedActivityAnswersDto(long Version, IReadOnlyList<ActivityEligibilityInputV020> ActivityEligibilityEvidence,
    HiitEligibilityEvidenceInputV020? HiitEligibilityEvidence, bool SafetyPlanChanged = false)
{
    /// <summary>The cardio activity HIIT replaced, kept so the answer can be replayed.</summary>
    public WorkoutActivityDto? HiitUnderlyingCardioActivity { get; init; }
    /// <summary>Proof the HIIT evidence challenge was answered, required before the answer counts.</summary>
    public HiitEvidenceCaptureStamp? HiitCapture { get; init; }
    /// <summary>The equipment she had when answering, so a later change invalidates the answer.</summary>
    public EquipmentCapabilities? EquipmentCapabilities { get; init; }
}
/// <summary>Answers to store for an activity.</summary>
public sealed record SaveActivityAnswersRequest(long ExpectedVersion, IReadOnlyList<ActivityEligibilityInputV020> ActivityEligibilityEvidence,
    HiitEligibilityEvidenceInputV020? HiitEligibilityEvidence)
{
    /// <summary>The cardio activity being replaced, sent with the HIIT answer.</summary>
    public WorkoutActivityDto? HiitUnderlyingCardioActivity { get; init; }
    /// <summary>The challenge this answer responds to, so it cannot be replayed against another.</summary>
    public Guid? HiitCaptureChallengeId { get; init; }
}

/// <summary>Proof that the HIIT evidence challenge was answered.</summary>
public sealed record HiitEvidenceCaptureStamp(DateOnly LocalDate, long TimeZoneVersion);

/// <summary>A preview is already open, so this request must use the existing one.</summary>
public sealed class WorkoutPreviewAlreadyOpenException : InvalidOperationException
{
    /// <summary>Reports that a preview is already open.</summary>
    public WorkoutPreviewAlreadyOpenException() : base("A workout plan preview is already open.") { }
}

/// <summary>The inputs changed after the preview was built, so it must be regenerated.</summary>
public sealed class WorkoutPlanningInputsChangedException : Exception
{
    /// <summary>Reports that the planning inputs changed since the preview.</summary>
    public WorkoutPlanningInputsChangedException() : base("Workout planning inputs changed. Please preview again.") { }
}

/// <summary>The proposal persistence.</summary>
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

/// <summary>A proposal she is accepting.</summary>
public sealed record AcceptStrengthPlanProposalRequestV020(Guid ProposalId, Guid LeaseId = default,
    long Fence = 0, int ProposalVersion = 0, bool ReplacementConfirmed = false,
    bool ReducedVolumeAcknowledged = false);

/// <summary>What accepting it scheduled.</summary>
public sealed record AcceptStrengthPlanProposalResultV020(
    string Status,
    Guid? PlanId,
    Guid? RevisionId,
    string? LimitationCode);

/// <summary>The client-side proposal operations.</summary>
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

/// <summary>The write that turns a proposal into the plan.</summary>
public interface IAcceptStrengthPlanProposalStoreV020
{
    Task<AcceptStrengthPlanProposalResultV020> AcceptAsync(Guid userId,
        AcceptStrengthPlanProposalRequestV020 request,
        CancellationToken cancellationToken);
}

/// <summary>Accepts a proposal, refusing one whose inputs changed since the preview.</summary>
public sealed class AcceptStrengthPlanProposalV020UseCase(IAcceptStrengthPlanProposalStoreV020 store)
{
    /// <summary>Accepts a plan proposal.</summary>
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
    /// <summary>Creates a proposal from the preview.</summary>
    public async Task<StrengthPlanProposalCreatedV020> ExecuteAsync(Guid userId,
        StrengthPlanPreviewRequestV020 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var preview = GenerationPlanV2Mapper.ForCanonicalUse(
            await previewUseCase.ExecuteAsync(userId, request, cancellationToken).ConfigureAwait(false));
        var hasStrengthContent = preview.Sessions.Count > 0 && preview.Sessions.All(session =>
            session.Slots.Any(slot => slot.ExerciseId.HasValue));
        if (!string.Equals(preview.Status, "Feasible", StringComparison.Ordinal) ||
            !hasStrengthContent ||
            preview.MixedActivitySchedule?.Sessions is not { Count: > 0 })
            return new StrengthPlanProposalCreatedV020(Guid.Empty, preview, false);

        return await store.SaveAsync(userId, request, preview, cancellationToken).ConfigureAwait(false);
    }
}
