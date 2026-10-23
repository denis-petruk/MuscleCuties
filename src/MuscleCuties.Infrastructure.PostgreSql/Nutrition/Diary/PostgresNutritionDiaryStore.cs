using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Nutrition.Diary;
using MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

namespace MuscleCuties.Infrastructure.PostgreSql.Nutrition.Diary;

/// <summary>Owner-scoped unified diary adapter. Snapshot and grouped meal rows retain separate identities.</summary>
public sealed class PostgresNutritionDiaryStore(PostgresDbContext db, TimeProvider timeProvider, PostgresIdempotencyExecutor? idempotencyExecutor = null) : INutritionDiaryStore, INutritionDiaryCommandEventStore
{
    private const string SnapshotPrefix = "snapshot:";
    private const string MealPrefix = "meal:";
    private const int DefaultWaterMillilitres = 0;
    private readonly PostgresIdempotencyExecutor _idempotencyExecutor = idempotencyExecutor ?? new PostgresIdempotencyExecutor();

    public async Task<NutritionDiaryDay> GetDayAsync(Guid userId, DateOnly date, int pageSize, string? pageToken,
        CancellationToken cancellationToken)
    {
        var cursor = ParsePageToken(pageToken);
        return await WithTenant(userId, cancellationToken, async () =>
        {
            var log = await db.DailyNutritionLogs.AsNoTracking().Where(x => x.UserId == userId && x.LocalDate == date && !x.IsDeleted)
                .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            var snapshots = await db.FoodItemEntries.AsNoTracking()
                .Where(x => x.UserId == userId && x.LocalDate == date && !x.IsDeleted)
                .OrderBy(x => x.LoggedAtTimestamp).ThenBy(x => x.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            var meals = await db.LoggedMeals.AsNoTracking().Where(x => x.UserId == userId && x.Date == date)
                .OrderBy(x => x.LoggedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            var mealIds = meals.Select(x => x.Id).ToArray();
            var mealEntries = await (from entry in db.LoggedMealEntries.AsNoTracking()
                join food in db.CatalogFoodItems.AsNoTracking() on entry.CatalogFoodItemId equals food.Id
                where entry.UserId == userId && mealIds.Contains(entry.LoggedMealId)
                select new { entry.LoggedMealId, Amount = new FoodAmount(entry.Grams, food.Name, food.Calories, food.Protein, food.Carbs, food.Fats) })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var customMealEntries = await (from entry in db.LoggedMealEntries.AsNoTracking()
                join food in db.UserFoodItems.AsNoTracking() on new { entry.UserId, entry.UserFoodItemId } equals new { food.UserId, UserFoodItemId = (Guid?)food.Id }
                where entry.UserId == userId && mealIds.Contains(entry.LoggedMealId)
                select new { entry.LoggedMealId, Amount = new FoodAmount(entry.Grams, food.Name, food.Calories, food.Protein, food.Carbs, food.Fats) })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var allMealEntries = mealEntries.Concat(customMealEntries).ToArray();
            var entries = snapshots.Select(ToSnapshotEntry).Concat(meals.Select(meal => ToMealEntry(meal,
                allMealEntries.Where(e => e.LoggedMealId == meal.Id).Select(e => e.Amount).ToArray())))
                .OrderBy(x => x.LoggedAtUtc).ThenBy(x => x.Identity, StringComparer.Ordinal).ToArray();
            var remaining = cursor is null ? entries : entries.Where(entry =>
                entry.LoggedAtUtc > cursor.Value.LoggedAtUtc ||
                (entry.LoggedAtUtc == cursor.Value.LoggedAtUtc && string.CompareOrdinal(entry.Identity, cursor.Value.Identity) > 0)).ToArray();
            var page = remaining.Take(pageSize).ToArray();
            var next = remaining.Length > pageSize && page.Length > 0 ? CreatePageToken(page[^1]) : null;
            return new NutritionDiaryDay(log?.Id ?? Guid.Empty, date, log?.WaterIntakeMl ?? DefaultWaterMillilitres,
                log?.Notes, log?.TargetCalories ?? 0, log?.TargetProtein ?? 0, log?.TargetCarbs ?? 0,
                log?.TargetFat ?? 0, page, next, log?.CreatedAt, log?.UpdatedAt);
        }).ConfigureAwait(false);
    }

    public Task<NutritionDiaryEntry?> GetSnapshotEntryByIdAsync(Guid userId, Guid itemId, CancellationToken cancellationToken) =>
        WithTenant(userId, cancellationToken, async () =>
        {
            var row = await db.FoodItemEntries.AsNoTracking()
                .SingleOrDefaultAsync(item => item.UserId == userId && item.Id == itemId && !item.IsDeleted, cancellationToken)
                .ConfigureAwait(false);
            return row is null ? null : ToSnapshotEntry(row);
        });

    public async Task<NutritionDiaryHistory> GetHistoryAsync(Guid userId, DateOnly beforeDate, int pageSize,
        CancellationToken cancellationToken)
    {
        return await WithTenant(userId, cancellationToken, async () =>
        {
            var logs = await db.DailyNutritionLogs.AsNoTracking().Where(x => x.UserId == userId && x.LocalDate < beforeDate && !x.IsDeleted)
                .Select(x => new { x.LocalDate, x.WaterIntakeMl }).ToListAsync(cancellationToken).ConfigureAwait(false);
            var snapshots = await db.FoodItemEntries.AsNoTracking().Where(x => x.UserId == userId && x.LocalDate < beforeDate && !x.IsDeleted)
                .Select(x => new { x.LocalDate, x.Calories, x.Protein, x.Carbs, x.Fat }).ToListAsync(cancellationToken).ConfigureAwait(false);
            var meals = await db.LoggedMeals.AsNoTracking().Where(x => x.UserId == userId && x.Date < beforeDate)
                .Select(x => new { x.Id, x.Date }).ToListAsync(cancellationToken).ConfigureAwait(false);
            var ids = meals.Select(x => x.Id).ToArray();
            var mealEntries = await (from entry in db.LoggedMealEntries.AsNoTracking()
                join food in db.CatalogFoodItems.AsNoTracking() on entry.CatalogFoodItemId equals food.Id
                where entry.UserId == userId && ids.Contains(entry.LoggedMealId)
                select new { entry.LoggedMealId, Amount = new FoodAmount(entry.Grams, food.Name, food.Calories, food.Protein, food.Carbs, food.Fats) })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var customMealEntries = await (from entry in db.LoggedMealEntries.AsNoTracking()
                join food in db.UserFoodItems.AsNoTracking() on new { entry.UserId, entry.UserFoodItemId } equals new { food.UserId, UserFoodItemId = (Guid?)food.Id }
                where entry.UserId == userId && ids.Contains(entry.LoggedMealId)
                select new { entry.LoggedMealId, Amount = new FoodAmount(entry.Grams, food.Name, food.Calories, food.Protein, food.Carbs, food.Fats) })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var allMealEntries = mealEntries.Concat(customMealEntries).ToArray();
            var dates = logs.Select(x => x.LocalDate).Concat(snapshots.Select(x => x.LocalDate)).Concat(meals.Select(x => x.Date))
                .Distinct().OrderByDescending(x => x).Take(pageSize).ToArray();
            var days = dates.Select(date =>
            {
                var foods = snapshots.Where(x => x.LocalDate == date).ToArray();
                var grouped = meals.Where(x => x.Date == date).ToArray();
                var linked = grouped.SelectMany(meal => allMealEntries.Where(x => x.LoggedMealId == meal.Id).Select(x => x.Amount));
                var values = foods.Select(x => (x.Calories, x.Protein, x.Carbs, x.Fat))
                    .Concat(linked.Select(x => (Scale(x.Calories, x.Grams), Scale(x.Protein, x.Grams), Scale(x.Carbs, x.Grams), Scale(x.Fat, x.Grams)))).ToArray();
                return new NutritionDiaryHistoryDay(date, values.Sum(x => x.Item1), values.Sum(x => x.Item2),
                    values.Sum(x => x.Item3), values.Sum(x => x.Item4), logs.Where(x => x.LocalDate == date).Select(x => x.WaterIntakeMl).FirstOrDefault(), foods.Length + grouped.Length);
            }).ToArray();
            return new NutritionDiaryHistory(days, dates.Length == pageSize ? dates.Last() : null);
        }).ConfigureAwait(false);
    }

    public async Task<string> SaveManualEntryAsync(Guid userId, SaveNutritionDiaryEntry item, CancellationToken cancellationToken)
    {
        var id = item.Identity is null ? Guid.NewGuid() : ParseSnapshotIdentity(item.Identity);
        await WithTenant(userId, cancellationToken, async () =>
        {
            var log = await db.DailyNutritionLogs.SingleOrDefaultAsync(x => x.UserId == userId && x.LocalDate == item.Date && !x.IsDeleted, cancellationToken).ConfigureAwait(false);
            if (log is null)
            {
                log = new DailyNutritionLogRecord { Id = Guid.NewGuid(), UserId = userId, LocalDate = item.Date,
                    CreatedAt = timeProvider.GetUtcNow().UtcDateTime, UpdatedAt = timeProvider.GetUtcNow().UtcDateTime };
                db.DailyNutritionLogs.Add(log);
            }
            var row = await db.FoodItemEntries.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id && !x.IsDeleted, cancellationToken).ConfigureAwait(false);
            if (item.Identity is not null && ((row is null && item.RequireExisting) ||
                (row is not null && (row.LocalDate != item.Date || row.DailyLogId != log.Id))))
                throw new InvalidOperationException("The diary entry is stale or does not belong to this owner and date.");

            if (item.IsDeleted)
            {
                if (row is not null)
                {
                    row.IsDeleted = true;
                    row.DeletedAt = timeProvider.GetUtcNow().UtcDateTime;
                    row.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
                }
                log.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }

            row ??= new FoodItemEntryRecord { Id = id, UserId = userId, DailyLogId = log.Id,
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime };
            row.LocalDate = item.Date; row.MealType = item.MealType.ToString(); row.Name = item.Name;
            row.ServingSize = item.ServingSize; row.ServingUnit = item.ServingUnit; row.ServingsCount = item.ServingsCount;
            row.Calories = item.Calories; row.Protein = item.Protein; row.Carbs = item.Carbs; row.Fat = item.Fat;
            row.LoggedAtTimestamp = DateTime.SpecifyKind(item.LoggedAtUtc, DateTimeKind.Utc);
            row.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            if (db.Entry(row).State == EntityState.Detached) db.FoodItemEntries.Add(row);
            log.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
        return SnapshotPrefix + id.ToString("N");
    }

    public async Task UpdateMetadataAsync(Guid userId, UpdateNutritionDiaryMetadata metadata, CancellationToken cancellationToken)
    {
        await WithTenant(userId, cancellationToken, async () =>
        {
            var log = await db.DailyNutritionLogs.SingleOrDefaultAsync(x => x.UserId == userId && x.LocalDate == metadata.Date && !x.IsDeleted, cancellationToken).ConfigureAwait(false);
            if (log is null)
            {
                log = new DailyNutritionLogRecord { Id = Guid.NewGuid(), UserId = userId, LocalDate = metadata.Date,
                    CreatedAt = timeProvider.GetUtcNow().UtcDateTime };
                db.DailyNutritionLogs.Add(log);
            }
            log.WaterIntakeMl = metadata.WaterIntakeMl; log.Notes = metadata.Notes; log.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }

    public async Task<BatchSaveNutritionDiaryResponse> BatchSaveEntriesAsync(
        Guid userId, BatchSaveNutritionDiaryEntries command, CancellationToken cancellationToken)
    {
        var commandKey = $"{NutritionDiaryDefaults.BatchCommandPrefix}{command.RequestId:D}";
        var requestHash = PostgresIdempotencyExecutor.ComputeRequestHash(command);

        var execution = await _idempotencyExecutor.ExecuteAsync(db, userId, commandKey, requestHash, async (context, ct) =>
        {
            var log = await context.DailyNutritionLogs.SingleOrDefaultAsync(
                x => x.UserId == userId && x.LocalDate == command.Date && !x.IsDeleted, ct).ConfigureAwait(false);
            if (log is null)
            {
                log = new DailyNutritionLogRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    LocalDate = command.Date,
                    CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
                    UpdatedAt = timeProvider.GetUtcNow().UtcDateTime
                };
                context.DailyNutritionLogs.Add(log);
            }

            var identities = new List<string>(command.Entries.Count);
            foreach (var item in command.Entries)
            {
                var id = item.Identity is null ? Guid.NewGuid() : ParseSnapshotIdentity(item.Identity);
                var row = await context.FoodItemEntries.SingleOrDefaultAsync(
                    x => x.UserId == userId && x.Id == id && !x.IsDeleted, ct).ConfigureAwait(false);

                if (item.Identity is not null && (row is null || row.LocalDate != command.Date || row.DailyLogId != log.Id))
                    throw new InvalidOperationException("The diary entry is stale or does not belong to this owner and date.");

                if (item.IsDeleted)
                {
                    if (row is not null)
                    {
                        row.IsDeleted = true;
                        row.DeletedAt = timeProvider.GetUtcNow().UtcDateTime;
                        row.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
                    }
                    identities.Add(SnapshotPrefix + id.ToString("N"));
                    continue;
                }

                row ??= new FoodItemEntryRecord { Id = id, UserId = userId, DailyLogId = log.Id };
                row.LocalDate = command.Date;
                row.MealType = item.MealType.ToString();
                row.Name = item.Name;
                row.ServingSize = item.ServingSize;
                row.ServingUnit = item.ServingUnit;
                row.ServingsCount = item.ServingsCount;
                row.Calories = item.Calories;
                row.Protein = item.Protein;
                row.Carbs = item.Carbs;
                row.Fat = item.Fat;
                row.LoggedAtTimestamp = DateTime.SpecifyKind(item.LoggedAtUtc, DateTimeKind.Utc);
                row.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

                if (context.Entry(row).State == EntityState.Detached)
                    context.FoodItemEntries.Add(row);

                identities.Add(SnapshotPrefix + id.ToString("N"));
            }

            log.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            await PostgresNutritionDiaryEventWriter.AppendAsync(context, userId, command.Date, commandKey, ct).ConfigureAwait(false);
            return new BatchSaveNutritionDiaryResponse(identities);
        }, cancellationToken).ConfigureAwait(false);

        return execution.Response;
    }

    public async Task<CopyNutritionDiaryResponse> CopyEntriesAsync(
        Guid userId, CopyNutritionDiary command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.RequestId == Guid.Empty)
            throw new ArgumentException("A non-empty request ID is required.", nameof(command));
        DailyFoodEntry.RequireDifferentDates(command.SourceDate, command.TargetDate);
        if (command.MealType.HasValue)
            DailyFoodEntry.RequireMealType((MuscleCuties.Core.Models.Enums.Nutrition.MealType)(int)command.MealType.Value);

        var commandKey = $"{NutritionDiaryDefaults.CopyCommandPrefix}{command.RequestId:D}";
        var requestHash = PostgresIdempotencyExecutor.ComputeRequestHash(command);

        var execution = await _idempotencyExecutor.ExecuteAsync(db, userId, commandKey, requestHash, async (context, ct) =>
        {
            var snapshots = await context.FoodItemEntries.AsNoTracking()
                .Where(x => x.UserId == userId && x.LocalDate == command.SourceDate && !x.IsDeleted)
                .OrderBy(x => x.LoggedAtTimestamp).ThenBy(x => x.Id)
                .ToListAsync(ct).ConfigureAwait(false);

            var meals = await context.LoggedMeals.AsNoTracking()
                .Where(x => x.UserId == userId && x.Date == command.SourceDate)
                .OrderBy(x => x.LoggedAt).ThenBy(x => x.Id)
                .ToListAsync(ct).ConfigureAwait(false);

            var mealIds = meals.Select(x => x.Id).ToArray();
            var mealEntries = await (from entry in context.LoggedMealEntries.AsNoTracking()
                join food in context.CatalogFoodItems.AsNoTracking() on entry.CatalogFoodItemId equals food.Id
                where entry.UserId == userId && mealIds.Contains(entry.LoggedMealId)
                select new { entry.LoggedMealId, Amount = new FoodAmount(entry.Grams, food.Name, food.Calories, food.Protein, food.Carbs, food.Fats) })
                .ToListAsync(ct).ConfigureAwait(false);

            var customMealEntries = await (from entry in context.LoggedMealEntries.AsNoTracking()
                join food in context.UserFoodItems.AsNoTracking() on new { entry.UserId, entry.UserFoodItemId } equals new { food.UserId, UserFoodItemId = (Guid?)food.Id }
                where entry.UserId == userId && mealIds.Contains(entry.LoggedMealId)
                select new { entry.LoggedMealId, Amount = new FoodAmount(entry.Grams, food.Name, food.Calories, food.Protein, food.Carbs, food.Fats) })
                .ToListAsync(ct).ConfigureAwait(false);

            var allMealEntries = mealEntries.Concat(customMealEntries).ToArray();
            var sourceItems = snapshots.Select(ToSnapshotEntry).Concat(meals.Select(meal => ToMealEntry(meal,
                allMealEntries.Where(e => e.LoggedMealId == meal.Id).Select(e => e.Amount).ToArray())))
                .Where(x => !command.MealType.HasValue || x.MealType == command.MealType.Value)
                .OrderBy(x => x.LoggedAtUtc).ThenBy(x => x.Identity, StringComparer.Ordinal)
                .ToArray();

            if (sourceItems.Length == 0)
            {
                return new CopyNutritionDiaryResponse(0, []);
            }

            var targetLog = await context.DailyNutritionLogs.SingleOrDefaultAsync(
                x => x.UserId == userId && x.LocalDate == command.TargetDate && !x.IsDeleted, ct).ConfigureAwait(false);
            if (targetLog is null)
            {
                targetLog = new DailyNutritionLogRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    LocalDate = command.TargetDate,
                    CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
                    UpdatedAt = timeProvider.GetUtcNow().UtcDateTime
                };
                context.DailyNutritionLogs.Add(targetLog);
            }

            var domainEntries = sourceItems.Select(x => DailyFoodEntry.Create(
                (MuscleCuties.Core.Models.Enums.Nutrition.MealType)(int)x.MealType,
                string.IsNullOrWhiteSpace(x.Name) ? NutritionDiaryDefaults.DefaultQuickAddTitle : x.Name,
                (double)(x.ServingSize <= 0m ? NutritionDiaryDefaults.DefaultQuickAddServingSize : x.ServingSize),
                string.IsNullOrWhiteSpace(x.ServingUnit) ? NutritionDiaryDefaults.DefaultQuickAddServingUnit : x.ServingUnit,
                (double)(x.ServingsCount <= 0m ? NutritionDiaryDefaults.DefaultQuickAddServingsCount : x.ServingsCount),
                (double)Math.Max(0m, x.Calories),
                (double)Math.Max(0m, x.Protein),
                (double)Math.Max(0m, x.Carbs),
                (double)Math.Max(0m, x.Fat))).ToArray();
            DailyFoodEntry.ValidateBatch(domainEntries);

            var newIdentities = new List<string>(sourceItems.Length);
            foreach (var source in sourceItems)
            {
                var newId = Guid.NewGuid();
                var copiedTime = DailyFoodEntry.CopyTimestamp(command.TargetDate, source.LoggedAtUtc);
                var copyRow = new FoodItemEntryRecord
                {
                    Id = newId,
                    DailyLogId = targetLog.Id,
                    UserId = userId,
                    LocalDate = command.TargetDate,
                    MealType = source.MealType.ToString(),
                    Name = string.IsNullOrWhiteSpace(source.Name) ? NutritionDiaryDefaults.DefaultQuickAddTitle : source.Name,
                    ServingSize = source.ServingSize <= 0m ? NutritionDiaryDefaults.DefaultQuickAddServingSize : source.ServingSize,
                    ServingUnit = string.IsNullOrWhiteSpace(source.ServingUnit) ? NutritionDiaryDefaults.DefaultQuickAddServingUnit : source.ServingUnit,
                    ServingsCount = source.ServingsCount <= 0m ? NutritionDiaryDefaults.DefaultQuickAddServingsCount : source.ServingsCount,
                    Calories = Math.Max(0m, source.Calories),
                    Protein = Math.Max(0m, source.Protein),
                    Carbs = Math.Max(0m, source.Carbs),
                    Fat = Math.Max(0m, source.Fat),
                    LoggedAtTimestamp = DateTime.SpecifyKind(copiedTime, DateTimeKind.Utc),
                    CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
                    UpdatedAt = timeProvider.GetUtcNow().UtcDateTime
                };
                context.FoodItemEntries.Add(copyRow);
                newIdentities.Add(SnapshotPrefix + newId.ToString("N"));
            }

            targetLog.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            await PostgresNutritionDiaryEventWriter.AppendAsync(context, userId, command.TargetDate, commandKey, ct).ConfigureAwait(false);
            return new CopyNutritionDiaryResponse(newIdentities.Count, newIdentities);
        }, cancellationToken).ConfigureAwait(false);

        return execution.Response;
    }

    public async Task<QuickAddNutritionDiaryResponse> QuickAddEntryAsync(
        Guid userId, QuickAddNutritionDiaryEntry command, CancellationToken cancellationToken)
    {
        var commandKey = $"{NutritionDiaryDefaults.QuickAddCommandPrefix}{command.RequestId:D}";
        var requestHash = PostgresIdempotencyExecutor.ComputeRequestHash(command);

        var execution = await _idempotencyExecutor.ExecuteAsync(db, userId, commandKey, requestHash, async (context, ct) =>
        {
            var log = await context.DailyNutritionLogs.SingleOrDefaultAsync(
                x => x.UserId == userId && x.LocalDate == command.Date && !x.IsDeleted, ct).ConfigureAwait(false);
            if (log is null)
            {
                log = new DailyNutritionLogRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    LocalDate = command.Date,
                    CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
                    UpdatedAt = timeProvider.GetUtcNow().UtcDateTime
                };
                context.DailyNutritionLogs.Add(log);
            }

            var id = Guid.NewGuid();
            var servingSize = command.ServingSize ?? NutritionDiaryDefaults.DefaultQuickAddServingSize;
            var servingUnit = string.IsNullOrWhiteSpace(command.ServingUnit) ? NutritionDiaryDefaults.DefaultQuickAddServingUnit : command.ServingUnit;
            var servingsCount = command.ServingsCount ?? NutritionDiaryDefaults.DefaultQuickAddServingsCount;
            var protein = command.Protein ?? NutritionDiaryDefaults.DefaultQuickAddMacroGrams;
            var carbs = command.Carbs ?? NutritionDiaryDefaults.DefaultQuickAddMacroGrams;
            var fat = command.Fat ?? NutritionDiaryDefaults.DefaultQuickAddMacroGrams;
            var name = string.IsNullOrWhiteSpace(command.Name) ? NutritionDiaryDefaults.DefaultQuickAddTitle : command.Name.Trim();
            var nowTime = TimeOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            var timestamp = DateTime.SpecifyKind(command.Date.ToDateTime(nowTime), DateTimeKind.Utc);

            var row = new FoodItemEntryRecord
            {
                Id = id,
                UserId = userId,
                DailyLogId = log.Id,
                LocalDate = command.Date,
                MealType = command.MealType.ToString(),
                Name = name,
                ServingSize = servingSize,
                ServingUnit = servingUnit,
                ServingsCount = servingsCount,
                Calories = command.Calories,
                Protein = protein,
                Carbs = carbs,
                Fat = fat,
                LoggedAtTimestamp = timestamp,
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
                UpdatedAt = timeProvider.GetUtcNow().UtcDateTime
            };

            context.FoodItemEntries.Add(row);
            log.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            await PostgresNutritionDiaryEventWriter.AppendAsync(context, userId, command.Date, commandKey, ct).ConfigureAwait(false);
            return new QuickAddNutritionDiaryResponse(SnapshotPrefix + id.ToString("N"));
        }, cancellationToken).ConfigureAwait(false);

        return execution.Response;
    }

    public async Task<NutritionDiaryCommandEventPage> GetEventsAfterAsync(
        Guid userId, long afterCursor, int pageSize, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A non-empty authenticated user ID is required.", nameof(userId));
        if (afterCursor < 0) throw new ArgumentOutOfRangeException(nameof(afterCursor));
        if (pageSize is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(pageSize));

        return await WithTenant(userId, cancellationToken, async () =>
        {
            var events = await db.NutritionDiaryCommandEvents.AsNoTracking()
                .Where(x => x.UserId == userId && x.Cursor > afterCursor)
                .OrderBy(x => x.Cursor).Take(pageSize)
                .Select(x => new NutritionDiaryCommandEvent(x.Cursor, x.DiaryDate, x.CommandId))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            return new NutritionDiaryCommandEventPage(events, events.Count == 0 ? afterCursor : events[^1].Cursor);
        }).ConfigureAwait(false);
    }

    private async Task<T> WithTenant<T>(Guid userId, CancellationToken ct, Func<Task<T>> action) =>
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(db, userId, ct).ConfigureAwait(false);
            var result = await action().ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return result;
        }).ConfigureAwait(false);

    private static NutritionDiaryEntry ToSnapshotEntry(FoodItemEntryRecord row) => new(
        SnapshotPrefix + row.Id.ToString("N"), row.LocalDate, Enum.Parse<MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.MealType>(row.MealType), row.Name,
        row.ServingSize, row.ServingUnit, row.ServingsCount, row.Calories, row.Protein, row.Carbs, row.Fat,
        DateTime.SpecifyKind(row.LoggedAtTimestamp, DateTimeKind.Utc), true);

    private static NutritionDiaryEntry ToMealEntry(LoggedMealRecord meal, IReadOnlyList<FoodAmount> foods)
    {
        // Legacy grouped meals expose their source meal identity and preserve catalog-derived values.
        decimal total = foods.Sum(x => x.Grams);
        var calories = foods.Sum(x => Scale(x.Calories, x.Grams));
        var protein = foods.Sum(x => Scale(x.Protein, x.Grams));
        var carbs = foods.Sum(x => Scale(x.Carbs, x.Grams));
        var fat = foods.Sum(x => Scale(x.Fat, x.Grams));
        return new NutritionDiaryEntry(MealPrefix + meal.Id.ToString("N"), meal.Date,
            Enum.Parse<MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.MealType>(meal.MealType), meal.AcceptedSuggestionName ?? string.Join(", ", foods.Select(x => x.Name)),
            total, "g", 1, calories, protein, carbs, fat, DateTime.SpecifyKind(meal.LoggedAt, DateTimeKind.Utc), false,
            meal.AcceptedSuggestionName, meal.AcceptedSuggestionPhase);
    }

    private static decimal Scale(decimal? per100, decimal grams) => per100.GetValueOrDefault() * grams / 100m;
    private sealed record FoodAmount(decimal Grams, string Name, decimal? Calories, decimal? Protein, decimal? Carbs, decimal? Fat);
    private static (DateTime LoggedAtUtc, string Identity)? ParsePageToken(string? token)
    {
        if (token is null) return null;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var separator = decoded.IndexOf('\n');
            if (separator < 0 || !DateTime.TryParseExact(decoded[..separator], "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var timestamp) || string.IsNullOrEmpty(decoded[(separator + 1)..]))
                throw new FormatException();
            return (timestamp, decoded[(separator + 1)..]);
        }
        catch (FormatException)
        {
            throw new ArgumentException("Invalid diary page token.", nameof(token));
        }
    }

    private static string CreatePageToken(NutritionDiaryEntry entry) => Convert.ToBase64String(
        Encoding.UTF8.GetBytes($"{entry.LoggedAtUtc:O}\n{entry.Identity}"));
    private static Guid ParseSnapshotIdentity(string identity) =>
        identity.StartsWith(SnapshotPrefix, StringComparison.Ordinal) && Guid.TryParseExact(identity[SnapshotPrefix.Length..], "N", out var id)
            ? id : throw new ArgumentException("Only snapshot identities can be manually corrected.", nameof(identity));
}
