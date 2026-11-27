using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;
using BarcodeModel = MuscleCuties.Core.Domain.Nutrition.Barcode;

namespace MuscleCuties.UseCases.Features.Nutrition.Barcode;

/// <summary>Resolves a barcode, refusing a malformed code before it reaches storage.</summary>
public sealed class LookupBarcodeUseCase(IBarcodeFoodLookupStore store) : ILookupBarcodeUseCase
{

    /// <summary>Looks up a scanned barcode and reports the match or why there is none.</summary>
    public async Task<BarcodeLookupResult> ExecuteAsync(
        BarcodeLookupRequest request,
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(request, null, cancellationToken).ConfigureAwait(false);

    /// <summary>Looks up a barcode value with the symbology the scanner reported.</summary>
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

        var dto = FoodCatalogItemMapper.Map(candidate);
        return new BarcodeLookupResult(true, true, barcode.NormalizedValue, dto, null);
    }

    /// <summary>The barcode format for the wire symbology.</summary>
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
