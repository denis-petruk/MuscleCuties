using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>The reviewed exercise-exclusion catalog through the backend, which is what a plan may exclude.</summary>
public sealed class CloudReviewedExerciseExclusionCatalogV020Store(
    IMuscleCutiesBackendApiClient apiClient) : IReviewedExerciseExclusionCatalogV020Store
{
    private const string CatalogRoute = "/api/v1/workouts/plan/exercise-exclusions";

    /// <summary>The reviewed exclusions the backend offers.</summary>
    public async Task<IReadOnlyList<ReviewedExerciseExclusionOptionV020>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<ReviewedExerciseExclusionOptionV020[]>(
            CatalogRoute,
            cancellationToken).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "reviewed exercise exclusion catalog");
    }
}
