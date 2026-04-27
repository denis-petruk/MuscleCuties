using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public interface INutritionRepository : IRepository<FoodItem>
{
    Task<List<FoodItem>> SearchFoodItemsAsync(string query);
    Task<List<FoodLog>> GetFoodLogsByDateAsync(int userId, DateTime date);
    Task AddFoodLogAsync(FoodLog log);
    Task DeleteFoodLogAsync(FoodLog log);
}
