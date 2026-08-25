using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Inputs;
using MuscleCuties.Core.Services.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition;

public interface INutritionService
{
    Task<NutritionPlan> GetDailyPlanAsync(int userId, CyclePhase phase, DateTime date);
    Task<(float Calories, float Protein, float Carbs, float Fats)> CalculateDailyTargetsAsync(int userId, CyclePhase phase);
    Task<MacroNutrients> GetConsumedTotalsAsync(int userId, DateTime date);
    Task<float> GetConsumedCaloriesAsync(int userId, DateTime date);
    Task<(float Protein, float Carbs, float Fats)> GetConsumedMacrosAsync(int userId, DateTime date);
    Task<List<FoodItem>> SearchFoodItemsAsync(string query, int pageSize = 15, int pageNumber = 1);
    Task<FoodItem> CreateCustomFoodAsync(CustomFoodInput input);
    Task<List<LoggedMeal>> GetLoggedMealsByDateAsync(int userId, DateTime date);
    Task<LoggedMeal?> GetLoggedMealAsync(int userId, int loggedMealId);
    Task<List<MealTemplate>> GetReadyMealTemplatesAsync(int userId, CyclePhase phase, DateTime date);
    Task LogFoodAsync(int userId, int foodItemId, float grams, MealType mealType, DateTime loggedAt);
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
