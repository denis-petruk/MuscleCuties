using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Diary;

public sealed class NutritionDiaryUseCase(INutritionDiaryStore store)
{
    private const int MinimumPageSize = 1;
    private const int MaximumPageSize = 100;

    public Task<NutritionDiaryDay> GetDayAsync(Guid userId, DateOnly date, int pageSize = 50, string? pageToken = null,
        CancellationToken cancellationToken = default) =>
        store.GetDayAsync(userId, date, Math.Clamp(pageSize, MinimumPageSize, MaximumPageSize), pageToken, cancellationToken);

    public Task<NutritionDiaryEntry?> GetSnapshotEntryByIdAsync(Guid userId, Guid itemId,
        CancellationToken cancellationToken = default) =>
        store.GetSnapshotEntryByIdAsync(userId, itemId, cancellationToken);

    public Task<NutritionDiaryHistory> GetHistoryAsync(Guid userId, DateOnly beforeDate, int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        store.GetHistoryAsync(userId, beforeDate, Math.Clamp(pageSize, MinimumPageSize, MaximumPageSize), cancellationToken);

    public Task<string> SaveManualEntryAsync(Guid userId, SaveNutritionDiaryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!entry.IsDeleted)
        {
            _ = DailyFoodEntry.Create(ToCoreMealType(entry.MealType), entry.Name, (double)entry.ServingSize, entry.ServingUnit,
                (double)entry.ServingsCount, (double)entry.Calories, (double)entry.Protein, (double)entry.Carbs,
                (double)entry.Fat);
        }
        else if (string.IsNullOrWhiteSpace(entry.Identity))
        {
            throw new ArgumentException("An identity is required to delete an entry.", nameof(entry));
        }

        return store.SaveManualEntryAsync(userId, entry, cancellationToken);
    }

    public Task UpdateMetadataAsync(Guid userId, UpdateNutritionDiaryMetadata metadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return store.UpdateMetadataAsync(userId, metadata with { WaterIntakeMl = Math.Max(0, metadata.WaterIntakeMl) }, cancellationToken);
    }

    public Task<BatchSaveNutritionDiaryResponse> BatchSaveEntriesAsync(
        Guid userId, BatchSaveNutritionDiaryEntries command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.RequestId == Guid.Empty)
            throw new ArgumentException("A non-empty request ID is required.", nameof(command));
        ArgumentNullException.ThrowIfNull(command.Entries);

        var activeEntries = new List<DailyFoodEntry>();
        foreach (var entry in command.Entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (entry.Date != command.Date)
                throw new ArgumentException("All entries in the batch must match the batch date.", nameof(command));
            if (entry.IsDeleted)
            {
                if (string.IsNullOrWhiteSpace(entry.Identity))
                    throw new ArgumentException("An identity is required to delete an entry in a batch.", nameof(command));
            }
            else
            {
                var domainFood = DailyFoodEntry.Create(
                    ToCoreMealType(entry.MealType), entry.Name, (double)entry.ServingSize, entry.ServingUnit,
                    (double)entry.ServingsCount, (double)entry.Calories, (double)entry.Protein,
                    (double)entry.Carbs, (double)entry.Fat);
                activeEntries.Add(domainFood);
            }
        }
        DailyFoodEntry.ValidateBatch(activeEntries);

        return store.BatchSaveEntriesAsync(userId, command, cancellationToken);
    }

    public Task<CopyNutritionDiaryResponse> CopyEntriesAsync(
        Guid userId, CopyNutritionDiary command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.RequestId == Guid.Empty)
            throw new ArgumentException("A non-empty request ID is required.", nameof(command));
        DailyFoodEntry.RequireDifferentDates(command.SourceDate, command.TargetDate);
        if (command.MealType.HasValue)
            DailyFoodEntry.RequireMealType(ToCoreMealType(command.MealType.Value));

        return store.CopyEntriesAsync(userId, command, cancellationToken);
    }

    public Task<QuickAddNutritionDiaryResponse> QuickAddEntryAsync(
        Guid userId, QuickAddNutritionDiaryEntry command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.RequestId == Guid.Empty)
            throw new ArgumentException("A non-empty request ID is required.", nameof(command));

        var servingSize = command.ServingSize ?? NutritionDiaryDefaults.DefaultQuickAddServingSize;
        var servingUnit = string.IsNullOrWhiteSpace(command.ServingUnit) ? NutritionDiaryDefaults.DefaultQuickAddServingUnit : command.ServingUnit;
        var servingsCount = command.ServingsCount ?? NutritionDiaryDefaults.DefaultQuickAddServingsCount;
        var protein = command.Protein ?? NutritionDiaryDefaults.DefaultQuickAddMacroGrams;
        var carbs = command.Carbs ?? NutritionDiaryDefaults.DefaultQuickAddMacroGrams;
        var fat = command.Fat ?? NutritionDiaryDefaults.DefaultQuickAddMacroGrams;
        var name = string.IsNullOrWhiteSpace(command.Name) ? NutritionDiaryDefaults.DefaultQuickAddTitle : command.Name.Trim();

        _ = DailyFoodEntry.Create(
            ToCoreMealType(command.MealType), name, (double)servingSize, servingUnit,
            (double)servingsCount, (double)command.Calories, (double)protein,
            (double)carbs, (double)fat);

        return store.QuickAddEntryAsync(userId, command, cancellationToken);
    }

    private static MuscleCuties.Core.Models.Enums.Nutrition.MealType ToCoreMealType(MealType mealType) =>
        (MuscleCuties.Core.Models.Enums.Nutrition.MealType)(int)mealType;
}
