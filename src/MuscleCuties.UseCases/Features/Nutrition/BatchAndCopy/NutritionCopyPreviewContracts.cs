using MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;

public enum DiaryCopyModeDto
{
    Merge,
    Overwrite
}

public sealed record DiaryCopyFoodDto(
    string Identity,
    string Name,
    MealType MealType,
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    string? GroupIdentity = null);

public sealed record DiaryCopySnapshotDto(
    Guid OwnerId,
    DateOnly Date,
    string Revision,
    IReadOnlyList<DiaryCopyFoodDto> Items);

public sealed record DiaryCopyTotalsDto(
    int Count,
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat);

public sealed record PreviewNutritionCopyRequest(
    DateOnly SourceDate,
    DateOnly TargetDate,
    MealType? MealType,
    DiaryCopyModeDto Mode);

public sealed record PreviewNutritionCopyResponse(
    Guid OwnerId,
    DateOnly SourceDate,
    DateOnly TargetDate,
    MealType? MealType,
    DiaryCopyModeDto Mode,
    string SourceRevision,
    string TargetRevision,
    IReadOnlyList<DiaryCopyFoodDto> SourceItems,
    IReadOnlyList<DiaryCopyFoodDto> ExistingTargetItems,
    IReadOnlyList<DiaryCopyFoodDto> ResultingTargetItems,
    DiaryCopyTotalsDto SourceTotals,
    DiaryCopyTotalsDto ExistingTargetTotals,
    DiaryCopyTotalsDto ResultingTargetTotals);

public sealed record CommitPreviewedNutritionCopyRequest(
    string IdempotencyKey,
    DateOnly SourceDate,
    DateOnly TargetDate,
    MealType? MealType,
    DiaryCopyModeDto Mode,
    string ExpectedSourceRevision,
    string ExpectedTargetRevision);

/// <summary>
/// PostgreSQL implementation must read both owner dates without writes for preview. Commit must lock both dates,
/// compare both revisions and apply the chosen merge or scoped overwrite in one transaction. Meal overwrite
/// preserves water, notes and every unrelated meal; whole-day overwrite replaces diary foods only. A stale revision
/// must throw before mutation, including when source is empty. Idempotency replays the original committed result
/// only for an identical request and rejects a reused key with different contents.
/// </summary>
public interface INutritionCopyPreviewStore
{
    Task<DiaryCopySnapshotDto> ReadSnapshotAsync(
        Guid userId, DateOnly date, CancellationToken cancellationToken);

    Task<CopyNutritionDiaryResponse> CommitPreviewedCopyAsync(
        Guid userId, CommitPreviewedNutritionCopyRequest request, CancellationToken cancellationToken);
}
