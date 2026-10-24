using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

public sealed record StrengthPlanProposalCreatedV020(
    Guid ProposalId,
    StrengthPlanPreviewResponseV020 Preview,
    bool IsPersisted);

public interface IStrengthPlanProposalStoreV020
{
    Task<Guid> SaveAsync(Guid userId, StrengthPlanPreviewRequestV020 request,
        StrengthPlanPreviewResponseV020 preview, CancellationToken cancellationToken);
}

public sealed record AcceptStrengthPlanProposalRequestV020(Guid ProposalId);

public sealed record AcceptStrengthPlanProposalResultV020(
    string Status,
    Guid? PlanId,
    Guid? RevisionId,
    string? LimitationCode);

public interface IStrengthPlanProposalClientV020
{
    Task<StrengthPlanProposalCreatedV020> CreateAsync(StrengthPlanPreviewRequestV020 request,
        CancellationToken cancellationToken = default);

    Task<AcceptStrengthPlanProposalResultV020> AcceptAsync(AcceptStrengthPlanProposalRequestV020 request,
        CancellationToken cancellationToken = default);
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
    private const int NoProposalSessions = 0;

    public async Task<StrengthPlanProposalCreatedV020> ExecuteAsync(Guid userId,
        StrengthPlanPreviewRequestV020 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var preview = await previewUseCase.ExecuteAsync(userId, request, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(preview.Status, "Feasible", StringComparison.Ordinal) ||
            preview.Sessions.Count == NoProposalSessions)
            return new StrengthPlanProposalCreatedV020(Guid.Empty, preview, false);

        var proposalId = await store.SaveAsync(userId, request, preview, cancellationToken)
            .ConfigureAwait(false);
        return new StrengthPlanProposalCreatedV020(proposalId, preview, true);
    }
}
