using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Models.Nutrition.Planning;

// --- Calorie Distribution & Meal Planning Models ---

/// <summary>The day's calorie budget, which the meal allocation divides.</summary>
public sealed record CalorieBudget(
    float TotalCalories,
    float MainMealCalories,
    float SnackCalories,
    float MainMealShare,
    float SnackShare,
    MacroNutrients DailyMacros,
    IReadOnlyList<MealCalorieAllocation> Allocations)
{
    /// <summary>The calories one meal receives from the budget.</summary>
    public MealCalorieAllocation? GetAllocation(MealType mealType)
    {
        return Allocations.FirstOrDefault(a => a.MealType == mealType);
    }
}

/// <summary>One meal's share of the day's calories.</summary>
public sealed record MealCalorieAllocation(
    MealType MealType,
    float Calories,
    float Share,
    MacroNutrients Macros);
