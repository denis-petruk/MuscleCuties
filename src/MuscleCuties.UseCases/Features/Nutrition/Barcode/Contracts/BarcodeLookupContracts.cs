using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;

public enum BarcodeSymbologyDto
{
    Auto = 0,
    Ean13 = 1,
    UpcA = 2,
    Ean8 = 3,
    UpcE = 4,
    Code128 = 5
}

public enum BarcodeCaptureOutcomeDto
{
    Decoded = 0,
    Cancelled = 1,
    PermissionDenied = 2,
    CameraUnavailable = 3,
    UnsupportedSymbology = 4
}

public sealed record BarcodeCameraCaptureResultDto(
    BarcodeCaptureOutcomeDto Outcome,
    string? BarcodeText = null,
    BarcodeSymbologyDto Symbology = BarcodeSymbologyDto.Auto,
    string? ErrorMessage = null
);

public sealed record BarcodeLookupRequest(
    string BarcodeText,
    BarcodeSymbologyDto Symbology = BarcodeSymbologyDto.Auto
);

public sealed record BarcodeLookupResult(
    bool IsFound,
    bool IsValidBarcode,
    string NormalizedBarcode,
    FoodCatalogItemDto? FoodItem,
    string? ErrorMessage
);
