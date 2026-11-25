using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.UseCases.Features.Nutrition.DailyLog;

/// <summary>Whether a free-text food query is usable, and why not when it is not.</summary>
public sealed record GenericFoodSearchQueryValidationDto(bool IsAccepted, string NormalizedQuery);

/// <summary>The daily log operations.</summary>
public interface INutritionDailyLogUseCase
{
    Task<DailyNutritionLogSnapshot> GetLogForDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<LoggedFoodItemDto?> GetFoodItemAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveFoodItemAsync(DateOnly date, LoggedFoodItemDto item, bool requireExisting = false,
        CancellationToken cancellationToken = default);

    Task BatchSaveFoodItemsAsync(DateOnly date, IReadOnlyList<LoggedFoodItemDto> items,
        CancellationToken cancellationToken = default);

    Task DeleteFoodItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task<int> CopyPreviousMealAsync(DateOnly sourceDate, DateOnly targetDate, MealType mealType,
        CancellationToken cancellationToken = default);

    Task<int> CopyEntireDayAsync(DateOnly sourceDate, DateOnly targetDate,
        CancellationToken cancellationToken = default);

    Task ReLogFoodAsync(DateOnly date, MealType mealType, QuickFoodItemSnapshot quickItem, LoggedFoodItemDto draft,
        CancellationToken cancellationToken = default);

    Task UpdateWaterIntakeAsync(DateOnly date, int waterIntakeMl, CancellationToken cancellationToken = default);
    Task UpdateNotesAsync(DateOnly date, string? notes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuickFoodItemSnapshot>> GetRecentIngredientsAsync(DateOnly date, MealType mealType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuickFoodItemSnapshot>> GetRecentAcceptedSuggestionsAsync(DateOnly date, MealType mealType,
        CyclePhase phase, CancellationToken cancellationToken = default);

    Task<CyclePhase> GetPhaseForDateAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FastLookupFoodItemDto>> SearchLocalFoodsAsync(string query, int maxResults = 12,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FastLookupFoodItemDto>> SearchOnlineFoodsAsync(string query, int maxResults = 12,
        CancellationToken cancellationToken = default);

    GenericFoodSearchQueryValidationDto ValidateOnlineFoodSearchQuery(string? query);
}

/// <summary>Adds, edits and removes logged foods, recomputing the day's totals in the same transaction.</summary>
public sealed class NutritionDailyLogUseCase(INutritionDailyLogStore store) : INutritionDailyLogUseCase
{
    /// <summary>Loads the day's nutrition log.</summary>
    public Task<DailyNutritionLogSnapshot> GetLogForDateAsync(DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return store.GetLogForDateAsync(date, cancellationToken);
    }

    /// <summary>One logged food, or null when it is not in her log.</summary>
    public Task<LoggedFoodItemDto?> GetFoodItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return store.GetFoodItemAsync(id, cancellationToken);
    }

    /// <summary>Saves one food item into the day's log.</summary>
    public Task SaveFoodItemAsync(DateOnly date, LoggedFoodItemDto item, bool requireExisting = false,
        CancellationToken cancellationToken = default)
    {
        ValidateFoodItem(item);
        return store.SaveFoodItemAsync(date, item, requireExisting, cancellationToken);
    }

    /// <summary>Saves several food items into the day's log.</summary>
    public Task BatchSaveFoodItemsAsync(DateOnly date, IReadOnlyList<LoggedFoodItemDto> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count < DailyFoodEntry.MinimumBatchItems) return Task.CompletedTask;
        DailyFoodEntry.ValidateBatch(items.Select(ValidateFoodItem).ToList());
        return store.BatchSaveFoodItemsAsync(date, items, cancellationToken);
    }

    /// <summary>Refuses a food with a non-positive amount or unknown nutrients before it is stored.</summary>
    private static DailyFoodEntry ValidateFoodItem(LoggedFoodItemDto item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return DailyFoodEntry.Create(item.MealType, item.Name, item.ServingSize, item.ServingUnit,
            item.ServingsCount, item.Calories, item.Protein, item.Carbs, item.Fat);
    }

    /// <summary>Removes the food and recomputes the day's totals in the same transaction.</summary>
    public Task DeleteFoodItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        if (itemId == Guid.Empty)
            throw new ArgumentException("Valid item ID is required.", nameof(itemId));
        return store.DeleteFoodItemAsync(itemId, cancellationToken);
    }

