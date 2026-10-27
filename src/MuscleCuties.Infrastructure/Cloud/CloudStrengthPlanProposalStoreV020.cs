using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.Cloud;

public sealed class CloudStrengthPlanProposalStoreV020(IMuscleCutiesBackendApiClient apiClient)
    : IStrengthPlanProposalClientV020
{
    private const string ProposalRoute = "/api/v1/workouts/plan/strength-proposals";
    private const string AcceptRouteFormat = "/api/v1/workouts/plan/strength-proposals/{0:D}/accept";
    private const string AnswersRoute = "/api/v1/workouts/plan/answers";
    private const long InitialLeaseFence = 0;

    public async Task<StrengthPlanProposalCreatedV020> CreateAsync(
        StrengthPlanPreviewRequestV020 request, CancellationToken cancellationToken)
    {
        var response = await apiClient.SendMutationAsync<
            StrengthPlanPreviewRequestV020, StrengthPlanProposalCreatedV020>(
            HttpMethod.Post, ProposalRoute, request,
            idempotencyKey: Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.ValidationFailed && response.Data?.Preview is not null)
            return response.Data;
        if (response.Status == BackendApiStatus.Conflict && response.Data is not null)
            return response.Data;
        return CloudStoreResponse.RequireData(response, "create strength plan proposal");
    }

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
        return CloudStoreResponse.RequireData(response, "accept strength plan proposal");
    }

    public Task<PlanningLeaseResult> RenewAsync(PlanningLeaseRequest request, CancellationToken cancellationToken = default) =>
        LeaseAsync("renew", request, cancellationToken);

    public Task<PlanningLeaseResult> CloseAsync(PlanningLeaseRequest request, CancellationToken cancellationToken = default) =>
        LeaseAsync("close", request, cancellationToken);

    public Task<PlanningLeaseResult> TakeoverAsync(Guid proposalId, CancellationToken cancellationToken = default) =>
        LeaseAsync("takeover", new PlanningLeaseRequest(proposalId, Guid.Empty, InitialLeaseFence), cancellationToken);

    public async Task<ActivePlanningPreviewDto> GetActivePreviewAsync(CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<ActivePlanningPreviewDto>("/api/v1/workouts/plan/lease/active", cancellationToken).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "read active workout preview");
    }

    public async Task<SavedActivityAnswersDto> ReadAnswersAsync(CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<SavedActivityAnswersDto>(AnswersRoute, cancellationToken).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "read workout activity answers");
    }

    public async Task<SavedActivityAnswersDto> SaveAnswersAsync(SaveActivityAnswersRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await apiClient.SendMutationAsync<SaveActivityAnswersRequest, SavedActivityAnswersDto>(
            HttpMethod.Put, AnswersRoute, request, Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.Conflict && response.Data is not null)
            return response.Data;
        return CloudStoreResponse.RequireData(response, "save workout activity answers");
    }

    private async Task<PlanningLeaseResult> LeaseAsync(string action, PlanningLeaseRequest request, CancellationToken cancellationToken)
    {
        var response = await apiClient.SendMutationAsync<PlanningLeaseRequest, PlanningLeaseResult>(
            HttpMethod.Post, $"/api/v1/workouts/plan/lease/{action}", request,
            idempotencyKey: Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
        if (response.Status is BackendApiStatus.Conflict or BackendApiStatus.NotFound)
            return response.Data ?? new PlanningLeaseResult("Stale", null);
        return CloudStoreResponse.RequireData(response, "update workout preview lease");
    }
}
