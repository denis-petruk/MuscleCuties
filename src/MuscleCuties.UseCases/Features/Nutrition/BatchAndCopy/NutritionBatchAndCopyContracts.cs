using MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;

public sealed record BatchNutritionDiaryEntryInput(
    string? Identity,
    MealType MealType,
    string Name,
    decimal ServingSize,
    string ServingUnit,
    decimal ServingsCount,
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    DateTime LoggedAtUtc);

public sealed record BatchSaveNutritionDiaryRequest(
    string IdempotencyKey,
    DateOnly Date,
    IReadOnlyList<string> DeleteIdentities,
    IReadOnlyList<BatchNutritionDiaryEntryInput> UpsertEntries);

public sealed record BatchSaveNutritionDiaryResponse(
    Guid LogId,
    DateOnly Date,
    IReadOnlyList<string> SavedIdentities,
    int DeletedCount,
    bool Replayed);

public sealed record CopyNutritionDiaryRequest(
    string IdempotencyKey,
    DateOnly SourceDate,
    DateOnly TargetDate,
    MealType? MealType = null);

public sealed record CopyNutritionDiaryResponse(
    Guid TargetLogId,
    DateOnly TargetDate,
    int CopiedCount,
    IReadOnlyList<string> CopiedIdentities,
    bool Replayed);

public sealed record QuickAddNutritionDiaryRequest(
    string IdempotencyKey,
    DateOnly Date,
    MealType MealType,
    string Name,
    decimal ServingSize,
    string ServingUnit,
    decimal ServingsCount,
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    DateTime LoggedAtUtc,
    string? SourceFoodKey = null,
    int? SourceVersion = null,
    bool LegacyTimestampUnspecified = false);

public sealed record QuickAddNutritionDiaryResponse(
    Guid LogId,
    DateOnly Date,
    string Identity,
    string? SourceFoodKey,
    int? SourceVersion,
    bool Replayed);

public interface INutritionBatchAndCopyStore
{
    Task<BatchSaveNutritionDiaryResponse> BatchSaveAsync(
        Guid userId,
        BatchSaveNutritionDiaryRequest request,
        CancellationToken cancellationToken);

    Task<CopyNutritionDiaryResponse> CopyAsync(
        Guid userId,
        CopyNutritionDiaryRequest request,
        CancellationToken cancellationToken);

    Task<QuickAddNutritionDiaryResponse> QuickAddAsync(
        Guid userId,
        QuickAddNutritionDiaryRequest request,
        CancellationToken cancellationToken);
}
