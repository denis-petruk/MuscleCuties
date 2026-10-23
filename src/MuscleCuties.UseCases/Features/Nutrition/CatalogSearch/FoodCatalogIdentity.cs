using System.Globalization;
using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;

/// <summary>Produces a stable source-qualified identity while preserving the legacy integer field.</summary>
public static class FoodCatalogIdentity
{
    public static string ToKey(FoodCatalogCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.UserFoodItemId is { } userFoodId)
            return $"custom:{userFoodId:N}";
        if (candidate.IsCustom)
            return $"custom-legacy:{candidate.Id.ToString(CultureInfo.InvariantCulture)}";
        return $"catalog:{candidate.Id.ToString(CultureInfo.InvariantCulture)}";
    }
}
