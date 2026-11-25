namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>Where a catalogue food came from, which decides how much its values are trusted.</summary>
public enum FoodCatalogSourceKind
{
    LocalFood,
    CustomFood,
    UsdaFoodDataCentral
}

/// <summary>How uncertain a source's values are, which the ranking weighs.</summary>
public sealed record FoodCatalogSourceUncertainty(
    FoodCatalogSourceKind SourceKind,
    string NeutralSourceKey,
    bool IsIndependentlyVerified,
    bool HasCompleteMacronutrients,
    float? VerifiedServingMassGrams);

/// <summary>Classifies a food's source and its uncertainty, so a derived value is never shown as measured.</summary>
public static class FoodCatalogProvenanceRule
{
    public const string LocalFoodSourceKey = "Nutrition_Source_LocalFood";
    public const string CustomFoodSourceKey = "Nutrition_Source_CustomFood";
    public const string UsdaFoodDataCentralSourceKey = "Nutrition_Source_UsdaFoodDataCentral";

    /// <summary>Classifies a food's source from the record itself, defaulting to the least trusted kind.</summary>
    public static FoodCatalogSourceKind Classify(FoodCatalogCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        // An FDC identifier marks USDA data even on an owner-scoped row: foods imported by the
        // external search fallback are stored per account but did not originate from the user.
        if (candidate.FdcId.HasValue)
            return FoodCatalogSourceKind.UsdaFoodDataCentral;

        return candidate.IsCustom
            ? FoodCatalogSourceKind.CustomFood
            : FoodCatalogSourceKind.LocalFood;
    }

    /// <summary>The uncertainty band for that source, with the reason it applies.</summary>
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
