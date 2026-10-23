using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public static class FatSolubleVitaminAbsorption
{
    public const float MinimumFatGramsInWindow = 5f;
    public static readonly TimeSpan AbsorptionWindow = FatSolubleAbsorptionRule.AbsorptionWindow;

    public static float SumAbsorbableNutrient(
        IEnumerable<LoggedMeal> sourceMeals,
        IEnumerable<LoggedMeal> dailyMeals,
        Func<FoodItem, float> valueSelector)
    {
        ArgumentNullException.ThrowIfNull(valueSelector);
        var source = Map(sourceMeals, valueSelector);
        var daily = Map(dailyMeals, _ => 0);
        var amount = FatSolubleAbsorptionRule.SumAbsorbableNutrient(source, daily);
        return double.IsFinite(amount) && amount <= float.MaxValue ? (float)amount : 0;
    }

    public static bool HasEnoughFatNearMeal(LoggedMeal meal, IEnumerable<LoggedMeal> dailyMeals)
    {
        if (meal is null)
            return false;
        return FatSolubleAbsorptionRule.HasEnoughFatNearMeal(
            Map([meal], _ => 0).FirstOrDefault(), Map(dailyMeals, _ => 0));
    }

    private static IReadOnlyList<AbsorptionMealObservation> Map(
        IEnumerable<LoggedMeal>? meals, Func<FoodItem, float> nutrientSelector)
    {
        return meals?.Where(meal => meal is not null)
            .Select(meal => new AbsorptionMealObservation(meal.LoggedAt,
                meal.Entries?.Where(entry => entry?.FoodItem is not null)
                    .Select(entry => new AbsorptionEntryObservation(entry.Grams,
                        entry.FoodItem!.Fats, nutrientSelector(entry.FoodItem)))
                    .ToArray() ?? []))
            .ToArray() ?? [];
    }
}
