using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UI.Services.Nutrition;

/// <summary>Presentation-owned state for a barcode scan; the decision itself belongs to <see cref="BarcodeScanRule"/>.</summary>
public enum BarcodeScanState
{
    EnterDigits,
    LookupUnavailable,
    InvalidFormat,
    NotFound,
    Found
}

public sealed record BarcodeScanResultView(
    BarcodeScanState State,
    string NormalizedBarcode,
    FoodCatalogItem? FoodItem);

public static partial class NutritionPresentationMapper
{
    public static FoodCatalogItem FromCatalogItem(FoodCatalogItemDto item) =>
        new(item.Id, item.Name, item.Calories, item.Protein, item.Carbs, item.Fats, item.ServingSize,
            item.ServingSizeUnit, item.ServingOptionsJson, item.SourceSummary, item.NutritionSummary,
            item.IsCustom, item.FoodKey);

    public static async Task<BarcodeScanResultView> LookupBarcodeAsync(
        ILookupBarcodeUseCase useCase,
        string barcodeText,
        BarcodeSymbology symbology)
    {
        var result = await useCase.ExecuteAsync(
            new BarcodeLookupRequest(barcodeText, (BarcodeSymbologyDto)symbology)).ConfigureAwait(false);

        var decision = BarcodeScanRule.Decide(barcodeText, result);

        return new BarcodeScanResultView(
            decision.Outcome switch
            {
                BarcodeScanOutcome.EnterDigits => BarcodeScanState.EnterDigits,
                BarcodeScanOutcome.LookupUnavailable => BarcodeScanState.LookupUnavailable,
                BarcodeScanOutcome.InvalidFormat => BarcodeScanState.InvalidFormat,
                BarcodeScanOutcome.NotFound => BarcodeScanState.NotFound,
                _ => BarcodeScanState.Found
            },
            decision.NormalizedBarcode,
            decision.FoodItem is null ? null : FromCatalogItem(decision.FoodItem));
    }
}
