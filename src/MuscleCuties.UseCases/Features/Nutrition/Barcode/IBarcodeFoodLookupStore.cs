using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.UseCases.Features.Nutrition.Barcode;

public interface IBarcodeFoodLookupStore
{
    Task<FoodCatalogCandidate?> FindFoodByBarcodeAsync(
        IReadOnlyList<string> exactValues,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A non-null owner requires an explicit scoped implementation. Null retains the
    /// legacy adapter contract, whose session/authenticated context controls visibility.
    /// </summary>
    Task<FoodCatalogCandidate?> FindFoodByBarcodeAsync(
        IReadOnlyList<string> exactValues,
        Guid? ownerUserId,
        CancellationToken cancellationToken = default) => ownerUserId is null
            ? FindFoodByBarcodeAsync(exactValues, cancellationToken)
            : Task.FromException<FoodCatalogCandidate?>(new NotSupportedException(
                "This barcode store does not support explicit owner-scoped lookup."));
}
