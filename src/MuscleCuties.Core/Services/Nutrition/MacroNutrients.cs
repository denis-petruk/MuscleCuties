using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public readonly record struct MacroNutrients(
    float Calories,
    float Protein,
    float Carbs,
    float Fats)
{
    public static MacroNutrients Empty => new(0f, 0f, 0f, 0f);

    public static MacroNutrients FromPer100g(
        float calories,
        float protein,
        float carbs,
        float fats,
        float grams)
    {
        var ratio = grams / 100f;
        return new MacroNutrients(
            calories * ratio,
            protein * ratio,
            carbs * ratio,
            fats * ratio);
    }

    public static MacroNutrients FromFood(FoodItem food, float grams) =>
        FromPer100g(food.Calories, food.Protein, food.Carbs, food.Fats, grams);

    public static MacroNutrients Sum(IEnumerable<MacroNutrients> values) =>
        values.Aggregate(Empty, (total, next) => total + next);

    public static MacroNutrients SumMealEntries(IEnumerable<LoggedMealEntry> entries) =>
        Sum(entries
            .Where(entry => entry.FoodItem is not null)
            .Select(entry => FromFood(entry.FoodItem!, entry.Grams)));

    public static MacroNutrients operator +(MacroNutrients left, MacroNutrients right) =>
        new(
            left.Calories + right.Calories,
            left.Protein + right.Protein,
            left.Carbs + right.Carbs,
            left.Fats + right.Fats);

    public string ToNutritionText() =>
        $"{Calories:N0} kcal · P {Protein:N1}g · C {Carbs:N1}g · F {Fats:N1}g";

    public string ToMacroText() =>
        $"P {Protein:N1}g · C {Carbs:N1}g · F {Fats:N1}g";
}
