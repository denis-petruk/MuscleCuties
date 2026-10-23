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

    public bool HasChangedFrom(FoodCatalogNutrientProfile previous) => this != previous;

    private static bool IsKnownNonNegative(decimal? value) => value is >= 0m;
}
