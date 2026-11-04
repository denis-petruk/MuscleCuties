using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UI.Services.Nutrition;

public sealed record BarcodeLookupResultView(
    bool IsFound,
    bool IsValidBarcode,
    string NormalizedBarcode,
    FoodCatalogItem? FoodItem,
    string? ErrorMessage);

public static partial class NutritionPresentationMapper
{
    public static FoodCatalogItem FromCatalogItem(FoodCatalogItemDto item) =>
        new(item.Id, item.Name, item.Calories, item.Protein, item.Carbs, item.Fats, item.ServingSize,
            item.ServingSizeUnit, item.ServingOptionsJson, item.SourceSummary, item.NutritionSummary,
            item.IsCustom, item.FoodKey);

    public static async Task<BarcodeLookupResultView> LookupBarcodeAsync(
        ILookupBarcodeUseCase useCase,
        string barcodeText,
        BarcodeSymbology symbology)
    {
        var result = await useCase.ExecuteAsync(
            new BarcodeLookupRequest(barcodeText, (BarcodeSymbologyDto)symbology));
        return new BarcodeLookupResultView(
            result.IsFound,
            result.IsValidBarcode,
            result.NormalizedBarcode,
            result.FoodItem is null ? null : FromCatalogItem(result.FoodItem),
            result.ErrorMessage);
    }
}
