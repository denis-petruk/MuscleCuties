using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;

public interface IFoodCatalogSearchStore
{
    Task<IReadOnlyList<FoodCatalogCandidate>> SearchCandidatesAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FoodCatalogCandidate>> SearchCandidatesAsync(
        string query,
        Guid? ownerUserId,
        CancellationToken cancellationToken = default) => SearchCandidatesAsync(query, cancellationToken);
}
