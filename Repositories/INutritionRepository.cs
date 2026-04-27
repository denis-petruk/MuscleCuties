using MuscleCuties.Models;

namespace MuscleCuties.Repositories;

public interface INutritionRepository : IRepository<FoodItem>
{
    Task<List<FoodItem>> SearchFoodItemsAsync(string query);
    Task<List<FoodLog>> GetFoodLogsByDateAsync(int userId, DateTime date);
    Task AddFoodLogAsync(FoodLog log);
    Task DeleteFoodLogAsync(FoodLog log);
}