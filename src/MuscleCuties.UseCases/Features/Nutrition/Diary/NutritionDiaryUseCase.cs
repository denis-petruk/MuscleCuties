using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Diary;

/// <summary>Reads and writes the diary, appending an event for every change.</summary>
public sealed class NutritionDiaryUseCase(INutritionDiaryStore store)
{
    private const int MinimumPageSize = 1;
    private const int MaximumPageSize = 100;

    /// <summary>Loads one day of the diary.</summary>
    public Task<NutritionDiaryDay> GetDayAsync(Guid userId, DateOnly date, int pageSize = 50, string? pageToken = null,
        CancellationToken cancellationToken = default) =>
        store.GetDayAsync(userId, date, Math.Clamp(pageSize, MinimumPageSize, MaximumPageSize), pageToken, cancellationToken);

    /// <summary>Loads one diary entry by id.</summary>
    public Task<NutritionDiaryEntry?> GetSnapshotEntryByIdAsync(Guid userId, Guid itemId,
        CancellationToken cancellationToken = default) =>
        store.GetSnapshotEntryByIdAsync(userId, itemId, cancellationToken);

    /// <summary>Loads the diary days before a date.</summary>
    public Task<NutritionDiaryHistory> GetHistoryAsync(Guid userId, DateOnly beforeDate, int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        store.GetHistoryAsync(userId, beforeDate, Math.Clamp(pageSize, MinimumPageSize, MaximumPageSize), cancellationToken);

    /// <summary>Stores a hand-entered entry and returns the day's totals with it.</summary>
    public Task<string> SaveManualEntryAsync(Guid userId, SaveNutritionDiaryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!entry.IsDeleted)
        {
            _ = DailyFoodEntry.Create(NutritionMealTypeMapper.ToDomain(entry.MealType), entry.Name, (double)entry.ServingSize, entry.ServingUnit,
                (double)entry.ServingsCount, (double)entry.Calories, (double)entry.Protein, (double)entry.Carbs,
                (double)entry.Fat);
        }
        else if (string.IsNullOrWhiteSpace(entry.Identity))
        {
            throw new ArgumentException("An identity is required to delete an entry.", nameof(entry));
        }

        return store.SaveManualEntryAsync(userId, entry, cancellationToken);
    }

    /// <summary>Applies a note or water change, appending the event other devices replay.</summary>
    public Task UpdateMetadataAsync(Guid userId, UpdateNutritionDiaryMetadata metadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return store.UpdateMetadataAsync(userId, metadata with { WaterIntakeMl = Math.Max(0, metadata.WaterIntakeMl) }, cancellationToken);
    }

    /// <summary>Saves a batch of diary changes.</summary>
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
                    NutritionMealTypeMapper.ToDomain(entry.MealType), entry.Name, (double)entry.ServingSize, entry.ServingUnit,
                    (double)entry.ServingsCount, (double)entry.Calories, (double)entry.Protein,
                    (double)entry.Carbs, (double)entry.Fat);
                activeEntries.Add(domainFood);
            }
        }
        DailyFoodEntry.ValidateBatch(activeEntries);

        return store.BatchSaveEntriesAsync(userId, command, cancellationToken);
    }

    /// <summary>Copies entries between days or meals.</summary>
    public Task<CopyNutritionDiaryResponse> CopyEntriesAsync(
        Guid userId, CopyNutritionDiary command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.RequestId == Guid.Empty)
            throw new ArgumentException("A non-empty request ID is required.", nameof(command));
        DailyFoodEntry.RequireDifferentDates(command.SourceDate, command.TargetDate);
        if (command.MealType.HasValue)
            DailyFoodEntry.RequireMealType(NutritionMealTypeMapper.ToDomain(command.MealType.Value));

        return store.CopyEntriesAsync(userId, command, cancellationToken);
    }

    /// <summary>Adds a quick-add entry.</summary>
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
            NutritionMealTypeMapper.ToDomain(command.MealType), name, (double)servingSize, servingUnit,
            (double)servingsCount, (double)command.Calories, (double)protein,
            (double)carbs, (double)fat);

        return store.QuickAddEntryAsync(userId, command, cancellationToken);
    }

}
