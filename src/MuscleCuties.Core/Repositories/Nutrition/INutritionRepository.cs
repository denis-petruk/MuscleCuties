using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Repositories.Nutrition;

public interface INutritionRepository : IRepository<FoodItem>
{
    Task<List<FoodItem>> SearchFoodItemsAsync(string query);
    Task<List<FoodItem>> GetFoodItemsByIdsAsync(IEnumerable<int> foodItemIds);
    Task<FoodItem?> GetFoodItemByFdcIdAsync(int fdcId);
    Task<List<FoodItem>> GetFoodItemsByFdcIdsAsync(IEnumerable<int> fdcIds);
    Task SaveFoodItemsAsync(IReadOnlyCollection<FoodItem> newItems, IReadOnlyCollection<FoodItem> updatedItems);
    Task<List<LoggedMeal>> GetLoggedMealsByDateAsync(int userId, DateTime date);
    Task<List<LoggedMeal>> GetLoggedMealsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate);
    Task<LoggedMeal?> GetLoggedMealAsync(int userId, int loggedMealId);
    Task<FoodItem?> GetFoodItemAsync(int foodItemId);
    Task AddLoggedMealAsync(LoggedMeal meal);
    Task UpdateLoggedMealAsync(LoggedMeal meal);
    Task DeleteLoggedMealAsync(LoggedMeal meal);
}
