using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.UseCases.Features.Nutrition.CatalogVersioning;

public sealed record CanonicalFoodUpsertInput(
    string SeedCode,
    string Name,
    FoodCatalogNutrientProfile Nutrients,
    string ChangeSource,
    int? FdcId = null);

public sealed record CanonicalFoodUpsertResult(int FoodItemId, bool Created, bool NutrientsChanged);

public interface ICanonicalFoodUpsertStore
{
    Task<CanonicalFoodUpsertResult> UpsertAsync(
        CanonicalFoodUpsertInput input,
        CancellationToken cancellationToken = default);
}
