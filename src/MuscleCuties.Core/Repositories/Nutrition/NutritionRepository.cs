using System.Text.RegularExpressions;
using MuscleCuties.Core.Repositories.Common;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Repositories.Nutrition;

public class NutritionRepository(AppDatabase db) : BaseRepository<FoodItem>(db), INutritionRepository
{
    private static readonly Regex SearchCleanupRegex = new(
        "[^a-z0-9]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "an",
        "and",
        "for",
        "in",
        "of",
        "or",
        "the",
        "to",
        "with"
    };

    public async Task<List<FoodItem>> SearchFoodItemsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var tokens = SearchCleanupRegex
            .Replace(query.ToLowerInvariant(), " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 1 && !StopWords.Contains(token))
            .ToList();

        if (tokens.Count == 0)
            return [];

        var foodItems = _db.FoodItems
            .AsNoTracking()
            .AsQueryable();

        foreach (var token in tokens)
        {
            var capturedToken = token;
            var normalizedToken = NormalizeToken(capturedToken);
            var hasNormalizedVariant = !string.Equals(capturedToken, normalizedToken, StringComparison.OrdinalIgnoreCase);

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

        return await foodItems
            .OrderBy(f => f.Name)
            .ThenBy(f => f.BrandOwner)
            .ThenBy(f => f.BrandName)
            .ToListAsync();
    }

    private static string NormalizeToken(string token)
    {
        if (token.Length <= 3 || token.Any(char.IsDigit))
            return token;

        if (token.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
            return $"{token[..^3]}y";

        if (token.EndsWith("oes", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("ses", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("xes", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("zes", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("ches", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("shes", StringComparison.OrdinalIgnoreCase))
        {
            return token[..^2];
        }

        if (token.EndsWith('s') && !token.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
            return token[..^1];

        return token;
    }

    public async Task<List<FoodItem>> GetFoodItemsByIdsAsync(IEnumerable<int> foodItemIds)
    {
        var ids = foodItemIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await _db.FoodItems
            .AsNoTracking()
            .Where(food => ids.Contains(food.Id))
            .ToListAsync();
    }

    public async Task<FoodItem?> GetFoodItemByFdcIdAsync(int fdcId) =>
        await _db.FoodItems
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FdcId == fdcId);

    public async Task<List<FoodItem>> GetFoodItemsByFdcIdsAsync(IEnumerable<int> fdcIds)
    {
        var ids = fdcIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await _db.FoodItems
            .AsNoTracking()
            .Where(food => food.FdcId.HasValue && ids.Contains(food.FdcId.Value))
            .ToListAsync();
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

    public async Task<FoodItem?> GetFoodItemAsync(int foodItemId) =>
        await _db.FoodItems
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == foodItemId);

    public async Task<List<LoggedMeal>> GetLoggedMealsByDateAsync(int userId, DateTime date) =>
        await _db.LoggedMeals
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.LoggedAt >= date.Date && m.LoggedAt < date.Date.AddDays(1))
            .Include(m => m.Entries)
            .ThenInclude(e => e.FoodItem)
            .OrderBy(m => m.LoggedAt)
            .ToListAsync();

    public async Task<LoggedMeal?> GetLoggedMealAsync(int userId, int loggedMealId) =>
        await _db.LoggedMeals
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.Id == loggedMealId)
            .Include(m => m.Entries)
            .ThenInclude(e => e.FoodItem)
            .FirstOrDefaultAsync();

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
        existing.MealTemplateId = meal.MealTemplateId;

        _db.LoggedMealEntries.RemoveRange(existing.Entries);
        foreach (var entry in meal.Entries)
        {
            existing.Entries.Add(new LoggedMealEntry
            {
                FoodItemId = entry.FoodItemId,
                Grams = entry.Grams
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteLoggedMealAsync(LoggedMeal meal)
    {
        _db.LoggedMeals.Remove(meal);
        await _db.SaveChangesAsync();
    }
}
