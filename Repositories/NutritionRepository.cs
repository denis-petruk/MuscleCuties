using Microsoft.EntityFrameworkCore;
using MuscleCuties.Data;
using MuscleCuties.Models;

namespace MuscleCuties.Repositories;

public class NutritionRepository(AppDatabase db) : BaseRepository<FoodItem>(db), INutritionRepository
{
    public async Task<List<FoodItem>> SearchFoodItemsAsync(string query) =>
        await _db.FoodItems
            .Where(f => f.Name.ToLower().Contains(query.ToLower()))
            .ToListAsync();

    public async Task<List<FoodLog>> GetFoodLogsByDateAsync(int userId, DateTime date) =>
        await _db.FoodLogs
            .Where(l => l.UserId == userId && l.Date == date.Date)
            .Include(l => l.FoodItem)
            .ToListAsync();

    public async Task AddFoodLogAsync(FoodLog log)
    {
        await _db.FoodLogs.AddAsync(log);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteFoodLogAsync(FoodLog log)
    {
        _db.FoodLogs.Remove(log);
        await _db.SaveChangesAsync();
    }
}