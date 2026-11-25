using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;

/// <summary>Which barcode format was scanned, so a misread can be told from an unknown product.</summary>
public enum BarcodeSymbologyDto
{
    Auto = 0,
    Ean13 = 1,
    UpcA = 2,
    Ean8 = 3,
    UpcE = 4,
    Code128 = 5
}

/// <summary>A scanned code, with the symbology the scanner reported.</summary>
public sealed record BarcodeLookupRequest(
    string BarcodeText,
    BarcodeSymbologyDto Symbology = BarcodeSymbologyDto.Auto
);

/// <summary>The food a code resolves to, or why it did not resolve.</summary>
public sealed record BarcodeLookupResult(
    bool IsFound,
    bool IsValidBarcode,
    string NormalizedBarcode,
    FoodCatalogItemDto? FoodItem,
    string? ErrorMessage
);
