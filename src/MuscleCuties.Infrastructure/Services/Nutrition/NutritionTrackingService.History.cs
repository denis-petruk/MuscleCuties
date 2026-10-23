using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public partial class NutritionTrackingService
{
    public async Task<IReadOnlyList<QuickFoodItemDto>> GetRecentIngredientsAsync(
        DateOnly date, MealType mealType, CancellationToken ct = default)
    {
        var userId = await GetCurrentUserIdSafeAsync().ConfigureAwait(false);
        var start = date.DayNumber >= DateOnly.MinValue.DayNumber + 14 ? date.AddDays(-14) : DateOnly.MinValue;
        var first = start.ToDateTime(TimeOnly.MinValue);
        var last = date.ToDateTime(TimeOnly.MaxValue);
        var firstStr = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var lastStr = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
#pragma warning disable CA1309 // ISO date text sorts chronologically in SQLite.
        var entries = await _db.FoodItemEntries.AsNoTracking()
            .Where(e => e.DailyLog != null && e.DailyLog.UserId == userId && !e.DailyLog.IsDeleted &&
                        !e.IsDeleted && e.MealType == mealType && e.ServingUnit != "meal" &&
                        string.Compare(e.Date, firstStr) >= 0 && string.Compare(e.Date, lastStr) <= 0)
            .ToListAsync(ct).ConfigureAwait(false);
#pragma warning restore CA1309
        var meals = await MealQuery(userId)
            .Where(m => m.MealType == mealType &&
                        ((m.Date >= first && m.Date <= last) ||
                         (m.Date == default && m.LoggedAt >= first && m.LoggedAt <= last)))
            .ToListAsync(ct).ConfigureAwait(false);

        var candidates = entries.Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .Select(e => new QuickFoodItemDto(e.Name.Trim(), e.ServingSize, e.ServingUnit,
                    e.ServingsCount, e.Calories, e.Protein, e.Carbs, e.Fat, string.Empty)
                { SourceId = e.Id })
            .Select(e => (Item: e, At: entries.First(x => x.Id == e.SourceId).LoggedAtTimestamp))
            .ToList();
        foreach (var meal in meals)
        foreach (var ingredient in meal.Entries.Where(e => e.FoodItem is not null && e.Grams > 0))
        {
            var food = ingredient.FoodItem!;
            var ratio = ingredient.Grams / 100d;
            candidates.Add((new QuickFoodItemDto(food.Name.Trim(), ingredient.Grams, "g", 1,
                    Math.Round(food.Calories * ratio, 1), Math.Round(food.Protein * ratio, 1),
                    Math.Round(food.Carbs * ratio, 1), Math.Round(food.Fats * ratio, 1), string.Empty)
                {
                    SourceId = CanonicalMealIdentity.ForLoggedMeal(meal.Id), IngredientFoodItemId = food.Id,
                    Ingredients = [new HistoryIngredientDto(food.Id, food.Name, ingredient.Grams)]
                },
                meal.LoggedAt != default ? meal.LoggedAt : meal.Date));
        }

        await EnsureCurrentUserAsync(userId).ConfigureAwait(false);
        return candidates.GroupBy(x => x.Item.Name, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count()).ThenByDescending(g => g.Max(x => x.At))
            .Take(8).Select(g =>
            {
                var latest = g.OrderByDescending(x => x.At).First().Item;
                return latest with
                {
                    Detail = $"Logged {g.Count()} time{(g.Count() == 1 ? "" : "s")} in the last 14 days"
                };
            }).ToList();
    }

    public async Task<IReadOnlyList<QuickFoodItemDto>> GetRecentAcceptedSuggestionsAsync(
        DateOnly date, MealType mealType, CyclePhase phase, CancellationToken ct = default)
    {
        var userId = await GetCurrentUserIdSafeAsync().ConfigureAwait(false);
        var start = date.DayNumber >= DateOnly.MinValue.DayNumber + 30 ? date.AddDays(-30) : DateOnly.MinValue;
        var first = start.ToDateTime(TimeOnly.MinValue);
        var last = date.ToDateTime(TimeOnly.MaxValue);
        var meals = await MealQuery(userId)
            .Where(m => m.MealType == mealType && m.AcceptedSuggestionName != null &&
                        (phase == CyclePhase.Unknown || m.AcceptedSuggestionPhase == phase) &&
                        ((m.LoggedAt != default && m.LoggedAt >= first && m.LoggedAt <= last) ||
                         (m.LoggedAt == default && m.Date >= first && m.Date <= last)))
            .OrderByDescending(m => m.LoggedAt).ThenByDescending(m => m.Id)
            .ToListAsync(ct).ConfigureAwait(false);
        await EnsureCurrentUserAsync(userId).ConfigureAwait(false);
        return meals.Where(m => m.Entries.Count > 0 && m.Entries.All(e => e.FoodItem is not null && e.Grams > 0))
            .DistinctBy(m => m.AcceptedSuggestionName, StringComparer.OrdinalIgnoreCase).Take(6)
            .Select(m =>
            {
                var mapped = MapMeal(m, Guid.Empty);
                var detail = phase == CyclePhase.Unknown && m.AcceptedSuggestionPhase is not null &&
                             m.AcceptedSuggestionPhase != CyclePhase.Unknown
                    ? $"Previously logged in {m.AcceptedSuggestionPhase} phase (all phases)"
                    : phase != CyclePhase.Unknown
                        ? $"Previously logged in {phase} phase"
                        : "Previously logged suggestion (all phases)";
                return new QuickFoodItemDto(mapped.Name, 1, "meal", 1,
                        mapped.Calories, mapped.Protein, mapped.Carbs, mapped.Fat, detail)
                    { SourceId = mapped.Id, Ingredients = mapped.Ingredients };
            }).ToList();
    }

    public async Task<CyclePhase> GetPhaseForDateAsync(DateOnly date)
    {
        if (_cycleService is null) return CyclePhase.Unknown;
        var userId = await GetCurrentUserIdSafeAsync().ConfigureAwait(false);
        var phase = await _cycleService.GetPhaseForDateAsync(userId, date.ToDateTime(TimeOnly.MinValue))
            .ConfigureAwait(false);
        await EnsureCurrentUserAsync(userId).ConfigureAwait(false);
        return phase;
    }

    private IQueryable<LoggedMeal> MealQuery(int userId)
    {
        return _db.LoggedMeals.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Include(m => m.Entries).ThenInclude(e => e.FoodItem);
    }

    private async Task<List<LoggedMeal>> MealsAsync(int userId, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var first = start.ToDateTime(TimeOnly.MinValue);
        var last = end.ToDateTime(TimeOnly.MaxValue);
        return await MealQuery(userId)
            .Where(m => (m.LoggedAt != default && m.LoggedAt >= first && m.LoggedAt <= last) ||
                        (m.LoggedAt == default && m.Date >= first && m.Date <= last))
            .OrderBy(m => m.LoggedAt).ThenBy(m => m.Id)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public static DateOnly MealDate(LoggedMeal meal)
    {
        return DateOnly.FromDateTime(meal.LoggedAt != default ? meal.LoggedAt : meal.Date);
    }

    private static FoodItemEntryDto MapMeal(LoggedMeal meal, Guid dailyLogId)
    {
        var ingredients = meal.Entries
            .Where(e => e.FoodItem is not null && e.Grams > 0)
            .OrderBy(e => e.Id)
            .Select(e => new HistoryIngredientDto(e.FoodItemId, e.FoodItem!.Name, e.Grams))
            .ToList();
        var names = ingredients.Select(i => i.Name.Trim())
            .Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var name = !string.IsNullOrWhiteSpace(meal.AcceptedSuggestionName)
            ? meal.AcceptedSuggestionName.Trim()
            : names.Count switch
            {
                0 => "Logged meal",
                1 => names[0],
                2 => $"{names[0]} + {names[1]}",
                _ => $"{names[0]} + {names[1]} + {names.Count - 2} more"
            };
        var entries = meal.Entries.Where(e => e.FoodItem is not null && e.Grams > 0).ToList();
        var date = MealDate(meal);
        var time = TimeOnly.FromDateTime(meal.LoggedAt == default ? meal.Date : meal.LoggedAt);
        return new FoodItemEntryDto(
            CanonicalMealIdentity.ForLoggedMeal(meal.Id), dailyLogId, meal.MealType, name,
            entries.Sum(e => (double)e.Grams), "g", 1,
            Math.Round(entries.Sum(e => e.FoodItem!.Calories * e.Grams / 100d), 1),
            Math.Round(entries.Sum(e => e.FoodItem!.Protein * e.Grams / 100d), 1),
            Math.Round(entries.Sum(e => e.FoodItem!.Carbs * e.Grams / 100d), 1),
            Math.Round(entries.Sum(e => e.FoodItem!.Fats * e.Grams / 100d), 1),
            date.ToDateTime(time))
        {
            Ingredients = ingredients,
            AcceptedSuggestionName = meal.AcceptedSuggestionName,
            AcceptedSuggestionPhase = meal.AcceptedSuggestionPhase
        };
    }
}
