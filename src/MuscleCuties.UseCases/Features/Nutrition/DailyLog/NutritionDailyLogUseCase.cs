using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.UseCases.Features.Nutrition.DailyLog;

public sealed record GenericFoodSearchQueryValidationDto(bool IsAccepted, string NormalizedQuery);

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

    Task<IReadOnlyList<DailyNutritionSummarySnapshot>> GetDateRangeSummariesAsync(DateOnly start, DateOnly end,
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

public sealed class NutritionDailyLogUseCase(INutritionDailyLogStore store) : INutritionDailyLogUseCase
{
    public Task<DailyNutritionLogSnapshot> GetLogForDateAsync(DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return store.GetLogForDateAsync(date, cancellationToken);
    }

    public Task<LoggedFoodItemDto?> GetFoodItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return store.GetFoodItemAsync(id, cancellationToken);
    }

    public Task SaveFoodItemAsync(DateOnly date, LoggedFoodItemDto item, bool requireExisting = false,
        CancellationToken cancellationToken = default)
    {
        ValidateFoodItem(item);
        return store.SaveFoodItemAsync(date, item, requireExisting, cancellationToken);
    }

    public Task BatchSaveFoodItemsAsync(DateOnly date, IReadOnlyList<LoggedFoodItemDto> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count < DailyFoodEntry.MinimumBatchItems) return Task.CompletedTask;
        DailyFoodEntry.ValidateBatch(items.Select(ValidateFoodItem).ToList());
        return store.BatchSaveFoodItemsAsync(date, items, cancellationToken);
    }

    private static DailyFoodEntry ValidateFoodItem(LoggedFoodItemDto item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return DailyFoodEntry.Create(item.MealType, item.Name, item.ServingSize, item.ServingUnit,
            item.ServingsCount, item.Calories, item.Protein, item.Carbs, item.Fat);
    }

    public Task DeleteFoodItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        if (itemId == Guid.Empty)
            throw new ArgumentException("Valid item ID is required.", nameof(itemId));
        return store.DeleteFoodItemAsync(itemId, cancellationToken);
    }

    public async Task<int> CopyPreviousMealAsync(DateOnly sourceDate, DateOnly targetDate, MealType mealType,
        CancellationToken cancellationToken = default)
    {
        DailyFoodEntry.RequireDifferentDates(sourceDate, targetDate);
        DailyFoodEntry.RequireMealType(mealType);
        return await store.CopyEntriesAsync(sourceDate, targetDate, mealType, cancellationToken);
    }

    public async Task<int> CopyEntireDayAsync(DateOnly sourceDate, DateOnly targetDate,
        CancellationToken cancellationToken = default)
    {
        DailyFoodEntry.RequireDifferentDates(sourceDate, targetDate);
        return await store.CopyEntriesAsync(sourceDate, targetDate, null, cancellationToken);
    }

    public Task ReLogFoodAsync(DateOnly date, MealType mealType, QuickFoodItemSnapshot quickItem,
        LoggedFoodItemDto draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quickItem);
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.MealType != mealType)
            throw new ArgumentException("Meal category cannot differ from requested category.", nameof(draft));
        return store.ReLogFoodAsync(date, mealType, quickItem, draft, cancellationToken);
    }

    public Task<IReadOnlyList<DailyNutritionSummarySnapshot>> GetDateRangeSummariesAsync(DateOnly start, DateOnly end,
        CancellationToken cancellationToken = default)
    {
        if (end < start)
            throw new ArgumentOutOfRangeException(nameof(end), "End date cannot precede start date.");
        return store.GetDateRangeSummariesAsync(start, end, cancellationToken);
    }

    public Task UpdateWaterIntakeAsync(DateOnly date, int waterIntakeMl, CancellationToken cancellationToken = default)
    {
        var clampedWater = Math.Max(0, waterIntakeMl);
        return store.UpdateWaterIntakeAsync(date, clampedWater, cancellationToken);
    }

    public Task UpdateNotesAsync(DateOnly date, string? notes, CancellationToken cancellationToken = default)
    {
        return store.UpdateNotesAsync(date, notes, cancellationToken);
    }

    public Task<IReadOnlyList<QuickFoodItemSnapshot>> GetRecentIngredientsAsync(DateOnly date, MealType mealType,
        CancellationToken cancellationToken = default)
    {
        return store.GetRecentIngredientsAsync(date, mealType, cancellationToken);
    }

    public Task<IReadOnlyList<QuickFoodItemSnapshot>> GetRecentAcceptedSuggestionsAsync(DateOnly date,
        MealType mealType, CyclePhase phase, CancellationToken cancellationToken = default)
    {
        return store.GetRecentAcceptedSuggestionsAsync(date, mealType, phase, cancellationToken);
    }

    public Task<CyclePhase> GetPhaseForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        return store.GetPhaseForDateAsync(date, cancellationToken);
    }

    public Task<IReadOnlyList<FastLookupFoodItemDto>> SearchLocalFoodsAsync(string query, int maxResults = 12,
        CancellationToken cancellationToken = default)
    {
        return string.IsNullOrWhiteSpace(query)
            ? Task.FromResult<IReadOnlyList<FastLookupFoodItemDto>>([])
            : store.SearchLocalFoodsAsync(query.Trim(), Math.Clamp(maxResults, 1, 50), cancellationToken);
    }

    public Task<IReadOnlyList<FastLookupFoodItemDto>> SearchOnlineFoodsAsync(string query, int maxResults = 12,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateOnlineFoodSearchQuery(query);
        return !validation.IsAccepted
            ? Task.FromResult<IReadOnlyList<FastLookupFoodItemDto>>([])
            : store.SearchOnlineFoodsAsync(validation.NormalizedQuery, Math.Clamp(maxResults, 1, 50), cancellationToken);
    }

    public GenericFoodSearchQueryValidationDto ValidateOnlineFoodSearchQuery(string? query)
    {
        var normalized = GenericFoodQueryRule.NormalizeOrEmpty(query);
        return new GenericFoodSearchQueryValidationDto(!string.IsNullOrEmpty(normalized), normalized);
    }
}
