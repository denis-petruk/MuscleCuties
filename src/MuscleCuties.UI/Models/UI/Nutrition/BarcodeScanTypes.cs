namespace MuscleCuties.UI.Models.UI.Nutrition;

public enum BarcodeSymbology
{
    Auto = 0,
    Ean13 = 1,
    UpcA = 2,
    Ean8 = 3,
    UpcE = 4,
    Code128 = 5
}

public enum BarcodeCaptureOutcome
{
    Decoded = 0,
    Cancelled = 1,
    PermissionDenied = 2,
    CameraUnavailable = 3,
    UnsupportedSymbology = 4
}

public sealed record BarcodeCameraCaptureResult(
    BarcodeCaptureOutcome Outcome,
    string? BarcodeText = null,
    BarcodeSymbology Symbology = BarcodeSymbology.Auto,
    string? ErrorMessage = null);
