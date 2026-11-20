using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Models.Nutrition.Planning;

// --- Calorie Distribution & Meal Planning Models ---

public sealed record CalorieBudget(
    float TotalCalories,
    float MainMealCalories,
    float SnackCalories,
    float MainMealShare,
    float SnackShare,
    MacroNutrients DailyMacros,
    IReadOnlyList<MealCalorieAllocation> Allocations)
{
    public MealCalorieAllocation? GetAllocation(MealType mealType)
    {
        return Allocations.FirstOrDefault(a => a.MealType == mealType);
    }
}

public sealed record MealCalorieAllocation(
    MealType MealType,
    float Calories,
    float Share,
    MacroNutrients Macros);
