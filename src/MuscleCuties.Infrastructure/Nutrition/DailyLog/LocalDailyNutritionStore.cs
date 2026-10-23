using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;

namespace MuscleCuties.Infrastructure.Nutrition.DailyLog;

public sealed class LocalDailyNutritionStore(
    IServiceScopeFactory scopeFactory
) : INutritionDailyLogStore
{
    private const double BaselineCalories = 2000;

    public async Task<DailyNutritionLogSnapshot> GetLogForDateAsync(DateOnly date,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<INutritionTrackingService>();
        var dto = await trackingService.GetLogForDateAsync(date, cancellationToken).ConfigureAwait(false);
        return MapDailyLogToSnapshot(dto);
    }

    public async Task<LoggedFoodItemDto?> GetFoodItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<INutritionTrackingService>();
        var dto = await trackingService.GetFoodItemAsync(id, cancellationToken).ConfigureAwait(false);
        return dto is null ? null : MapFoodItemDtoToSnapshot(dto);
    }

    public async Task SaveFoodItemAsync(DateOnly date, LoggedFoodItemDto item, bool requireExisting,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<INutritionTrackingService>();
        var legacyDto = MapSnapshotToFoodItemDto(item);
        if (requireExisting)
            await trackingService.UpdateFoodItemAsync(date, legacyDto, cancellationToken).ConfigureAwait(false);
        else
            await trackingService.AddOrUpdateFoodItemAsync(date, legacyDto, cancellationToken).ConfigureAwait(false);
    }

    public async Task BatchSaveFoodItemsAsync(DateOnly date, IReadOnlyList<LoggedFoodItemDto> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0) return;
        await using var scope = scopeFactory.CreateAsyncScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<INutritionTrackingService>();
        var legacyDtos = items.Select(MapSnapshotToFoodItemDto).ToList();
        await trackingService.AddFoodItemsBatchAsync(date, legacyDtos, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CopyEntriesAsync(DateOnly sourceDate, DateOnly targetDate, MealType? mealType,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<NutritionTrackingService>();
        return await trackingService.CopyEntriesAsync(sourceDate, targetDate, mealType, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteFoodItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<INutritionTrackingService>();
        await trackingService.DeleteFoodItemAsync(itemId, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReLogFoodAsync(DateOnly date, MealType mealType, QuickFoodItemSnapshot quickItem,
        LoggedFoodItemDto draft, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<INutritionTrackingService>();
        var legacyQuick = MapQuickSnapshotToDto(quickItem);
        var legacyDraft = MapSnapshotToFoodItemDto(draft);
        await trackingService.ReLogFoodAsync(date, mealType, legacyQuick, legacyDraft, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DailyNutritionSummarySnapshot>> GetDateRangeSummariesAsync(DateOnly start,
        DateOnly end, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<INutritionTrackingService>();
        var summaries = await trackingService.GetDateRangeSummariesAsync(start, end, cancellationToken)
            .ConfigureAwait(false);
        return summaries.Select(s => new DailyNutritionSummarySnapshot(
            s.Date, s.LogId, s.TotalCalories, s.TotalProtein, s.TotalCarbs, s.TotalFat,
            s.TargetCalories, s.WaterIntakeMl, s.ItemsCount
        )).ToList();
    }

    public async Task UpdateWaterIntakeAsync(DateOnly date, int waterIntakeMl,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<INutritionTrackingService>();
        await trackingService.UpdateWaterIntakeAsync(date, waterIntakeMl, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateNotesAsync(DateOnly date, string? notes, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<INutritionTrackingService>();
        await trackingService.UpdateNotesAsync(date, notes, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<QuickFoodItemSnapshot>> GetRecentIngredientsAsync(DateOnly date, MealType mealType,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<INutritionTrackingService>();
        var quickItems = await trackingService.GetRecentIngredientsAsync(date, mealType, cancellationToken)
            .ConfigureAwait(false);
        return quickItems.Select(MapQuickDtoToSnapshot).ToList();
    }

    public async Task<IReadOnlyList<QuickFoodItemSnapshot>> GetRecentAcceptedSuggestionsAsync(DateOnly date,
        MealType mealType, CyclePhase phase, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<INutritionTrackingService>();
        var quickItems = await trackingService
            .GetRecentAcceptedSuggestionsAsync(date, mealType, phase, cancellationToken).ConfigureAwait(false);
        return quickItems.Select(MapQuickDtoToSnapshot).ToList();
    }

    public async Task<CyclePhase> GetPhaseForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<INutritionTrackingService>();
        return await trackingService.GetPhaseForDateAsync(date).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FastLookupFoodItemDto>> SearchLocalFoodsAsync(string query, int maxResults = 12,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var lookupService = scope.ServiceProvider.GetService<IFoodLookupService>();
        if (lookupService is null) return [];
        var foods = await lookupService.SearchLocalAsync(query, maxResults).ConfigureAwait(false);
        return foods.Select(f => new FastLookupFoodItemDto(f.Name, f.Calories, f.Protein, f.Carbs, f.Fats, f.IsCustom))
            .ToList();
    }

    public async Task<IReadOnlyList<FastLookupFoodItemDto>> SearchOnlineFoodsAsync(string query, int maxResults = 12,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var lookupService = scope.ServiceProvider.GetService<IFoodLookupService>();
        if (lookupService is null) return [];
        var foods = await lookupService.SearchOnlineAsync(query, maxResults, cancellationToken).ConfigureAwait(false);
        return foods.Select(f => new FastLookupFoodItemDto(f.Name, f.Calories, f.Protein, f.Carbs, f.Fats, f.IsCustom))
            .ToList();
    }

    private static DailyNutritionLogSnapshot MapDailyLogToSnapshot(DailyNutritionLogDto dto)
    {
        return new DailyNutritionLogSnapshot(
            dto.Id,
            dto.Date,
            dto.WaterIntakeMl,
            dto.TargetCalories,
            dto.TargetProtein,
            dto.TargetCarbs,
            dto.TargetFat,
            dto.TotalCalories,
            dto.TotalProtein,
            dto.TotalCarbs,
            dto.TotalFat,
            dto.Notes,
            dto.CreatedAt,
            dto.UpdatedAt,
            dto.Entries.Select(MapFoodItemDtoToSnapshot).ToList()
        );
    }

    private static LoggedFoodItemDto MapFoodItemDtoToSnapshot(FoodItemEntryDto dto)
    {
        return new LoggedFoodItemDto(
            dto.Id,
            dto.DailyLogId,
            dto.MealType,
            dto.Name,
            dto.ServingSize,
            dto.ServingUnit,
            dto.ServingsCount,
            dto.Calories,
            dto.Protein,
            dto.Carbs,
            dto.Fat,
            dto.LoggedAtTimestamp,
            dto.Ingredients.Select(i => new LoggedIngredientDto(i.FoodItemId, i.Name, i.Grams)).ToList(),
            dto.AcceptedSuggestionName,
            dto.AcceptedSuggestionPhase
        );
    }

    private static FoodItemEntryDto MapSnapshotToFoodItemDto(LoggedFoodItemDto dto)
    {
        return new FoodItemEntryDto(
            dto.Id,
            dto.DailyLogId,
            dto.MealType,
            dto.Name,
            dto.ServingSize,
            dto.ServingUnit,
            dto.ServingsCount,
            dto.Calories,
            dto.Protein,
            dto.Carbs,
            dto.Fat,
            dto.LoggedAtTimestamp
        )
        {
            Ingredients = dto.Ingredients.Select(i => new HistoryIngredientDto(i.FoodItemId, i.Name, i.Grams)).ToList(),
            AcceptedSuggestionName = dto.AcceptedSuggestionName,
            AcceptedSuggestionPhase = dto.AcceptedSuggestionPhase
        };
    }

    private static QuickFoodItemSnapshot MapQuickDtoToSnapshot(QuickFoodItemDto dto)
    {
        return new QuickFoodItemSnapshot(
            dto.Name,
            dto.ServingSize,
            dto.ServingUnit,
            dto.ServingsCount,
            dto.Calories,
            dto.Protein,
            dto.Carbs,
            dto.Fat,
            dto.Detail,
            dto.SourceId,
            dto.IngredientFoodItemId,
            dto.Ingredients.Select(i => new LoggedIngredientDto(i.FoodItemId, i.Name, i.Grams)).ToList()
        );
    }

    private static QuickFoodItemDto MapQuickSnapshotToDto(QuickFoodItemSnapshot snapshot)
    {
        return new QuickFoodItemDto(
            snapshot.Name,
            snapshot.ServingSize,
            snapshot.ServingUnit,
            snapshot.ServingsCount,
            snapshot.Calories,
            snapshot.Protein,
            snapshot.Carbs,
            snapshot.Fat,
            snapshot.Detail
        )
        {
            SourceId = snapshot.SourceId,
            IngredientFoodItemId = snapshot.IngredientFoodItemId,
            Ingredients = snapshot.Ingredients?.Select(i => new HistoryIngredientDto(i.FoodItemId, i.Name, i.Grams))
                .ToList() ?? []
        };
    }
}
