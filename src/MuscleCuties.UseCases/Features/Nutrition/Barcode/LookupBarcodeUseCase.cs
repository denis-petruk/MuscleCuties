using BarcodeModel = MuscleCuties.Core.Domain.Nutrition.Barcode;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Barcode;

public sealed class LookupBarcodeUseCase(IBarcodeFoodLookupStore store) : ILookupBarcodeUseCase
{

    public async Task<BarcodeLookupResult> ExecuteAsync(
        BarcodeLookupRequest request,
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(request, null, cancellationToken).ConfigureAwait(false);

    public async Task<BarcodeLookupResult> ExecuteAsync(
        BarcodeLookupRequest request,
        Guid? ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return new BarcodeLookupResult(false, false, string.Empty, null, "Barcode cannot be empty.");

        var format = MapSymbology(request.Symbology);
        var barcode = BarcodeModel.Create(request.BarcodeText, format);
        if (!barcode.IsValid)
            return new BarcodeLookupResult(false, false, barcode.RawValue, null, barcode.ErrorMessage);

        var candidate = await store.FindFoodByBarcodeAsync(
            barcode.GetExactLookupValues(),
            ownerUserId,
            cancellationToken
        ).ConfigureAwait(false);

        if (candidate is null || !barcode.MatchesExactLookupValue(candidate.GtinUpc) ||
            !FoodCatalogNutritionRule.HasDisplayableNutrition(candidate))
            return new BarcodeLookupResult(false, true, barcode.NormalizedValue, null, "Food item not found for barcode.");

        var dto = MapToDto(candidate);
        return new BarcodeLookupResult(true, true, barcode.NormalizedValue, dto, null);
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

    private static BarcodeFormat MapSymbology(BarcodeSymbologyDto symbology) => symbology switch
    {
        BarcodeSymbologyDto.Ean13 => BarcodeFormat.Ean13,
        BarcodeSymbologyDto.UpcA => BarcodeFormat.UpcA,
        BarcodeSymbologyDto.Ean8 => BarcodeFormat.Ean8,
        BarcodeSymbologyDto.UpcE => BarcodeFormat.UpcE,
        BarcodeSymbologyDto.Code128 => BarcodeFormat.Code128,
        _ => BarcodeFormat.Unknown
    };
}
