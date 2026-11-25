using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>The strength plan's proposal, answers and lease through the backend.</summary>
public sealed class CloudStrengthPlanProposalStoreV020(IMuscleCutiesBackendApiClient apiClient)
    : IStrengthPlanProposalClientV020
{
    private const string ProposalRoute = "/api/v1/workouts/plan/strength-proposals";
    private const string AcceptRouteFormat = "/api/v1/workouts/plan/strength-proposals/{0:D}/accept";
    private const string AnswersRoute = "/api/v1/workouts/plan/answers";
    private const string HiitCaptureChallengeRoute = "/api/v1/workouts/plan/hiit-evidence-challenge";
    private const long InitialLeaseFence = 0;
    private const string PreviewAlreadyOpenCode = "WorkoutPreviewOpen";
    private const string PlanningInputsChangedCode = "WorkoutPlanningInputsChanged";

    /// <summary>Creates a plan proposal.</summary>
    public async Task<StrengthPlanProposalCreatedV020> CreateAsync(
        StrengthPlanPreviewRequestV020 request, CancellationToken cancellationToken)
    {
        var response = await apiClient.SendMutationAsync<
            StrengthPlanPreviewRequestV020, StrengthPlanProposalCreatedV020>(
            HttpMethod.Post, ProposalRoute, request,
            idempotencyKey: Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.ValidationFailed && response.Data?.Preview is not null)
            return response.Data;
        if (response.Status == BackendApiStatus.Conflict && response.ErrorCode == PreviewAlreadyOpenCode)
            throw new WorkoutPreviewAlreadyOpenException();
        if (response.Status == BackendApiStatus.Conflict && response.ErrorCode == PlanningInputsChangedCode)
            throw new WorkoutPlanningInputsChangedException();
        if (response.Status == BackendApiStatus.Conflict && response.Data is not null)
            return response.Data;
        return RequirePlanningData(response, "create strength plan proposal");
    }

    /// <summary>Accepts a plan proposal.</summary>
    public async Task<AcceptStrengthPlanProposalResultV020> AcceptAsync(
        AcceptStrengthPlanProposalRequestV020 request, CancellationToken cancellationToken)
    {
        var response = await apiClient.SendMutationAsync<AcceptStrengthPlanProposalRequestV020,
            AcceptStrengthPlanProposalResultV020>(
            HttpMethod.Post,
            string.Format(System.Globalization.CultureInfo.InvariantCulture, AcceptRouteFormat,
                request.ProposalId),
            request,
            idempotencyKey: request.ProposalId.ToString("N"), cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.Conflict)
            return response.Data ?? new AcceptStrengthPlanProposalResultV020(
                "Conflict", null, null, response.ErrorCode);
        if (response.Status == BackendApiStatus.ValidationFailed && response.Data is not null)
            return response.Data;
        return RequirePlanningData(response, "accept strength plan proposal");
    }

    /// <summary>Renews the planning lease.</summary>
    public Task<PlanningLeaseResult> RenewAsync(PlanningLeaseRequest request, CancellationToken cancellationToken = default) =>
        LeaseAsync("renew", request, cancellationToken);

    /// <summary>Closes the planning lease.</summary>
    public Task<PlanningLeaseResult> CloseAsync(PlanningLeaseRequest request, CancellationToken cancellationToken = default) =>
        LeaseAsync("close", request, cancellationToken);

    /// <summary>Takes over an abandoned proposal.</summary>
    public Task<PlanningLeaseResult> TakeoverAsync(Guid proposalId, CancellationToken cancellationToken = default) =>
        LeaseAsync("takeover", new PlanningLeaseRequest(proposalId, Guid.Empty, InitialLeaseFence), cancellationToken);

    /// <summary>The proposal being planned, so a returning user continues the one in flight.</summary>
    public async Task<ActivePlanningPreviewDto> GetActivePreviewAsync(CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<ActivePlanningPreviewDto>("/api/v1/workouts/plan/lease/active", cancellationToken).ConfigureAwait(false);
        return RequirePlanningData(response, "read active workout preview");
    }

    /// <summary>The activity answers already given for the proposal.</summary>
    public async Task<SavedActivityAnswersDto> ReadAnswersAsync(CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<SavedActivityAnswersDto>(AnswersRoute, cancellationToken).ConfigureAwait(false);
        return RequirePlanningData(response, "read workout activity answers");
    }

    /// <summary>The evidence challenge the HIIT capture has to answer before its numbers are accepted.</summary>
    public async Task<HiitEvidenceCaptureChallengeDto> IssueHiitCaptureChallengeAsync(CancellationToken cancellationToken = default)
    {
        var response = await apiClient.SendMutationAsync<object, HiitEvidenceCaptureChallengeDto>(
            HttpMethod.Post, HiitCaptureChallengeRoute, new { }, Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
        return RequirePlanningData(response, "start HIIT evidence capture");
    }

    /// <summary>Saves the activity answers.</summary>
    public async Task<SavedActivityAnswersDto> SaveAnswersAsync(SaveActivityAnswersRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await apiClient.SendMutationAsync<SaveActivityAnswersRequest, SavedActivityAnswersDto>(
            HttpMethod.Put, AnswersRoute, request, Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.Conflict && response.ErrorCode == PreviewAlreadyOpenCode)
            throw new WorkoutPreviewAlreadyOpenException();
        if (response.Status == BackendApiStatus.Conflict && response.ErrorCode == PlanningInputsChangedCode)
            throw new WorkoutPlanningInputsChangedException();
        if (response.Status == BackendApiStatus.Conflict && response.Data is not null)
            return response.Data;
        return RequirePlanningData(response, "save workout activity answers");
    }

    /// <summary>Takes the planning lease, which is what stops two devices applying plans to the same account at once.</summary>
    private async Task<PlanningLeaseResult> LeaseAsync(string action, PlanningLeaseRequest request, CancellationToken cancellationToken)
    {
        var response = await apiClient.SendMutationAsync<PlanningLeaseRequest, PlanningLeaseResult>(
            HttpMethod.Post, $"/api/v1/workouts/plan/lease/{action}", request,
            idempotencyKey: Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
        if (response.Status is BackendApiStatus.Conflict or BackendApiStatus.NotFound)
            return response.Data ?? new PlanningLeaseResult("Stale", null);
        return RequirePlanningData(response, "update workout preview lease");
    }

    /// <summary>The planning data, or an exception naming the failed operation.</summary>
    private static T RequirePlanningData<T>(BackendApiResponse<T> response, string operation) where T : class
    {
        // PG01: every outcome keeps its backend status, error category and request reference so the
        // screen can offer the right next action. A 401/403, an unclassified 400/409/422, a 405/408 and
        // a 429/503 used to become one generic connection exception before presentation.
        if (response.Status != BackendApiStatus.Success)
            throw new PlanningRequestFailedException(
                operation,
                response.Status,
                response.ResponseStatusCode,
                response.ErrorCode,
                response.RequestId);
        return CloudStoreResponse.RequireData(response, operation);
    }
}
