using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public interface INutritionTrackingService
{
    Task<FoodItemEntryDto?> GetFoodItemAsync(Guid id, CancellationToken ct = default)
    {
        throw new NotSupportedException();
    }

    Task UpdateFoodItemAsync(DateOnly date, FoodItemEntryDto item, CancellationToken ct = default)
    {
        return AddOrUpdateFoodItemAsync(date, item, ct);
    }

    Task ReLogFoodAsync(DateOnly date, MealType mealType, QuickFoodItemDto item,
        FoodItemEntryDto draft, CancellationToken ct = default)
    {
        return AddOrUpdateFoodItemAsync(date, draft, ct);
    }

    Task<DailyNutritionLogDto> GetLogForDateAsync(DateOnly date, CancellationToken ct = default);
    Task AddOrUpdateFoodItemAsync(DateOnly date, FoodItemEntryDto item, CancellationToken ct = default);
    Task AddFoodItemsBatchAsync(DateOnly date, IReadOnlyList<FoodItemEntryDto> items, CancellationToken ct = default);
    Task DeleteFoodItemAsync(Guid itemId, CancellationToken ct = default);

    Task<IReadOnlyList<DailyNutritionSummaryDto>> GetDateRangeSummariesAsync(DateOnly start, DateOnly end,
        CancellationToken ct = default);

    Task UpdateWaterIntakeAsync(DateOnly date, int waterIntakeMl, CancellationToken ct = default);
    Task UpdateNotesAsync(DateOnly date, string? notes, CancellationToken ct = default);

    Task<IReadOnlyList<QuickFoodItemDto>> GetRecentIngredientsAsync(DateOnly date, MealType mealType,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<QuickFoodItemDto>>([]);
    }

    Task<IReadOnlyList<QuickFoodItemDto>> GetRecentAcceptedSuggestionsAsync(DateOnly date, MealType mealType,
        CyclePhase phase, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<QuickFoodItemDto>>([]);
    }

    Task<CyclePhase> GetPhaseForDateAsync(DateOnly date)
    {
        return Task.FromResult(CyclePhase.Unknown);
    }
}
