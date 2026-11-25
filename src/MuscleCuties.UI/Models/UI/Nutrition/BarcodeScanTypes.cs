namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>Barcode formats the scanner can decode; <c>Auto</c> lets the platform pick from the enabled set.</summary>
public enum BarcodeSymbology
{
    Auto = 0,
    Ean13 = 1,
    UpcA = 2,
    Ean8 = 3,
    UpcE = 4,
    Code128 = 5
}

/// <summary>How a scan ended: a decoded value, a deliberate cancel, or one of the failure modes the UI has to</summary>
/// explain rather than reporting "no result".</summary>
public enum BarcodeCaptureOutcome
{
    Decoded = 0,
    Cancelled = 1,
    PermissionDenied = 2,
    CameraUnavailable = 3,
    UnsupportedSymbology = 4
}

/// <summary>What the camera produced. The text and symbology are set only for a decoded scan;</summary>
/// <c>ErrorMessage</c> is set only for a failure.</summary>
public sealed record BarcodeCameraCaptureResult(
    BarcodeCaptureOutcome Outcome,
    string? BarcodeText = null,
    BarcodeSymbology Symbology = BarcodeSymbology.Auto,
    string? ErrorMessage = null);
