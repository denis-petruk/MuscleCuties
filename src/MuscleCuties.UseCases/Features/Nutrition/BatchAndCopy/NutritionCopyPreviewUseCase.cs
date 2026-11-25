using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Diary;

namespace MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;

/// <summary>Previews a day copy and commits it, refusing a commit whose preview is stale.</summary>
public sealed class NutritionCopyPreviewUseCase(
    INutritionCopyPreviewStore store,
    INutritionBatchAndCopyChangeNotifier? changeNotifier = null)
{
    private const int MaximumIdempotencyKeyLength = 128;

    /// <summary>Previews what copying a day's entries would change.</summary>
    public async Task<PreviewNutritionCopyResponse> PreviewAsync(
        Guid userId,
        PreviewNutritionCopyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireOwner(userId);
        _ = DiaryCopyDates.Create(request.SourceDate, request.TargetDate);

        var source = await store.ReadSnapshotAsync(userId, request.SourceDate, cancellationToken)
            .ConfigureAwait(false);
        var target = await store.ReadSnapshotAsync(userId, request.TargetDate, cancellationToken)
            .ConfigureAwait(false);
        ValidateSnapshot(source, userId, request.SourceDate);
        ValidateSnapshot(target, userId, request.TargetDate);

        var projection = DiaryCopyProjection.Create(
            source.Items.Select(ToDomain).ToArray(),
            target.Items.Select(ToDomain).ToArray(),
            request.MealType is { } mealType ? NutritionMealTypeMapper.ToDomain(mealType) : null,
            ToDomainMode(request.Mode));

        return new PreviewNutritionCopyResponse(
            userId,
            request.SourceDate,
            request.TargetDate,
            request.MealType,
            request.Mode,
            source.Revision,
            target.Revision,
            projection.SourceItems.Select(ToDto).ToArray(),
            projection.ExistingTargetItems.Select(ToDto).ToArray(),
            projection.ResultingTargetItems.Select(ToDto).ToArray(),
            ToDto(projection.SourceTotals),
            ToDto(projection.ExistingTargetTotals),
            ToDto(projection.ResultingTargetTotals));
    }

    /// <summary>Applies the copy the preview described.</summary>
    public async Task<CopyNutritionDiaryResponse> CommitAsync(
        Guid userId,
        CommitPreviewedNutritionCopyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireOwner(userId);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) ||
            request.IdempotencyKey.Trim().Length > MaximumIdempotencyKeyLength)
            throw new ArgumentException("A valid idempotency key is required.", nameof(request));
        _ = DiaryCopyDates.Create(request.SourceDate, request.TargetDate);
        if (request.MealType is { } mealType)
            DailyFoodEntry.RequireMealType(NutritionMealTypeMapper.ToDomain(mealType));
        if (!Enum.IsDefined(request.Mode))
            throw new ArgumentOutOfRangeException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.ExpectedSourceRevision) ||
            string.IsNullOrWhiteSpace(request.ExpectedTargetRevision))
            throw new ArgumentException("Preview revisions are required for commit.", nameof(request));

        var response = await store.CommitPreviewedCopyAsync(userId, request, cancellationToken)
            .ConfigureAwait(false);
        if (!response.Replayed && response.CopiedCount > 0)
            changeNotifier?.NotifyNutritionChanged(userId, request.TargetDate);
        return response;
    }

    /// <summary>Maps a previewed food back into the entry the commit will write, so the commit writes what was shown.</summary>
    private static DiaryCopyFood ToDomain(DiaryCopyFoodDto item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new DiaryCopyFood(item.Identity, item.Name, NutritionMealTypeMapper.ToDomain(item.MealType),
            item.Calories, item.Protein, item.Carbs, item.Fat, item.GroupIdentity);
    }

    /// <summary>Maps a copy food onto its wire shape.</summary>
    private static DiaryCopyFoodDto ToDto(DiaryCopyFood item) =>
        new(item.Identity, item.Name, NutritionMealTypeMapper.ToContract(item.MealType),
            item.Calories, item.Protein, item.Carbs, item.Fat, item.GroupIdentity);

    /// <summary>Maps copy totals onto their wire shape.</summary>
    private static DiaryCopyTotalsDto ToDto(DiaryCopyTotals totals) =>
        new(totals.Count, totals.Calories, totals.Protein, totals.Carbs, totals.Fat);

    /// <summary>The domain copy mode for the wire value.</summary>
    private static MuscleCuties.Core.Models.Enums.Nutrition.DiaryCopyMode ToDomainMode(DiaryCopyModeDto mode) =>
        mode switch
        {
            DiaryCopyModeDto.Merge => MuscleCuties.Core.Models.Enums.Nutrition.DiaryCopyMode.Merge,
            DiaryCopyModeDto.Overwrite => MuscleCuties.Core.Models.Enums.Nutrition.DiaryCopyMode.Overwrite,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown diary copy mode.")
        };

    /// <summary>Refuses a preview with no foods or an unknown mode, which the commit could not apply.</summary>
    private static void ValidateSnapshot(DiaryCopySnapshotDto snapshot, Guid userId, DateOnly expectedDate)
    {
        if (snapshot is null || snapshot.OwnerId != userId || snapshot.Date != expectedDate ||
            string.IsNullOrWhiteSpace(snapshot.Revision) || snapshot.Items is null)
            throw new InvalidOperationException("The diary snapshot does not match the requested owner and date.");
    }

    /// <summary>Refuses an empty owner, since a copy is always scoped to one account.</summary>
    private static void RequireOwner(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid owner user ID is required.", nameof(userId));
    }
}
