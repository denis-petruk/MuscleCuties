using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Services.Nutrition;

namespace MuscleCuties.Core.Repositories.Nutrition;

public class NutritionRepository(AppDatabase db) : BaseRepository<FoodItem>(db), INutritionRepository
{
    private static readonly Regex SearchCleanupRegex = new(
        "[^a-z0-9]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<List<FoodItem>> SearchFoodItemsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var tokens = SearchCleanupRegex
            .Replace(query.ToLowerInvariant(), " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 1 && !FoodSearchTextNormalizer.StopWords.Contains(token))
            .ToList();

        if (tokens.Count == 0)
            return [];

        var foodItems = _db.FoodItems
            .AsNoTracking();

        foreach (var token in tokens)
        {
            var capturedToken = token;
            var normalizedToken = FoodSearchTextNormalizer.NormalizeToken(capturedToken);
            var hasNormalizedVariant =
                !string.Equals(capturedToken, normalizedToken, StringComparison.OrdinalIgnoreCase);

            var tokenPattern = $"%{capturedToken}%";
            var normalizedPattern = $"%{normalizedToken}%";

            foodItems = foodItems.Where(f =>
                EF.Functions.Like(f.Name, tokenPattern) ||
                (hasNormalizedVariant && EF.Functions.Like(f.Name, normalizedPattern)) ||
                (f.BrandName != null && EF.Functions.Like(f.BrandName, tokenPattern)) ||
                (hasNormalizedVariant && f.BrandName != null && EF.Functions.Like(f.BrandName, normalizedPattern)) ||
                (f.BrandOwner != null && EF.Functions.Like(f.BrandOwner, tokenPattern)) ||
                (hasNormalizedVariant && f.BrandOwner != null && EF.Functions.Like(f.BrandOwner, normalizedPattern)) ||
                (f.GtinUpc != null && EF.Functions.Like(f.GtinUpc, tokenPattern)) ||
                (hasNormalizedVariant && f.GtinUpc != null && EF.Functions.Like(f.GtinUpc, normalizedPattern)) ||
                (f.Ingredients != null && EF.Functions.Like(f.Ingredients, tokenPattern)));
        }

        return await ReadAsync(() => foodItems
            .OrderBy(f => f.Name)
            .ThenBy(f => f.BrandOwner)
            .ThenBy(f => f.BrandName)
            .ToListAsync());
    }

    public async Task<List<FoodItem>> GetFoodItemsByIdsAsync(IEnumerable<int> foodItemIds)
    {
        var ids = NormalizeDistinctIds(foodItemIds);
        if (ids.Count == 0)
            return [];

        return await ReadAsync(() => _db.FoodItems
            .AsNoTracking()
            .Where(food => ids.Contains(food.Id))
            .ToListAsync());
    }

    public async Task<FoodItem?> GetFoodItemByFdcIdAsync(int fdcId)
    {
        return await ReadAsync(() => _db.FoodItems
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FdcId == fdcId));
    }

    public async Task<List<FoodItem>> GetFoodItemsByFdcIdsAsync(IEnumerable<int> fdcIds)
    {
        var ids = NormalizeDistinctIds(fdcIds);
        if (ids.Count == 0)
            return [];

        return await ReadAsync(() => _db.FoodItems
            .AsNoTracking()
            .Where(food => food.FdcId.HasValue && ids.Contains(food.FdcId.Value))
            .ToListAsync());
    }

