using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;

public interface IFoodCatalogSearchUseCase
{
    Task<FoodCatalogSearchResponse> SearchAsync(
        FoodCatalogSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<FoodCatalogSearchResponse> SearchAsync(
        FoodCatalogSearchRequest request,
        Guid? ownerUserId,
        CancellationToken cancellationToken = default) => SearchAsync(request, cancellationToken);
}
