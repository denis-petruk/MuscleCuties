using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.Cloud;

public sealed class CloudStrengthPlanProposalStoreV020(IMuscleCutiesBackendApiClient apiClient)
    : IStrengthPlanProposalClientV020
{
    private const string ProposalRoute = "/api/v1/workouts/plan/strength-proposals";
    private const string AcceptRouteFormat = "/api/v1/workouts/plan/strength-proposals/{0:D}/accept";

    public async Task<StrengthPlanProposalCreatedV020> CreateAsync(
        StrengthPlanPreviewRequestV020 request, CancellationToken cancellationToken)
    {
        var result = CloudStoreResponse.RequireData(await apiClient.SendMutationAsync<
            StrengthPlanPreviewRequestV020, StrengthPlanProposalCreatedV020>(
            HttpMethod.Post, ProposalRoute, request,
            idempotencyKey: Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false),
            "create strength plan proposal");
        return result;
    }

    public async Task<AcceptStrengthPlanProposalResultV020> AcceptAsync(
        AcceptStrengthPlanProposalRequestV020 request, CancellationToken cancellationToken)
    {
        var response = await apiClient.SendMutationAsync<object,
            AcceptStrengthPlanProposalResultV020>(
            HttpMethod.Post,
            string.Format(System.Globalization.CultureInfo.InvariantCulture, AcceptRouteFormat,
                request.ProposalId),
            new { },
            idempotencyKey: Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.Conflict)
            return response.Data ?? new AcceptStrengthPlanProposalResultV020(
                "Conflict", null, null, response.ErrorCode);
        return CloudStoreResponse.RequireData(response, "accept strength plan proposal");
    }
}
