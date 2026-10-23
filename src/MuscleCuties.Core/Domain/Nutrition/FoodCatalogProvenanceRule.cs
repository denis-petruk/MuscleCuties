namespace MuscleCuties.Core.Domain.Nutrition;

public enum FoodCatalogSourceKind
{
    LocalFood,
    CustomFood,
    UsdaFoodDataCentral
}

public sealed record FoodCatalogSourceUncertainty(
    FoodCatalogSourceKind SourceKind,
    string NeutralSourceKey,
    bool IsIndependentlyVerified,
    bool HasCompleteMacronutrients,
    float? VerifiedServingMassGrams);

public static class FoodCatalogProvenanceRule
{
    public const string LocalFoodSourceKey = "Nutrition_Source_LocalFood";
    public const string CustomFoodSourceKey = "Nutrition_Source_CustomFood";
    public const string UsdaFoodDataCentralSourceKey = "Nutrition_Source_UsdaFoodDataCentral";

    public static FoodCatalogSourceKind Classify(FoodCatalogCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.IsCustom)
            return FoodCatalogSourceKind.CustomFood;

        return candidate.FdcId.HasValue
            ? FoodCatalogSourceKind.UsdaFoodDataCentral
            : FoodCatalogSourceKind.LocalFood;
    }

    public static FoodCatalogSourceUncertainty EvaluateUncertainty(FoodCatalogCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var kind = Classify(candidate);
        var neutralKey = kind switch
        {
            FoodCatalogSourceKind.CustomFood => CustomFoodSourceKey,
            FoodCatalogSourceKind.UsdaFoodDataCentral => UsdaFoodDataCentralSourceKey,
            _ => LocalFoodSourceKey
        };

        var hasCompleteMacros = FoodCatalogNutritionRule.HasDisplayableNutrition(candidate);
        float? servingGrams = candidate.ServingSize.HasValue &&
                              FoodPortionMassRule.TryConvertMassToGrams(candidate.ServingSize.Value, candidate.ServingSizeUnit, out var grams)
            ? grams
            : null;

        return new FoodCatalogSourceUncertainty(
            SourceKind: kind,
            NeutralSourceKey: neutralKey,
            IsIndependentlyVerified: false,
            HasCompleteMacronutrients: hasCompleteMacros,
            VerifiedServingMassGrams: servingGrams);
    }
}
