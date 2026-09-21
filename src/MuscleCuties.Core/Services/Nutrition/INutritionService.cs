using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Inputs;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition;

public interface INutritionService
{
    Task<NutritionPlan> GetDailyPlanAsync(
        int userId,
        CyclePhase phase,
        DateTime date,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury);

    Task<(float Calories, float Protein, float Carbs, float Fats)> CalculateDailyTargetsAsync(int userId,
        CyclePhase phase);

    Task<MacroNutrients> GetConsumedTotalsAsync(int userId, DateTime date);
    Task<List<FoodItem>> SearchFoodItemsAsync(string query, int pageSize = 15, int pageNumber = 1);
    Task<FoodItem> CreateCustomFoodAsync(CustomFoodInput input);
    Task<List<LoggedMeal>> GetLoggedMealsByDateAsync(int userId, DateTime date);
    Task<LoggedMeal?> GetLoggedMealAsync(int userId, int loggedMealId);
    Task<IReadOnlyList<SuggestedMeal>> GetSuggestedMealsAsync(
        int userId,
        MealType mealType,
        BreakfastPreference breakfastPreference,
        CyclePhase phase,
        DateTime date,
        IReadOnlySet<string>? excludeConceptNames = null);
    Task LogMealAsync(
        int userId,
        IReadOnlyCollection<MealIngredientInput> ingredients,
        MealType mealType,
        DateTime loggedAt);

    Task UpdateMealAsync(
        int userId,
        int loggedMealId,
        IReadOnlyCollection<MealIngredientInput> ingredients,
        MealType mealType,
        DateTime loggedAt);

}
