using System.Text;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Infrastructure.PostgreSql.Common;
using MuscleCuties.Infrastructure.PostgreSql.Models.Operations;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Diary;
using MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;

namespace MuscleCuties.Infrastructure.PostgreSql.Nutrition.BatchAndCopy;

/// <summary>Executes atomic PostgreSQL batch, copy and preview-copy diary commands with durable idempotency.</summary>
public sealed class PostgresNutritionBatchAndCopyStore(
    PostgresDbContext dbContext,
    PostgresIdempotencyExecutor idempotencyExecutor,
    TimeProvider timeProvider) : INutritionBatchAndCopyStore, INutritionCopyPreviewStore
{
    private const string SnapshotPrefix = "snapshot:";
    private const string MealPrefix = "meal:";
    private const string BatchCommandPrefix = "nutrition.diary.batch:";
    private const string CopyCommandPrefix = "nutrition.diary.copy:";
    private const string CopyPreviewCommitCommandPrefix = "nutrition.diary.copy.commit:";

    /// <summary>Reads the day's entries for a copy preview.</summary>
    public async Task<DiaryCopySnapshotDto> ReadSnapshotAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, ct).ConfigureAwait(false);
            var snapshot = await BuildDateSnapshotCoreAsync(dbContext, userId, date, ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return snapshot;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Applies the previewed copy.</summary>
    public async Task<CopyNutritionDiaryResponse> CommitPreviewedCopyAsync(
        Guid userId,
        CommitPreviewedNutritionCopyRequest request,
        CancellationToken cancellationToken)
    {
        var commandKey = IdempotencyCommandKey.Build(CopyPreviewCommitCommandPrefix, request.IdempotencyKey);
        var requestHash = PostgresIdempotencyExecutor.ComputeRequestHash(new
        {
            request.SourceDate,
            request.TargetDate,
            request.MealType,
            request.Mode,
            request.ExpectedSourceRevision,
            request.ExpectedTargetRevision
        });

        var execution = await idempotencyExecutor.ExecuteAsync(
            dbContext,
            userId,
            commandKey,
            requestHash,
            async (context, ct) =>
            {
                var firstDate = request.SourceDate < request.TargetDate ? request.SourceDate : request.TargetDate;
                var secondDate = request.SourceDate < request.TargetDate ? request.TargetDate : request.SourceDate;
                await NutritionDiaryDayLock.AcquireAsync(context, userId, firstDate, ct).ConfigureAwait(false);
                await NutritionDiaryDayLock.AcquireAsync(context, userId, secondDate, ct).ConfigureAwait(false);

                var currentSource = await BuildDateSnapshotCoreAsync(context, userId, request.SourceDate, ct).ConfigureAwait(false);
                var currentTarget = await BuildDateSnapshotCoreAsync(context, userId, request.TargetDate, ct).ConfigureAwait(false);
                if (!string.Equals(currentSource.Revision, request.ExpectedSourceRevision, StringComparison.Ordinal) ||
                    !string.Equals(currentTarget.Revision, request.ExpectedTargetRevision, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Diary copy preview is stale; refresh preview before committing.");
                }

                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                var mealFilter = request.MealType?.ToString();

                if (request.Mode == DiaryCopyModeDto.Overwrite)
                {
                    var targetSnapshotsQuery = context.FoodItemEntries
                        .Where(row => row.UserId == userId && row.LocalDate == request.TargetDate && !row.IsDeleted);
                    var targetMealsQuery = context.LoggedMeals
                        .Where(row => row.UserId == userId && row.Date == request.TargetDate);
                    if (mealFilter is not null)
                    {
                        targetSnapshotsQuery = targetSnapshotsQuery.Where(row => row.MealType == mealFilter);
                        targetMealsQuery = targetMealsQuery.Where(row => row.MealType == mealFilter);
                    }

                    var targetSnapshots = await targetSnapshotsQuery.ToListAsync(ct).ConfigureAwait(false);
                    foreach (var existing in targetSnapshots)
                    {
                        existing.IsDeleted = true;
                        existing.DeletedAt = nowUtc;
                        existing.UpdatedAt = nowUtc;
                    }

                    var targetMeals = await targetMealsQuery.ToListAsync(ct).ConfigureAwait(false);
                    if (targetMeals.Count > 0)
                    {
                        var mealIds = targetMeals.Select(m => m.Id).ToArray();
                        var mealEntries = await context.LoggedMealEntries
                            .Where(e => e.UserId == userId && mealIds.Contains(e.LoggedMealId))
                            .ToListAsync(ct)
                            .ConfigureAwait(false);
                        context.LoggedMealEntries.RemoveRange(mealEntries);
                        context.LoggedMeals.RemoveRange(targetMeals);
                    }
                }

                return await ExecuteCopyItemsCoreAsync(
                    context,
                    userId,
                    commandKey,
                    request.SourceDate,
                    request.TargetDate,
                    mealFilter,
                    nowUtc,
                    request.Mode == DiaryCopyModeDto.Overwrite,
                    ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return execution.Replayed
            ? execution.Response with { Replayed = true }
            : execution.Response;
    }

    /// <summary>Saves a batch of diary changes.</summary>
    public async Task<BatchSaveNutritionDiaryResponse> BatchSaveAsync(
        Guid userId,
        BatchSaveNutritionDiaryRequest request,
        CancellationToken cancellationToken)
    {
        var commandKey = IdempotencyCommandKey.Build(BatchCommandPrefix, request.IdempotencyKey);
        var requestHash = PostgresIdempotencyExecutor.ComputeRequestHash(new
        {
            request.Date,
            DeleteIdentities = request.DeleteIdentities,
            UpsertEntries = request.UpsertEntries
        });

        var execution = await idempotencyExecutor.ExecuteAsync(
            dbContext,
            userId,
            commandKey,
            requestHash,
            async (context, ct) =>
            {
                await NutritionDiaryDayLock.AcquireAsync(context, userId, request.Date, ct).ConfigureAwait(false);
                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                var log = await GetOrCreateDailyLogAsync(context, userId, request.Date, nowUtc, ct).ConfigureAwait(false);

                var deletedCount = 0;
                foreach (var deleteIdentity in request.DeleteIdentities)
                {
                    var entryId = ParseSnapshotIdentity(deleteIdentity);
                    var existing = await context.FoodItemEntries.SingleOrDefaultAsync(
                            row => row.UserId == userId && row.Id == entryId && !row.IsDeleted,
                            ct)
                        .ConfigureAwait(false);
                    if (existing is null || existing.LocalDate != request.Date || existing.DailyLogId != log.Id)
                        throw new InvalidOperationException("A staged diary delete targets a missing, foreign, or stale entry.");

                    existing.IsDeleted = true;
                    existing.DeletedAt = nowUtc;
                    existing.UpdatedAt = nowUtc;
                    deletedCount++;
                }

                var savedIdentities = new List<string>(request.UpsertEntries.Count);
                for (var index = 0; index < request.UpsertEntries.Count; index++)
                {
                    var item = request.UpsertEntries[index];
                    var entryId = item.Identity is null
                        ? CreateDeterministicUuid(userId, commandKey, index)
                        : ParseSnapshotIdentity(item.Identity);
                    var row = await context.FoodItemEntries.SingleOrDefaultAsync(
                            existing => existing.UserId == userId && existing.Id == entryId && !existing.IsDeleted,
                            ct)
                        .ConfigureAwait(false);
                    if (item.Identity is not null && (row is null || row.LocalDate != request.Date || row.DailyLogId != log.Id))
                        throw new InvalidOperationException("A staged diary upsert targets a missing, foreign, or stale entry.");

                    row ??= new FoodItemEntryRecord
                    {
                        Id = entryId,
                        UserId = userId,
                        DailyLogId = log.Id,
                        CreatedAt = nowUtc
                    };
                    row.LocalDate = request.Date;
                    row.MealType = item.MealType.ToString();
                    row.Name = item.Name.Trim();
                    row.ServingSize = item.ServingSize;
                    row.ServingUnit = item.ServingUnit.Trim();
                    row.ServingsCount = item.ServingsCount;
                    row.Calories = item.Calories;
                    row.Protein = item.Protein;
                    row.Carbs = item.Carbs;
                    row.Fat = item.Fat;
                    row.LoggedAtTimestamp = DateTime.SpecifyKind(item.LoggedAtUtc, DateTimeKind.Utc);
                    row.UpdatedAt = nowUtc;

                    if (context.Entry(row).State == EntityState.Detached)
                        context.FoodItemEntries.Add(row);

                    savedIdentities.Add(SnapshotPrefix + entryId.ToString("N"));
                }

                log.UpdatedAt = nowUtc;
                await context.SaveChangesAsync(ct).ConfigureAwait(false);
                await PostgresNutritionDiaryEventWriter.AppendAsync(context, userId, request.Date, commandKey, ct).ConfigureAwait(false);
                return new BatchSaveNutritionDiaryResponse(log.Id, request.Date, savedIdentities, deletedCount, false);
            },
            cancellationToken).ConfigureAwait(false);

        return execution.Replayed
            ? execution.Response with { Replayed = true }
            : execution.Response;
    }

    /// <summary>Copies entries between days or meals.</summary>
    public async Task<CopyNutritionDiaryResponse> CopyAsync(
        Guid userId,
        CopyNutritionDiaryRequest request,
        CancellationToken cancellationToken)
    {
        var commandKey = IdempotencyCommandKey.Build(CopyCommandPrefix, request.IdempotencyKey);
        var requestHash = PostgresIdempotencyExecutor.ComputeRequestHash(new
        {
            request.SourceDate,
            request.TargetDate,
            request.MealType
        });

        var execution = await idempotencyExecutor.ExecuteAsync(
            dbContext,
            userId,
            commandKey,
            requestHash,
            async (context, ct) =>
            {
                var firstDate = request.SourceDate < request.TargetDate ? request.SourceDate : request.TargetDate;
                var secondDate = request.SourceDate < request.TargetDate ? request.TargetDate : request.SourceDate;
                await NutritionDiaryDayLock.AcquireAsync(context, userId, firstDate, ct).ConfigureAwait(false);
                await NutritionDiaryDayLock.AcquireAsync(context, userId, secondDate, ct).ConfigureAwait(false);

                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                var mealFilter = request.MealType?.ToString();
                return await ExecuteCopyItemsCoreAsync(
                    context,
                    userId,
                    commandKey,
                    request.SourceDate,
                    request.TargetDate,
                    mealFilter,
                    nowUtc,
                    alwaysEmitEvent: false,
                    ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return execution.Replayed
            ? execution.Response with { Replayed = true }
            : execution.Response;
    }

    /// <summary>Copies the selected entries and reports the result.</summary>
    private static async Task<CopyNutritionDiaryResponse> ExecuteCopyItemsCoreAsync(
        PostgresDbContext context,
        Guid userId,
        string commandKey,
        DateOnly sourceDate,
        DateOnly targetDate,
        string? mealFilter,
        DateTime nowUtc,
        bool alwaysEmitEvent,
        CancellationToken ct)
    {
        var sourceSnapshotsQuery = context.FoodItemEntries.AsNoTracking()
            .Where(row => row.UserId == userId && row.LocalDate == sourceDate && !row.IsDeleted);
        var sourceMealsQuery = context.LoggedMeals.AsNoTracking()
            .Where(row => row.UserId == userId && row.Date == sourceDate);
        if (mealFilter is not null)
        {
            sourceSnapshotsQuery = sourceSnapshotsQuery.Where(row => row.MealType == mealFilter);
            sourceMealsQuery = sourceMealsQuery.Where(row => row.MealType == mealFilter);
        }

        var sourceSnapshots = await sourceSnapshotsQuery
            .OrderBy(row => row.LoggedAtTimestamp)
            .ThenBy(row => row.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var sourceMeals = await sourceMealsQuery
            .OrderBy(row => row.LoggedAt)
            .ThenBy(row => row.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var targetLog = await GetOrCreateDailyLogAsync(context, userId, targetDate, nowUtc, ct)
            .ConfigureAwait(false);
        var copiedIdentities = new List<string>(sourceSnapshots.Count + sourceMeals.Count);
        var itemIndex = 0;

        foreach (var snapshot in sourceSnapshots)
        {
            var newId = CreateDeterministicUuid(userId, commandKey, itemIndex++);
            var copiedTimestamp = DateTime.SpecifyKind(
                DailyFoodEntry.CopyTimestamp(targetDate, snapshot.LoggedAtTimestamp),
                DateTimeKind.Utc);
            context.FoodItemEntries.Add(new FoodItemEntryRecord
            {
                Id = newId,
                UserId = userId,
                DailyLogId = targetLog.Id,
                LocalDate = targetDate,
                MealType = snapshot.MealType,
                Name = snapshot.Name,
                ServingSize = snapshot.ServingSize,
                ServingUnit = snapshot.ServingUnit,
                ServingsCount = snapshot.ServingsCount,
                Calories = snapshot.Calories,
                Protein = snapshot.Protein,
                Carbs = snapshot.Carbs,
                Fat = snapshot.Fat,
                LoggedAtTimestamp = copiedTimestamp,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            });
            copiedIdentities.Add(SnapshotPrefix + newId.ToString("N"));
        }

        if (sourceMeals.Count > 0)
        {
            var sourceMealIds = sourceMeals.Select(meal => meal.Id).ToArray();
            var sourceMealEntries = await context.LoggedMealEntries.AsNoTracking()
                .Where(entry => entry.UserId == userId && sourceMealIds.Contains(entry.LoggedMealId))
                .OrderBy(entry => entry.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var meal in sourceMeals)
            {
                var newMealId = CreateDeterministicUuid(userId, commandKey, itemIndex++);
                var copiedLoggedAt = DateTime.SpecifyKind(
                    DailyFoodEntry.CopyTimestamp(targetDate, meal.LoggedAt),
                    DateTimeKind.Utc);
                context.LoggedMeals.Add(new LoggedMealRecord
                {
                    Id = newMealId,
                    UserId = userId,
                    Date = targetDate,
                    LoggedAt = copiedLoggedAt,
                    MealType = meal.MealType,
                    AcceptedSuggestionName = meal.AcceptedSuggestionName,
                    AcceptedSuggestionPhase = meal.AcceptedSuggestionPhase,
                    CreatedAt = nowUtc
                });

                foreach (var mealEntry in sourceMealEntries.Where(entry => entry.LoggedMealId == meal.Id))
                {
                    context.LoggedMealEntries.Add(new LoggedMealEntryRecord
                    {
                        Id = CreateDeterministicUuid(userId, commandKey, itemIndex++),
                        UserId = userId,
                        LoggedMealId = newMealId,
                        CatalogFoodItemId = mealEntry.CatalogFoodItemId,
                        UserFoodItemId = mealEntry.UserFoodItemId,
                        Grams = mealEntry.Grams
                    });
                }

                copiedIdentities.Add(MealPrefix + newMealId.ToString("N"));
            }
        }

        targetLog.UpdatedAt = nowUtc;
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        if (copiedIdentities.Count > 0 || alwaysEmitEvent)
            await PostgresNutritionDiaryEventWriter.AppendAsync(context, userId, targetDate, commandKey, ct).ConfigureAwait(false);
        return new CopyNutritionDiaryResponse(
            targetLog.Id,
            targetDate,
            copiedIdentities.Count,
            copiedIdentities,
            false);
    }

    /// <summary>Builds the entry snapshot for one date.</summary>
    private static async Task<DiaryCopySnapshotDto> BuildDateSnapshotCoreAsync(
        PostgresDbContext context,
        Guid userId,
        DateOnly date,
        CancellationToken ct)
    {
        var snapshots = await context.FoodItemEntries.AsNoTracking()
            .Where(row => row.UserId == userId && row.LocalDate == date && !row.IsDeleted)
            .OrderBy(row => row.LoggedAtTimestamp)
            .ThenBy(row => row.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var items = snapshots
            .Select(row => new DiaryCopyFoodDto(
                SnapshotPrefix + row.Id.ToString("N"),
                row.Name,
                Enum.TryParse<MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.MealType>(
                    row.MealType, true, out var parsed)
                    ? parsed
                    : MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.MealType.Snack,
                row.Calories,
                row.Protein,
                row.Carbs,
                row.Fat))
            .ToArray();

        var revisionPayload = string.Join("|", snapshots.Select(s => $"{s.Id:N}:{s.UpdatedAt.Ticks}:{s.Calories}:{s.MealType}"));
        var revisionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:N}:{date:yyyy-MM-dd}:{revisionPayload}"))).ToLowerInvariant();
        return new DiaryCopySnapshotDto(userId, date, revisionHash, items);
    }

    /// <summary>Returns the day's diary row, creating it when absent.</summary>
    private static async Task<DailyNutritionLogRecord> GetOrCreateDailyLogAsync(
        PostgresDbContext context,
        Guid userId,
        DateOnly date,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var log = await context.DailyNutritionLogs.SingleOrDefaultAsync(
                row => row.UserId == userId && row.LocalDate == date && !row.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);
        if (log is not null)
            return log;

        log = new DailyNutritionLogRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            LocalDate = date,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };
        context.DailyNutritionLogs.Add(log);
        return log;
    }

    /// <summary>A GUID derived from the source row and day, so copying a day twice produces the same ids instead of duplicates.</summary>
    private static Guid CreateDeterministicUuid(Guid userId, string commandKey, int index)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:N}:{commandKey}:{index}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }

    /// <summary>The stored identity of a snapshot entry.</summary>
    private static Guid ParseSnapshotIdentity(string identity) =>
        !string.IsNullOrWhiteSpace(identity) &&
        identity.StartsWith(SnapshotPrefix, StringComparison.Ordinal) &&
        Guid.TryParseExact(identity[SnapshotPrefix.Length..], "N", out var id)
            ? id
            : throw new ArgumentException("Only snapshot identities can be modified in diary batch operations.", nameof(identity));
}
