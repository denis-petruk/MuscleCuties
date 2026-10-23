using MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Diary;

public interface INutritionDiaryStore
{
    Task<NutritionDiaryDay> GetDayAsync(Guid userId, DateOnly date, int pageSize, string? pageToken, CancellationToken cancellationToken);
    Task<NutritionDiaryEntry?> GetSnapshotEntryByIdAsync(Guid userId, Guid itemId, CancellationToken cancellationToken);
    Task<NutritionDiaryHistory> GetHistoryAsync(Guid userId, DateOnly beforeDate, int pageSize, CancellationToken cancellationToken);
    Task<string> SaveManualEntryAsync(Guid userId, SaveNutritionDiaryEntry entry, CancellationToken cancellationToken);
    Task UpdateMetadataAsync(Guid userId, UpdateNutritionDiaryMetadata metadata, CancellationToken cancellationToken);
    Task<BatchSaveNutritionDiaryResponse> BatchSaveEntriesAsync(Guid userId, BatchSaveNutritionDiaryEntries command, CancellationToken cancellationToken);
    Task<CopyNutritionDiaryResponse> CopyEntriesAsync(Guid userId, CopyNutritionDiary command, CancellationToken cancellationToken);
    Task<QuickAddNutritionDiaryResponse> QuickAddEntryAsync(Guid userId, QuickAddNutritionDiaryEntry command, CancellationToken cancellationToken);
}
