using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

// A page may outlive a database scope. Each operation owns and disposes its context,
// so navigation, notifications and writes cannot run concurrently on one DbContext.
public sealed class ScopedNutritionTrackingService(IServiceScopeFactory scopes) : INutritionTrackingService
{
    public Task<DailyNutritionLogDto> GetLogForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        return RunAsync(s => s.GetLogForDateAsync(date, ct));
    }

    public Task<FoodItemEntryDto?> GetFoodItemAsync(Guid id, CancellationToken ct = default)
    {
        return RunAsync(s => s.GetFoodItemAsync(id, ct));
    }

    public Task AddOrUpdateFoodItemAsync(DateOnly date, FoodItemEntryDto item, CancellationToken ct = default)
    {
        return RunAsync(s => s.AddOrUpdateFoodItemAsync(date, item, ct));
    }

    public Task AddFoodItemsBatchAsync(DateOnly date, IReadOnlyList<FoodItemEntryDto> items, CancellationToken ct = default)
    {
        return RunAsync(s => s.AddFoodItemsBatchAsync(date, items, ct));
    }

    public Task UpdateFoodItemAsync(DateOnly date, FoodItemEntryDto item, CancellationToken ct = default)
    {
        return RunAsync(s => s.UpdateFoodItemAsync(date, item, ct));
    }

    public Task DeleteFoodItemAsync(Guid id, CancellationToken ct = default)
    {
        return RunAsync(s => s.DeleteFoodItemAsync(id, ct));
    }

    public Task ReLogFoodAsync(DateOnly date, MealType mealType, QuickFoodItemDto item, FoodItemEntryDto draft,
        CancellationToken ct = default)
    {
        return RunAsync(s => s.ReLogFoodAsync(date, mealType, item, draft, ct));
    }

    public Task<IReadOnlyList<DailyNutritionSummaryDto>> GetDateRangeSummariesAsync(DateOnly start, DateOnly end,
        CancellationToken ct = default)
    {
        return RunAsync(s => s.GetDateRangeSummariesAsync(start, end, ct));
    }

    public Task UpdateWaterIntakeAsync(DateOnly date, int amount, CancellationToken ct = default)
    {
        return RunAsync(s => s.UpdateWaterIntakeAsync(date, amount, ct));
    }

    public Task UpdateNotesAsync(DateOnly date, string? notes, CancellationToken ct = default)
    {
        return RunAsync(s => s.UpdateNotesAsync(date, notes, ct));
    }

    public Task<IReadOnlyList<QuickFoodItemDto>> GetRecentIngredientsAsync(DateOnly date, MealType mealType,
        CancellationToken ct = default)
    {
        return RunAsync(s => s.GetRecentIngredientsAsync(date, mealType, ct));
    }

    public Task<IReadOnlyList<QuickFoodItemDto>> GetRecentAcceptedSuggestionsAsync(DateOnly date, MealType mealType,
        CyclePhase phase, CancellationToken ct = default)
    {
        return RunAsync(s => s.GetRecentAcceptedSuggestionsAsync(date, mealType, phase, ct));
    }

    public Task<CyclePhase> GetPhaseForDateAsync(DateOnly date)
    {
        return RunAsync(s => s.GetPhaseForDateAsync(date));
    }

    private async Task<T> RunAsync<T>(Func<NutritionTrackingService, Task<T>> action)
    {
        using var scope = scopes.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<NutritionTrackingService>()).ConfigureAwait(false);
    }

    private Task RunAsync(Func<NutritionTrackingService, Task> action)
    {
        return RunAsync(async service =>
        {
            await action(service).ConfigureAwait(false);
            return true;
        });
    }
}
