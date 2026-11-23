namespace MuscleCuties.UI.Services.Nutrition;

/// <summary>
/// One owner of the barcode status keys. The diary and the fast-logging panel each declared their own copy of
/// these strings, so a wording or key change had to be made twice and could drift.
/// </summary>
public static class BarcodeStatusText
{
    public const string EnterDigitsKey = "Nutrition_Barcode_StatusEnterDigits";
    public const string SearchingCatalogKey = "Nutrition_Barcode_StatusSearchingCatalog";
    public const string FoundInCatalogFormatKey = "Nutrition_Barcode_StatusFoundInCatalogFormat";
    public const string NotFoundFormatKey = "Nutrition_Barcode_StatusNotFoundFormat";
    public const string IncompleteNutritionKey = "Nutrition_Barcode_StatusIncompleteNutrition";
    public const string InvalidFormatKey = "Nutrition_Barcode_StatusInvalidFormat";
    public const string PermissionDeniedKey = "Nutrition_Barcode_StatusPermissionDenied";
    public const string CameraUnavailableKey = "Nutrition_Barcode_StatusCameraUnavailable";
    public const string UnsupportedSymbologyKey = "Nutrition_Barcode_StatusUnsupportedSymbology";
    public const string NoCodeDetectedKey = "Nutrition_Barcode_StatusNoCodeDetected";
    public const string ScanCancelledKey = "Nutrition_Barcode_StatusScanCancelled";
    public const string LookupUnavailableKey = "Nutrition_Barcode_StatusLookupUnavailable";
    public const string LookupFailedKey = "Nutrition_Barcode_StatusLookupFailed";
}
