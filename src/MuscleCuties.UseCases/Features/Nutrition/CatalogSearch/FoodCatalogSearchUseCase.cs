using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;

public sealed class FoodCatalogSearchUseCase(IFoodCatalogSearchStore store) : IFoodCatalogSearchUseCase
{
    private const int MinPageSize = FoodCatalogSearchDefaults.FirstPageNumber;
    private const int MaxPageSize = 50;
    private const int DefaultPageSize = FoodCatalogSearchDefaults.DefaultPageSize;

    public async Task<FoodCatalogSearchResponse> SearchAsync(
        FoodCatalogSearchRequest request,
        CancellationToken cancellationToken = default) =>
        await SearchAsync(request, null, cancellationToken).ConfigureAwait(false);

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

        var items = pagedCandidates.Select(MapToDto).ToList();
        return new FoodCatalogSearchResponse(items, safePageNumber, clampedPageSize, hasMore);
    }

    private static FoodCatalogItemDto MapToDto(FoodCatalogCandidate candidate)
    {
        var sourceSummary = FoodCatalogProvenanceRule.Classify(candidate) switch
        {
            FoodCatalogSourceKind.CustomFood => "Nutrition_Source_CustomFood",
            FoodCatalogSourceKind.UsdaFoodDataCentral => "Nutrition_Source_UsdaFoodDataCentral",
            _ => "Nutrition_Source_LocalFood"
        };

        var item = new FoodCatalogItemDto(
            candidate.Id,
            candidate.Name,
            candidate.Calories,
            candidate.Protein,
            candidate.Carbs,
            candidate.Fats,
            candidate.ServingSize,
            candidate.ServingSizeUnit,
            candidate.ServingOptionsJson,
            sourceSummary,
            string.Empty,
            candidate.IsCustom,
            FoodCatalogIdentity.ToKey(candidate)
        );
        return item with { NutritionSummary = FoodCatalogNutritionSummary.Build(item) };
    }
}
