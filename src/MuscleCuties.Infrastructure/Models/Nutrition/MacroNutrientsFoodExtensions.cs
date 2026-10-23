using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Nutrition;

namespace MuscleCuties.Infrastructure.Models.Nutrition;

public static class MacroNutrientsFoodExtensions
{
    public static MacroNutrients FromFood(FoodItem food, float grams)
    {
        return MacroNutrients.FromPer100g(food.Calories, food.Protein, food.Carbs, food.Fats, grams);
    }

    public static MacroNutrients SumMealEntries(IEnumerable<LoggedMealEntry> entries)
    {
        return MacroNutrients.Sum(entries
            .Where(entry => entry.FoodItem is not null)
            .Select(entry => FromFood(entry.FoodItem!, entry.Grams)));
    }
}
