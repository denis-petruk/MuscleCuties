using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public interface ICalorieDistributionService
{
    CalorieBudget Distribute(
        float totalCalories,
        MacroNutrients? macroPreference = null,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury);
}

public sealed class CalorieDistributionService : ICalorieDistributionService
{
    private const float MinMainMealShare = 0.75f;
    private const float MaxMainMealShare = 0.85f;
    private const float MinSnackShare = 0.15f;
    private const float MaxSnackShare = 0.25f;

    private const float DefaultCarbPercent = 0.40f;
    private const float DefaultProteinPercent = 0.30f;
    private const float DefaultFatPercent = 0.30f;

    private const float MinCalories = 800f;
    private const float CaloriesPerGramProtein = 4f;
    private const float CaloriesPerGramCarb = 4f;
    private const float CaloriesPerGramFat = 9f;

    public CalorieBudget Distribute(
        float totalCalories,
        MacroNutrients? macroPreference = null,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury)
    {
        if (float.IsNaN(totalCalories) || float.IsInfinity(totalCalories) || totalCalories < MinCalories)
            totalCalories = MinCalories;

        var dailyMacros = macroPreference is { } pref && pref.Calories > 0f
            ? pref
            : ComputeDefaultMacros(totalCalories);

        var (breakfastShare, lunchShare, dinnerShare, snackShare) =
            GetMealShares(breakfastPreference);

        var mainMealShare = breakfastShare + lunchShare + dinnerShare;
        (mainMealShare, snackShare) = ClampShares(mainMealShare, snackShare);

        var rebalancedBreakfast = breakfastShare / (breakfastShare + lunchShare + dinnerShare) * mainMealShare;
        var rebalancedLunch = lunchShare / (breakfastShare + lunchShare + dinnerShare) * mainMealShare;
        var rebalancedDinner = dinnerShare / (breakfastShare + lunchShare + dinnerShare) * mainMealShare;

        var allocations = new List<MealCalorieAllocation>(4)
        {
            BuildAllocation(MealType.Breakfast, rebalancedBreakfast, totalCalories, dailyMacros),
            BuildAllocation(MealType.Lunch, rebalancedLunch, totalCalories, dailyMacros),
            BuildAllocation(MealType.Dinner, rebalancedDinner, totalCalories, dailyMacros),
            BuildAllocation(MealType.Snack, snackShare, totalCalories, dailyMacros)
        };

        return new CalorieBudget(
            TotalCalories: RoundTo(totalCalories, 10f),
            MainMealCalories: RoundTo(totalCalories * mainMealShare, 10f),
            SnackCalories: RoundTo(totalCalories * snackShare, 10f),
            MainMealShare: mainMealShare,
            SnackShare: snackShare,
            DailyMacros: dailyMacros,
            Allocations: allocations);
    }

    private static (float MainMealShare, float SnackShare) ClampShares(
        float mainMealShare, float snackShare)
    {
        if (mainMealShare >= MinMainMealShare && mainMealShare <= MaxMainMealShare)
            return (mainMealShare, snackShare);

        mainMealShare = Math.Clamp(mainMealShare, MinMainMealShare, MaxMainMealShare);
        snackShare = 1f - mainMealShare;

        return (mainMealShare, snackShare);
    }

    private static (float Breakfast, float Lunch, float Dinner, float Snack) GetMealShares(
        BreakfastPreference preference)
    {
        // Shares aligned with NutritionPlanner.BuildMealTargets
        return preference switch
        {
            BreakfastPreference.Sweet => (0.20f, 0.32f, 0.28f, 0.20f),
            _ => (0.25f, 0.33f, 0.25f, 0.17f)
        };
    }

    private static MacroNutrients ComputeDefaultMacros(float totalCalories)
    {
        var proteinCals = totalCalories * DefaultProteinPercent;
        var carbCals = totalCalories * DefaultCarbPercent;
        var fatCals = totalCalories * DefaultFatPercent;

        return new MacroNutrients(
            Calories: RoundTo(totalCalories, 1f),
            Protein: RoundTo(proteinCals / CaloriesPerGramProtein, 1f),
            Carbs: RoundTo(carbCals / CaloriesPerGramCarb, 1f),
            Fats: RoundTo(fatCals / CaloriesPerGramFat, 1f));
    }

    private static MealCalorieAllocation BuildAllocation(
        MealType mealType, float share, float totalCalories, MacroNutrients dailyMacros)
    {
        var mealCalories = RoundTo(totalCalories * share, 10f);

        var macros = new MacroNutrients(
            Calories: mealCalories,
            Protein: RoundTo(dailyMacros.Protein * share, 1f),
            Carbs: RoundTo(dailyMacros.Carbs * share, 1f),
            Fats: RoundTo(dailyMacros.Fats * share, 1f));

        return new MealCalorieAllocation(mealType, mealCalories, share, macros);
    }

    private static float RoundTo(float value, float nearest)
        => MathF.Round(value / nearest) * nearest;
}