    /// <summary>Copies one meal from a source day, returning how many entries moved.</summary>
    public async Task<int> CopyPreviousMealAsync(DateOnly sourceDate, DateOnly targetDate, MealType mealType,
        CancellationToken cancellationToken = default)
    {
        DailyFoodEntry.RequireDifferentDates(sourceDate, targetDate);
        DailyFoodEntry.RequireMealType(mealType);
        return await store.CopyEntriesAsync(sourceDate, targetDate, mealType, cancellationToken);
    }

    /// <summary>Copies every entry from a source day, returning how many moved.</summary>
    public async Task<int> CopyEntireDayAsync(DateOnly sourceDate, DateOnly targetDate,
        CancellationToken cancellationToken = default)
    {
        DailyFoodEntry.RequireDifferentDates(sourceDate, targetDate);
        return await store.CopyEntriesAsync(sourceDate, targetDate, null, cancellationToken);
    }

    /// <summary>Logs a previously used food again.</summary>
    public Task ReLogFoodAsync(DateOnly date, MealType mealType, QuickFoodItemSnapshot quickItem,
        LoggedFoodItemDto draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quickItem);
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.MealType != mealType)
            throw new ArgumentException("Meal category cannot differ from requested category.", nameof(draft));
        return store.ReLogFoodAsync(date, mealType, quickItem, draft, cancellationToken);
    }

    /// <summary>Sets the day's water figure, which the hydration screen reads back.</summary>
    public Task UpdateWaterIntakeAsync(DateOnly date, int waterIntakeMl, CancellationToken cancellationToken = default)
    {
        var clampedWater = Math.Max(0, waterIntakeMl);
        return store.UpdateWaterIntakeAsync(date, clampedWater, cancellationToken);
    }

    /// <summary>Stores the day's note, keeping the previous one when the field is unchanged.</summary>
    public Task UpdateNotesAsync(DateOnly date, string? notes, CancellationToken cancellationToken = default)
    {
        return store.UpdateNotesAsync(date, notes, cancellationToken);
    }

    /// <summary>The ingredients recently logged for the meal.</summary>
    public Task<IReadOnlyList<QuickFoodItemSnapshot>> GetRecentIngredientsAsync(DateOnly date, MealType mealType,
        CancellationToken cancellationToken = default)
    {
        return store.GetRecentIngredientsAsync(date, mealType, cancellationToken);
    }

    /// <summary>The suggestions recently accepted for the day.</summary>
    public Task<IReadOnlyList<QuickFoodItemSnapshot>> GetRecentAcceptedSuggestionsAsync(DateOnly date,
        MealType mealType, CyclePhase phase, CancellationToken cancellationToken = default)
    {
        return store.GetRecentAcceptedSuggestionsAsync(date, mealType, phase, cancellationToken);
    }

    /// <summary>The cycle phase that day, which the log uses to explain its targets.</summary>
    public Task<CyclePhase> GetPhaseForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        return store.GetPhaseForDateAsync(date, cancellationToken);
    }

    /// <summary>Searches the local food list.</summary>
    public Task<IReadOnlyList<FastLookupFoodItemDto>> SearchLocalFoodsAsync(string query, int maxResults = 12,
        CancellationToken cancellationToken = default)
    {
        return string.IsNullOrWhiteSpace(query)
            ? Task.FromResult<IReadOnlyList<FastLookupFoodItemDto>>([])
            : store.SearchLocalFoodsAsync(query.Trim(), Math.Clamp(maxResults, 1, 50), cancellationToken);
    }

    /// <summary>Searches the online food catalog.</summary>
    public Task<IReadOnlyList<FastLookupFoodItemDto>> SearchOnlineFoodsAsync(string query, int maxResults = 12,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateOnlineFoodSearchQuery(query);
        return !validation.IsAccepted
            ? Task.FromResult<IReadOnlyList<FastLookupFoodItemDto>>([])
            : store.SearchOnlineFoodsAsync(validation.NormalizedQuery, Math.Clamp(maxResults, 1, 50), cancellationToken);
    }

    /// <summary>Trims the query and reports whether it is usable, so the client can disable search instead of guessing.</summary>
    public GenericFoodSearchQueryValidationDto ValidateOnlineFoodSearchQuery(string? query)
    {
        var normalized = GenericFoodQueryRule.NormalizeOrEmpty(query);
        return new GenericFoodSearchQueryValidationDto(!string.IsNullOrEmpty(normalized), normalized);
    }
}
