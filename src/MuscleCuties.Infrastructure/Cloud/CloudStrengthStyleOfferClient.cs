using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>The style offer a plan can make, through the backend.</summary>
public sealed class CloudStrengthStyleOfferClient(IMuscleCutiesBackendApiClient apiClient) : IStrengthStyleOfferClient
{
    private const string Route = "/api/v1/workouts/plan/style-offer";
    /// <summary>The offer currently on the table, or the reason there is none.</summary>
    public async Task<StrengthStyleOfferResponse> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<StrengthStyleOfferResponse>(Route, cancellationToken).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "read strength style offer");
    }
    /// <summary>Dismisses the offer so it is not shown again.</summary>
    public async Task<StrengthStyleOfferDismissResult> DismissAsync(Guid offerId, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.SendMutationAsync<object, StrengthStyleOfferDismissResult>(HttpMethod.Post,
            $"{Route}/{offerId:D}/dismiss", new { }, Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "dismiss strength style offer");
    }
}
