using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public static class FatSolubleVitaminAbsorption
{
    public const float MinimumFatGramsInWindow = 5f;
    public static readonly TimeSpan AbsorptionWindow = TimeSpan.FromHours(2);

    public static float SumAbsorbableNutrient(
        IEnumerable<LoggedMeal> sourceMeals,
        IEnumerable<LoggedMeal> dailyMeals,
        Func<FoodItem, float> valueSelector)
    {
        var sourceMealList = sourceMeals.ToList();
        var dailyMealList = dailyMeals.ToList();

        return sourceMealList
            .Where(meal => HasEnoughFatNearMeal(meal, dailyMealList))
            .SelectMany(meal => meal.Entries)
            .Where(entry => entry.FoodItem is not null)
            .Sum(entry => valueSelector(entry.FoodItem!) * entry.Grams / 100f);
    }

    public static bool HasEnoughFatNearMeal(
        LoggedMeal meal,
        IEnumerable<LoggedMeal> dailyMeals)
    {
        var nearbyFat = dailyMeals
            .Where(candidate => Math.Abs((candidate.LoggedAt - meal.LoggedAt).TotalMinutes) <= AbsorptionWindow.TotalMinutes)
            .SelectMany(candidate => candidate.Entries)
            .Where(entry => entry.FoodItem is not null)
            .Sum(entry => entry.FoodItem!.Fats * entry.Grams / 100f);

        return nearbyFat >= MinimumFatGramsInWindow;
    }
}
