namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>The stored canonical nutrient values per 100 grams.</summary>
public sealed record FoodCatalogNutrientProfile(
    decimal? Calories,
    decimal? Protein,
    decimal? Carbs,
    decimal? Fats,
    decimal? Fiber,
    decimal? Iron,
    decimal? VitaminB12,
    decimal? VitaminC,
    decimal? VitaminD,
    decimal? VitaminA,
    decimal? VitaminB6,
    decimal? Folate,
    decimal? Calcium,
    decimal? Magnesium,
    decimal? Zinc,
    decimal? Potassium)
{
    /// <summary>Upsert payloads require known, non-negative values for every nutrient.</summary>
    public bool IsValid => IsKnownNonNegative(Calories) && IsKnownNonNegative(Protein) &&
                           IsKnownNonNegative(Carbs) && IsKnownNonNegative(Fats) &&
                           IsKnownNonNegative(Fiber) && IsKnownNonNegative(Iron) &&
                           IsKnownNonNegative(VitaminB12) && IsKnownNonNegative(VitaminC) &&
                           IsKnownNonNegative(VitaminD) && IsKnownNonNegative(VitaminA) &&
                           IsKnownNonNegative(VitaminB6) && IsKnownNonNegative(Folate) &&
                           IsKnownNonNegative(Calcium) && IsKnownNonNegative(Magnesium) &&
                           IsKnownNonNegative(Zinc) && IsKnownNonNegative(Potassium);

    /// <summary>True when any nutrient differs from the profile it is compared with.</summary>
    public bool HasChangedFrom(FoodCatalogNutrientProfile previous) => this != previous;

    /// <summary>True when the nutrient is either unset or a non-negative amount.</summary>
    private static bool IsKnownNonNegative(decimal? value) => value is >= 0m;
}
