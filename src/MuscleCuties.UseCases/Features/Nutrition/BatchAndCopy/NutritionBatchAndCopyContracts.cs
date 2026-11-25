using MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;

/// <summary>One entry of a batch save.</summary>
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

/// <summary>Several entries saved in one call, idempotent on their key.</summary>
public sealed record BatchSaveNutritionDiaryRequest(
    string IdempotencyKey,
    DateOnly Date,
    IReadOnlyList<string> DeleteIdentities,
    IReadOnlyList<BatchNutritionDiaryEntryInput> UpsertEntries);

/// <summary>What the batch wrote, so the client can show the day at once.</summary>
public sealed record BatchSaveNutritionDiaryResponse(
    Guid LogId,
    DateOnly Date,
    IReadOnlyList<string> SavedIdentities,
    int DeletedCount,
    bool Replayed);

/// <summary>A copy of one day onto another.</summary>
public sealed record CopyNutritionDiaryRequest(
    string IdempotencyKey,
    DateOnly SourceDate,
    DateOnly TargetDate,
    MealType? MealType = null);

/// <summary>What the copy wrote, with the new day's totals.</summary>
public sealed record CopyNutritionDiaryResponse(
    Guid TargetLogId,
    DateOnly TargetDate,
    int CopiedCount,
    IReadOnlyList<string> CopiedIdentities,
    bool Replayed);

/// <summary>A quick add of a food with no measured portion.</summary>
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

/// <summary>The stored entry and the day's new totals.</summary>
public sealed record QuickAddNutritionDiaryResponse(
    Guid LogId,
    DateOnly Date,
    string Identity,
    string? SourceFoodKey,
    int? SourceVersion,
    bool Replayed);

/// <summary>The persistence batch saves and copies need.</summary>
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
