using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.Cloud;

public sealed class CloudPhaseAdjustmentProposalClientV010(IMuscleCutiesBackendApiClient apiClient)
    : IPhaseAdjustmentProposalClientV010
{
    private const string ProposalRoute = "/api/v1/workouts/plan/phase-adjustment-proposals";
    private const string DecisionRouteFormat =
        "/api/v1/workouts/plan/phase-adjustment-proposals/{0:D}/decision";

    public async Task<CreatePhaseAdjustmentProposalResultV010> CreateAsync(string? timeZoneId,
        CancellationToken cancellationToken = default) =>
        CloudStoreResponse.RequireData(await apiClient.SendMutationAsync<CreatePhaseAdjustmentProposalRequestV010,
            CreatePhaseAdjustmentProposalResultV010>(HttpMethod.Post, ProposalRoute,
            new CreatePhaseAdjustmentProposalRequestV010(timeZoneId),
            idempotencyKey: Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false),
            "create phase adjustment proposal");

    public async Task<DecidePhaseAdjustmentProposalResultV010> DecideAsync(
        DecidePhaseAdjustmentProposalRequestV010 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var route = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            DecisionRouteFormat, request.ProposalId);
        var response = await apiClient.SendMutationAsync<PhaseAdjustmentProposalDecisionBodyV010,
            DecidePhaseAdjustmentProposalResultV010>(HttpMethod.Post, route,
            new PhaseAdjustmentProposalDecisionBodyV010(request.ApplyChanges,
                request.Selections ?? Array.Empty<PhaseAdjustmentChoiceSelectionDtoV010>()),
            idempotencyKey: Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.Conflict)
            return response.Data ?? new DecidePhaseAdjustmentProposalResultV010(
                "Conflict", false, response.ErrorCode);
        return CloudStoreResponse.RequireData(response, "decide phase adjustment proposal");
    }

    private sealed record PhaseAdjustmentProposalDecisionBodyV010(
        bool ApplyChanges,
        IReadOnlyList<PhaseAdjustmentChoiceSelectionDtoV010> Selections);
}
