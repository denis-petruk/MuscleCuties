using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;

/// <summary>Searches the catalog and her own foods, ranking exact matches first.</summary>
public sealed class FoodCatalogSearchUseCase(IFoodCatalogSearchStore store) : IFoodCatalogSearchUseCase
{
    private const int MinPageSize = FoodCatalogSearchDefaults.FirstPageNumber;
    private const int MaxPageSize = 50;
    private const int DefaultPageSize = FoodCatalogSearchDefaults.DefaultPageSize;

    /// <summary>Searches the catalog for the query.</summary>
    public async Task<FoodCatalogSearchResponse> SearchAsync(
        FoodCatalogSearchRequest request,
        CancellationToken cancellationToken = default) =>
        await SearchAsync(request, null, cancellationToken).ConfigureAwait(false);

    /// <summary>Searches the catalog with a result limit and paging token.</summary>
    public async Task<FoodCatalogSearchResponse> SearchAsync(
        FoodCatalogSearchRequest request,
        Guid? ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Query))
            return new FoodCatalogSearchResponse([], MinPageSize, DefaultPageSize, false);

        var clampedPageSize = Math.Clamp(request.PageSize, MinPageSize, MaxPageSize);
        var safePageNumber = Math.Max(MinPageSize, request.PageNumber);

        var candidates = await store.SearchCandidatesAsync(request.Query.Trim(), ownerUserId, cancellationToken)
            .ConfigureAwait(false);

        if (candidates is null || candidates.Count == 0)
            return new FoodCatalogSearchResponse([], safePageNumber, clampedPageSize, false);

        var ranked = FoodCatalogRankingRule.Rank(request.Query, candidates);

        var skip = ((long)safePageNumber - MinPageSize) * clampedPageSize;
        if (skip >= ranked.Count)
            return new FoodCatalogSearchResponse([], safePageNumber, clampedPageSize, false);

        var pagedCandidates = ranked.Skip((int)skip).Take(clampedPageSize).ToList();
        var hasMore = ranked.Count > skip + pagedCandidates.Count;

        var items = pagedCandidates.Select(FoodCatalogItemMapper.Map).ToList();
        return new FoodCatalogSearchResponse(items, safePageNumber, clampedPageSize, hasMore);
    }
}
