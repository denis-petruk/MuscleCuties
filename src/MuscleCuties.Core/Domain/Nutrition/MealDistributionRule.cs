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
    public static MealDistributionShare GetShares(BreakfastPreference preference)
    {
        return preference switch
        {
            BreakfastPreference.Sweet => new MealDistributionShare(0.26f, 0.34f, 0.30f, 0.10f),
            _ => new MealDistributionShare(0.28f, 0.34f, 0.28f, 0.10f)
        };
    }

    public static CalorieBudget Distribute(
        float totalCalories,
        MacroNutrients? macroPreference = null,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury)
    {
        var calories = float.IsFinite(totalCalories)
            ? Math.Clamp(totalCalories, 800f, 4000f)
            : 800f;
        var macros = IsValid(macroPreference)
            ? macroPreference!.Value
            : new MacroNutrients(RoundTo(calories, 1f),
                RoundTo(calories * 0.30f / 4f, 1f),
                RoundTo(calories * 0.40f / 4f, 1f),
                RoundTo(calories * 0.30f / 9f, 1f));
        var shares = GetShares(breakfastPreference);
        var allocations = new List<MealCalorieAllocation>(4)
        {
            Allocate(MealType.Breakfast, shares.Breakfast, shares.GetProteinShare(MealType.Breakfast), calories,
                macros),
            Allocate(MealType.Lunch, shares.Lunch, shares.GetProteinShare(MealType.Lunch), calories, macros),
            Allocate(MealType.Dinner, shares.Dinner, shares.GetProteinShare(MealType.Dinner), calories, macros),
            Allocate(MealType.Snack, shares.Snack, shares.GetProteinShare(MealType.Snack), calories, macros)
        };
        return new CalorieBudget(RoundTo(calories, 10f),
            RoundTo(calories * shares.MainMealTotal, 10f),
            RoundTo(calories * shares.Snack, 10f),
            shares.MainMealTotal, shares.Snack, macros, allocations);
    }

    private static bool IsValid(MacroNutrients? macros)
    {
        return macros is { } value &&
               float.IsFinite(value.Calories) && value.Calories > 0 && value.Calories <= 4000 &&
               float.IsFinite(value.Protein) && value.Protein is >= 0 and <= 1000 &&
               float.IsFinite(value.Carbs) && value.Carbs is >= 0 and <= 1000 &&
               float.IsFinite(value.Fats) && value.Fats is >= 0 and <= 1000;
    }

    private static MealCalorieAllocation Allocate(MealType type, float share,
        float proteinShare, float calories, MacroNutrients macros)
    {
        var mealCalories = RoundTo(calories * share, 10f);
        return new MealCalorieAllocation(type, mealCalories, share,
            new MacroNutrients(mealCalories,
                RoundTo(macros.Protein * proteinShare, 1f),
                RoundTo(macros.Carbs * share, 1f),
                RoundTo(macros.Fats * share, 1f)));
    }

    private static float RoundTo(float value, float nearest)
    {
        return MathF.Round(value / nearest) * nearest;
    }
}
