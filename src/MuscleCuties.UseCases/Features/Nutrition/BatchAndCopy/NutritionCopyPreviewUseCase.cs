using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;

public sealed class NutritionCopyPreviewUseCase(
    INutritionCopyPreviewStore store,
    INutritionBatchAndCopyChangeNotifier? changeNotifier = null)
{
    private const int MaximumIdempotencyKeyLength = 128;

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
            request.MealType,
            request.Mode);

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
            DailyFoodEntry.RequireMealType(mealType);
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

    private static DiaryCopyFood ToDomain(DiaryCopyFoodDto item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new DiaryCopyFood(item.Identity, item.Name, item.MealType,
            item.Calories, item.Protein, item.Carbs, item.Fat, item.GroupIdentity);
    }

    private static DiaryCopyFoodDto ToDto(DiaryCopyFood item) =>
        new(item.Identity, item.Name, item.MealType,
            item.Calories, item.Protein, item.Carbs, item.Fat, item.GroupIdentity);

    private static DiaryCopyTotalsDto ToDto(DiaryCopyTotals totals) =>
        new(totals.Count, totals.Calories, totals.Protein, totals.Carbs, totals.Fat);

    private static void ValidateSnapshot(DiaryCopySnapshotDto snapshot, Guid userId, DateOnly expectedDate)
    {
        if (snapshot is null || snapshot.OwnerId != userId || snapshot.Date != expectedDate ||
            string.IsNullOrWhiteSpace(snapshot.Revision) || snapshot.Items is null)
            throw new InvalidOperationException("The diary snapshot does not match the requested owner and date.");
    }

    private static void RequireOwner(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid owner user ID is required.", nameof(userId));
    }
}
