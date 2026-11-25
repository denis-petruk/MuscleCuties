using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Nutrition;

namespace MuscleCuties.Infrastructure.Models.Nutrition;

/// <summary>Reads macro nutrients out of the model types, so the planner and the diary share one conversion.</summary>
public static class MacroNutrientsFoodExtensions
{
    /// <summary>A food's per-100 g macros, which the callers then scale by weight.</summary>
    public static MacroNutrients FromFood(FoodItem food, float grams)
    {
        return MacroNutrients.FromPer100g(food.Calories, food.Protein, food.Carbs, food.Fats, grams);
    }

    /// <summary>The summed macros of a meal's entries, each scaled by the grams it was logged at.</summary>
    public static MacroNutrients SumMealEntries(IEnumerable<LoggedMealEntry> entries)
    {
        return MacroNutrients.Sum(entries
            .Where(entry => entry.FoodItem is not null)
            .Select(entry => FromFood(entry.FoodItem!, entry.Grams)));
    }
}
