using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Barcode;

public interface ILookupBarcodeUseCase
{
    Task<BarcodeLookupResult> ExecuteAsync(
        BarcodeLookupRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Never discard an explicit owner; null retains the legacy session contract.</summary>
    Task<BarcodeLookupResult> ExecuteAsync(
        BarcodeLookupRequest request,
        Guid? ownerUserId,
        CancellationToken cancellationToken = default) => ownerUserId is null
            ? ExecuteAsync(request, cancellationToken)
            : Task.FromException<BarcodeLookupResult>(new NotSupportedException(
                "This barcode use case does not support explicit owner-scoped lookup."));
}
