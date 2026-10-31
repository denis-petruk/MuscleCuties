using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Domain.Nutrition;

public readonly record struct MealDistributionShare(
    float Breakfast,
    float Lunch,
    float Dinner,
    float Snack)
{
    public float MainMealTotal => Breakfast + Lunch + Dinner;

    public float GetProteinShare(MealType mealType)
    {
        if (!float.IsFinite(MainMealTotal) || MainMealTotal <= 0)
            return 0;
        return mealType switch
        {
            MealType.Breakfast => Breakfast / MainMealTotal,
            MealType.Lunch => Lunch / MainMealTotal,
            MealType.Dinner => Dinner / MainMealTotal,
            _ => 0f
        };
    }
}

public static class MealDistributionRule
{
    private static readonly MealDistributionShare SweetShares = new(0.26f, 0.34f, 0.30f, 0.10f);
    private static readonly MealDistributionShare SavouryShares = new(0.28f, 0.34f, 0.28f, 0.10f);

    private const int MealCount = 4;
    private const float MinimumCaloriesKcal = 800f;
    private const float MaximumCaloriesKcal = 4000f;
    private const float DefaultProteinShare = 0.30f;
    private const float DefaultCarbShare = 0.40f;
    private const float DefaultFatShare = 0.30f;
    private const float CaloriesPerGramProtein = 4f;
    private const float CaloriesPerGramCarb = 4f;
    private const float CaloriesPerGramFat = 9f;
    private const float MaximumMacroGrams = 1000f;
    private const float MacroRoundingGrams = 1f;
    private const float FineCalorieRoundingKcal = 1f;
    private const float CalorieRoundingKcal = 10f;

    public static MealDistributionShare GetShares(BreakfastPreference preference)
    {
        return preference switch
        {
            BreakfastPreference.Sweet => SweetShares,
            _ => SavouryShares
        };
    }

    public static CalorieBudget Distribute(
        float totalCalories,
        MacroNutrients? macroPreference = null,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury)
    {
        var calories = float.IsFinite(totalCalories)
            ? Math.Clamp(totalCalories, MinimumCaloriesKcal, MaximumCaloriesKcal)
            : MinimumCaloriesKcal;
        var macros = IsValid(macroPreference)
            ? macroPreference!.Value
            : new MacroNutrients(RoundTo(calories, FineCalorieRoundingKcal),
                RoundTo(calories * DefaultProteinShare / CaloriesPerGramProtein, MacroRoundingGrams),
                RoundTo(calories * DefaultCarbShare / CaloriesPerGramCarb, MacroRoundingGrams),
                RoundTo(calories * DefaultFatShare / CaloriesPerGramFat, MacroRoundingGrams));
        var shares = GetShares(breakfastPreference);
        var allocations = new List<MealCalorieAllocation>(MealCount)
        {
            Allocate(MealType.Breakfast, shares.Breakfast, shares.GetProteinShare(MealType.Breakfast), calories,
                macros),
            Allocate(MealType.Lunch, shares.Lunch, shares.GetProteinShare(MealType.Lunch), calories, macros),
            Allocate(MealType.Dinner, shares.Dinner, shares.GetProteinShare(MealType.Dinner), calories, macros),
            Allocate(MealType.Snack, shares.Snack, shares.GetProteinShare(MealType.Snack), calories, macros)
        };
        return new CalorieBudget(RoundTo(calories, CalorieRoundingKcal),
            RoundTo(calories * shares.MainMealTotal, CalorieRoundingKcal),
            RoundTo(calories * shares.Snack, CalorieRoundingKcal),
            shares.MainMealTotal, shares.Snack, macros, allocations);
    }

    private static bool IsValid(MacroNutrients? macros)
    {
        return macros is { } value &&
               float.IsFinite(value.Calories) && value.Calories > 0 && value.Calories <= MaximumCaloriesKcal &&
               float.IsFinite(value.Protein) && value.Protein is >= 0 and <= MaximumMacroGrams &&
               float.IsFinite(value.Carbs) && value.Carbs is >= 0 and <= MaximumMacroGrams &&
               float.IsFinite(value.Fats) && value.Fats is >= 0 and <= MaximumMacroGrams;
    }

    private static MealCalorieAllocation Allocate(MealType type, float share,
        float proteinShare, float calories, MacroNutrients macros)
    {
        var mealCalories = RoundTo(calories * share, CalorieRoundingKcal);
        return new MealCalorieAllocation(type, mealCalories, share,
            new MacroNutrients(mealCalories,
                RoundTo(macros.Protein * proteinShare, MacroRoundingGrams),
                RoundTo(macros.Carbs * share, MacroRoundingGrams),
                RoundTo(macros.Fats * share, MacroRoundingGrams)));
    }

    private static float RoundTo(float value, float nearest)
    {
        return MathF.Round(value / nearest) * nearest;
    }
}
