using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public interface INutritionRepository : IRepository<FoodItem>
{
    Task<List<FoodItem>> SearchFoodItemsAsync(string query);
    Task<List<LoggedMeal>> GetLoggedMealsByDateAsync(int userId, DateTime date);
    Task AddLoggedMealAsync(LoggedMeal meal);
    Task DeleteLoggedMealAsync(LoggedMeal meal);
}