    public async Task SaveFoodItemsAsync(
        IReadOnlyCollection<FoodItem> newItems,
        IReadOnlyCollection<FoodItem> updatedItems)
    {
        if (newItems.Count == 0 && updatedItems.Count == 0)
            return;

        if (newItems.Count > 0)
            await _db.FoodItems.AddRangeAsync(newItems);

        if (updatedItems.Count > 0)
        {
            foreach (var item in updatedItems)
                DetachTrackedLocal(item);

            _db.FoodItems.UpdateRange(updatedItems);
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<LoggedMeal>> GetLoggedMealsByDateAsync(int userId, DateTime date)
    {
        return await ReadAsync(() => _db.LoggedMeals
            .AsNoTracking()
            .Where(m => m.UserId == userId &&
                        ((m.LoggedAt != default && m.LoggedAt >= date.Date &&
                          m.LoggedAt <= date.Date.AddTicks(TimeSpan.TicksPerDay - 1)) ||
                         (m.LoggedAt == default && m.Date == date.Date)))
            .Include(m => m.Entries)
            .ThenInclude(e => e.FoodItem)
            .OrderBy(m => m.LoggedAt)
            .ToListAsync());
    }

    public async Task<List<LoggedMeal>> GetLoggedMealsByDateRangeAsync(
        int userId,
        DateTime startDate,
        DateTime endDate)
    {
        var rangeStart = startDate.Date;
        var rangeEnd = endDate.Date.AddTicks(TimeSpan.TicksPerDay - 1);

        return await ReadAsync(() => _db.LoggedMeals
            .AsNoTracking()
            .Where(m => m.UserId == userId &&
                        ((m.LoggedAt != default && m.LoggedAt >= rangeStart && m.LoggedAt <= rangeEnd) ||
                         (m.LoggedAt == default && m.Date >= rangeStart && m.Date <= rangeEnd)))
            .OrderByDescending(m => m.LoggedAt)
            .ToListAsync());
    }

    public async Task<LoggedMeal?> GetLoggedMealAsync(int userId, int loggedMealId)
    {
        return await ReadAsync(() => _db.LoggedMeals
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.Id == loggedMealId)
            .Include(m => m.Entries)
            .ThenInclude(e => e.FoodItem)
            .FirstOrDefaultAsync());
    }

    public async Task AddLoggedMealAsync(LoggedMeal meal)
    {
        var now = DateTime.UtcNow;

        if (meal.CreatedAt == default)
            meal.CreatedAt = now;

        if (meal.LoggedAt == default)
        {
            var date = meal.Date == default ? now.Date : meal.Date.Date;
            meal.LoggedAt = date.Add(meal.CreatedAt.TimeOfDay);
        }

        meal.Date = meal.LoggedAt.Date;

        await _db.LoggedMeals.AddAsync(meal);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateLoggedMealAsync(LoggedMeal meal)
    {
        var existing = await _db.LoggedMeals
            .Include(m => m.Entries)
            .FirstOrDefaultAsync(m => m.UserId == meal.UserId && m.Id == meal.Id);

        if (existing is null)
            throw new InvalidOperationException("Meal was not found.");

        existing.LoggedAt = meal.LoggedAt;
        existing.Date = meal.LoggedAt.Date;
        existing.MealType = meal.MealType;
        _db.LoggedMealEntries.RemoveRange(existing.Entries);
        existing.Entries.Clear();
        foreach (var entry in meal.Entries)
            existing.Entries.Add(new LoggedMealEntry
            {
                FoodItemId = entry.FoodItemId,
                Grams = entry.Grams
            });

        await _db.SaveChangesAsync();
    }

    public async Task DeleteLoggedMealAsync(int userId, int loggedMealId)
    {
        var existing = await _db.LoggedMeals
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Id == loggedMealId);

        if (existing is null)
            return;

        _db.LoggedMeals.Remove(existing);
        await _db.SaveChangesAsync();
    }

    public async Task<(double Calories, double Protein, double Carbs, double Fat)> GetDailyNutritionLogTotalsAsync(
        int userId, DateTime date)
    {
        var dateStr = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var log = await ReadAsync(() => _db.DailyNutritionLogs
            .AsNoTracking()
            .Include(d => d.Entries.Where(e => !e.IsDeleted))
            .FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted && d.Date == dateStr));

        if (log is null || log.Entries.Count == 0)
            return (0, 0, 0, 0);

        return (
            log.Entries.Sum(e => e.Calories),
            log.Entries.Sum(e => e.Protein),
            log.Entries.Sum(e => e.Carbs),
            log.Entries.Sum(e => e.Fat));
    }

    private static List<int> NormalizeDistinctIds(IEnumerable<int> ids)
    {
        return ids.Where(id => id > 0).Distinct().ToList();
    }
}
