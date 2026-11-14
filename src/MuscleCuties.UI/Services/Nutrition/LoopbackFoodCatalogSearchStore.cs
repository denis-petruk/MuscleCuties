using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;

namespace MuscleCuties.UI.Services.Nutrition;

/// <summary>
///     A Debug build searches the loopback API. When that API is down, or its catalogue has not been
///     migrated and returns an empty page, the picker used to show a permanent "no match" state: the
///     field took text and no row ever appeared. This store keeps the API authoritative — a real answer
///     always wins — and only fills the gap with <see cref="DevelopmentFoodCatalog"/> when the local
///     answer is empty or the call fails.
/// </summary>
public sealed class LoopbackFoodCatalogSearchStore(CloudFoodCatalogSearchStore inner) : IFoodCatalogSearchStore
{
    public Task<IReadOnlyList<FoodCatalogCandidate>> SearchCandidatesAsync(
        string query,
        CancellationToken cancellationToken = default) =>
        SearchCandidatesAsync(query, null, cancellationToken);

    public async Task<IReadOnlyList<FoodCatalogCandidate>> SearchCandidatesAsync(
        string query,
        Guid? ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        try
        {
            var cloud = await inner.SearchCandidatesAsync(query, cancellationToken)
                .ConfigureAwait(false);
            if (cloud.Count > 0)
                return cloud;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A loopback server that is not running is the normal case for a Debug build; the
            // development catalogue below is the whole point of this store.
        }

        return DevelopmentFoodCatalog.Search(query);
    }
}
