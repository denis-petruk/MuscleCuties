using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Barcode;

public enum BarcodeScanOutcome
{
    EnterDigits,
    LookupUnavailable,
    InvalidFormat,
    NotFound,
    Found
}

public sealed record BarcodeScanDecision(
    BarcodeScanOutcome Outcome,
    string Input,
    string NormalizedBarcode,
    FoodCatalogItemDto? FoodItem);

/// <summary>
/// The one owner of what a typed or scanned barcode means. Both nutrition screens (the diary and the fast
/// logging panel) used to repeat this ladder, which is how their messages drifted apart.
/// </summary>
public static class BarcodeScanRule
{
    public const int CameraCaptureTimeoutSeconds = 20;

    public static BarcodeScanDecision Decide(string? typedText, BarcodeLookupResult? result)
    {
        var input = (typedText ?? string.Empty).Trim();
        if (input.Length == 0)
            return new BarcodeScanDecision(BarcodeScanOutcome.EnterDigits, string.Empty, string.Empty, null);

        if (result is null)
            return new BarcodeScanDecision(BarcodeScanOutcome.LookupUnavailable, input, string.Empty, null);

        if (!result.IsValidBarcode)
            return new BarcodeScanDecision(
                BarcodeScanOutcome.InvalidFormat, input, result.NormalizedBarcode, null);

        if (!result.IsFound || result.FoodItem is null)
            return new BarcodeScanDecision(BarcodeScanOutcome.NotFound, input, result.NormalizedBarcode, null);

        return new BarcodeScanDecision(
            BarcodeScanOutcome.Found, input, result.NormalizedBarcode, result.FoodItem);
    }
}
