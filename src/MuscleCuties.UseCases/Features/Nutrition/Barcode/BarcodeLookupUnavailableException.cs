namespace MuscleCuties.UseCases.Features.Nutrition.Barcode;

/// <summary>
/// Raised when the barcode catalog cannot be reached (offline client or backend failure),
/// so a lookup failure is never reported as a valid barcode that is absent from the catalog.
/// </summary>
public sealed class BarcodeLookupUnavailableException : Exception
{
    /// <summary>The lookup service could not answer, so the scan should be retried later rather than treated as unknown.</summary>
    public BarcodeLookupUnavailableException()
        : base("The barcode food catalog is temporarily unavailable.") { }

    /// <summary>The lookup service could not answer, so the scan should be retried later rather than treated as unknown.</summary>
    public BarcodeLookupUnavailableException(Exception innerException)
        : base("The barcode food catalog is temporarily unavailable.", innerException) { }
}
