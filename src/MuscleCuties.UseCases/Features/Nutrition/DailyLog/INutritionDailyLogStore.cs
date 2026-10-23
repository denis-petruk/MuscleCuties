using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.UseCases.Features.Nutrition.DailyLog;

public interface INutritionDailyLogStore
{
    Task<DailyNutritionLogSnapshot> GetLogForDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<LoggedFoodItemDto?> GetFoodItemAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveFoodItemAsync(DateOnly date, LoggedFoodItemDto item, bool requireExisting,
        CancellationToken cancellationToken = default);

    Task BatchSaveFoodItemsAsync(DateOnly date, IReadOnlyList<LoggedFoodItemDto> items,
        CancellationToken cancellationToken = default);

    Task<int> CopyEntriesAsync(DateOnly sourceDate, DateOnly targetDate, MealType? mealType,
        CancellationToken cancellationToken = default);

    Task DeleteFoodItemAsync(Guid itemId, CancellationToken cancellationToken = default);

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
}
