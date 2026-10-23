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

public sealed record FallbackMealItem(
    string Name,
    string Description,
    MealType MealType,
    MacroNutrients Macros,
    IReadOnlyList<string> IngredientNames)
{
    public float Calories => Macros.Calories;
}

public sealed record MealPlan(
    CalorieBudget Budget,
    IReadOnlyList<MealPlanEntry> Entries)
{
    public MacroNutrients TotalMacros
        => MacroNutrients.Sum(Entries.Select(e => e.Meal.Macros));

    public IReadOnlyList<MealPlanEntry> MainMeals
        => Entries.Where(e => e.Meal.MealType is not MealType.Snack).ToList();

    public IReadOnlyList<MealPlanEntry> Snacks
        => Entries.Where(e => e.Meal.MealType is MealType.Snack).ToList();
}

public sealed record MealPlanEntry(
    FallbackMealItem Meal,
    MealCalorieAllocation Target);
