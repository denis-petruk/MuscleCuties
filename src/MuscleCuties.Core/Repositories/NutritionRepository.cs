using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public class NutritionRepository(AppDatabase db) : BaseRepository<FoodItem>(db), INutritionRepository
{
    public async Task<List<FoodItem>> SearchFoodItemsAsync(string query) =>
        await _db.FoodItems
            .Where(f => f.Name.ToLower().Contains(query.ToLower()))
            .ToListAsync();

    public async Task<List<LoggedMeal>> GetLoggedMealsByDateAsync(int userId, DateTime date) =>
        await _db.LoggedMeals
            .Where(m => m.UserId == userId && m.Date.Date == date.Date)
            .Include(m => m.Entries)
            .ThenInclude(e => e.FoodItem)
            .ToListAsync();

    public async Task AddLoggedMealAsync(LoggedMeal meal)
    {
        await _db.LoggedMeals.AddAsync(meal);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteLoggedMealAsync(LoggedMeal meal)
    {
        _db.LoggedMeals.Remove(meal);
        await _db.SaveChangesAsync();
    }
}