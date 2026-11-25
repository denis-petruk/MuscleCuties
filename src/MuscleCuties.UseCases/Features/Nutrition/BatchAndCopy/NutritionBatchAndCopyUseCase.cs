using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Diary;

namespace MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;

/// <summary>Saves a batch, copies a day and quick-adds, each under the day's lock.</summary>
public sealed class NutritionBatchAndCopyUseCase(
    INutritionBatchAndCopyStore store,
    INutritionBatchAndCopyChangeNotifier? changeNotifier = null)
{
    private const int MaximumIdempotencyKeyLength = 128;

    /// <summary>Saves a batch of diary changes as one request.</summary>
    public async Task<BatchSaveNutritionDiaryResponse> BatchSaveAsync(
        Guid userId,
        BatchSaveNutritionDiaryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateOwnerAndKey(userId, request.IdempotencyKey);
        var deletes = request.DeleteIdentities ?? [];
        var upserts = request.UpsertEntries ?? [];
        if (deletes.Count == 0 && upserts.Count < DailyFoodEntry.MinimumBatchItems)
            throw new ArgumentException("A batch command requires at least one delete or upsert operation.", nameof(request));

        if (upserts.Count > 0)
        {
            var domainEntries = upserts
                .Select(entry =>
                {
                    ArgumentNullException.ThrowIfNull(entry);
                    return DailyFoodEntry.Create(
                        NutritionMealTypeMapper.ToDomain(entry.MealType),
                        entry.Name,
                        (double)entry.ServingSize,
                        entry.ServingUnit,
                        (double)entry.ServingsCount,
                        (double)entry.Calories,
                        (double)entry.Protein,
                        (double)entry.Carbs,
                        (double)entry.Fat);
                })
                .ToList();
            DailyFoodEntry.ValidateBatch(domainEntries);
        }

        var response = await store.BatchSaveAsync(
            userId,
            request with { DeleteIdentities = deletes, UpsertEntries = upserts },
            cancellationToken).ConfigureAwait(false);
        if (!response.Replayed)
            changeNotifier?.NotifyNutritionChanged(userId, request.Date);
        return response;
    }

    /// <summary>Copies a day's or meal's entries to another day.</summary>
    public async Task<CopyNutritionDiaryResponse> CopyAsync(
        Guid userId,
        CopyNutritionDiaryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateOwnerAndKey(userId, request.IdempotencyKey);
        _ = DiaryCopyDates.Create(request.SourceDate, request.TargetDate);
        if (request.MealType is { } mealType)
            DailyFoodEntry.RequireMealType(NutritionMealTypeMapper.ToDomain(mealType));

        var response = await store.CopyAsync(userId, request, cancellationToken).ConfigureAwait(false);
        if (!response.Replayed && response.CopiedCount > 0)
            changeNotifier?.NotifyNutritionChanged(userId, request.TargetDate);
        return response;
    }

    /// <summary>Adds a quick-add entry to the diary.</summary>
    public async Task<QuickAddNutritionDiaryResponse> QuickAddAsync(
        Guid userId,
        QuickAddNutritionDiaryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateOwnerAndKey(userId, request.IdempotencyKey);
        _ = DailyFoodEntry.Create(
            NutritionMealTypeMapper.ToDomain(request.MealType),
            request.Name,
            (double)request.ServingSize,
            request.ServingUnit,
            (double)request.ServingsCount,
            (double)request.Calories,
            (double)request.Protein,
            (double)request.Carbs,
            (double)request.Fat);
        if (request.SourceVersion is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Captured source version must be positive when provided.");

        var response = await store.QuickAddAsync(userId, request, cancellationToken).ConfigureAwait(false);
        if (!response.Replayed)
            changeNotifier?.NotifyNutritionChanged(userId, request.Date);
        return response;
    }

    /// <summary>Refuses a write with no owner, no command key or entries that belong to another account.</summary>
    private static void ValidateOwnerAndKey(Guid userId, string idempotencyKey)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid owner user ID is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > MaximumIdempotencyKeyLength)
            throw new ArgumentException("A valid non-empty idempotency key is required.", nameof(idempotencyKey));
    }
}
