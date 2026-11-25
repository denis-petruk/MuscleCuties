using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Diary;
using MuscleCuties.UseCases.Features.Hydration;

namespace MuscleCuties.Infrastructure.PostgreSql.Hydration;

/// <summary>Water intake, presets and reminders, stored as JSON documents per account with operation receipts so a replayed command is not applied twice.</summary>
public sealed class PostgresHydrationStore(
    PostgresDbContext dbContext,
    TimeProvider timeProvider)
    : IWaterPresetInitializationStore, IWaterIntakeStore, IWaterReminderConfigurationStore
{
    private const string LegacyIntakeConflict = "hydrationLegacyTotalConflict";
    private const string PresetsStateKey = "state:hydration:presets";
    private const string RemindersStateKey = "state:hydration:reminders";
    private const string IntakeStatePrefix = "state:hydration:intake:";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Seeds the default presets once, answering false when the account already had state.</summary>
    public async Task<bool> TryInitializeAsync(WaterPresetCatalog defaults, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        var accountId = defaults.AccountId;
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, accountId, token).ConfigureAwait(false);

            var existing = await dbContext.CommandIdempotencyRecords
                .SingleOrDefaultAsync(r => r.UserId == accountId && r.CommandKey == PresetsStateKey, token)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return false;
            }

            var initialState = WaterPresetState.Restore(defaults, []);
            var payload = SerializePresetState(initialState);
            dbContext.CommandIdempotencyRecords.Add(new CommandIdempotencyRecord
            {
                Id = Guid.CreateVersion7(),
                UserId = accountId,
                CommandKey = PresetsStateKey,
                RequestHash = "1",
                ResponseJson = JsonSerializer.Serialize(payload, JsonOptions),
                CompletedAt = timeProvider.GetUtcNow().UtcDateTime
            });
            await dbContext.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The account's presets, or the defaults when it has none.</summary>
    public async Task<StoredWaterPresetCatalog> ReadAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, accountId, token).ConfigureAwait(false);
            var row = await dbContext.CommandIdempotencyRecords.AsNoTracking()
                .SingleOrDefaultAsync(r => r.UserId == accountId && r.CommandKey == PresetsStateKey, token)
                .ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            if (row?.ResponseJson is null)
                return new StoredWaterPresetCatalog(WaterPresetState.Restore(WaterPresetCatalog.Restore(accountId, []), []), null);

            var payload = JsonSerializer.Deserialize<PersistedPresetState>(row.ResponseJson, JsonOptions)
                ?? new PersistedPresetState([], []);
            return new StoredWaterPresetCatalog(DeserializePresetState(accountId, payload), row.RequestHash);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Replaces the preset catalog at the expected version.</summary>
    public async Task<StoredWaterPresetCatalog?> TryReplaceAsync(
        WaterPresetState state,
        string? expectedConcurrencyToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        var accountId = state.Catalog.AccountId;
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<StoredWaterPresetCatalog?>(async token =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, accountId, token).ConfigureAwait(false);

            var row = await dbContext.CommandIdempotencyRecords
                .SingleOrDefaultAsync(r => r.UserId == accountId && r.CommandKey == PresetsStateKey, token)
                .ConfigureAwait(false);
            var currentToken = row?.RequestHash;
            if (!string.Equals(currentToken, expectedConcurrencyToken, StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(token).ConfigureAwait(false);
                return null;
            }

            var nextVersion = NextVersionToken(currentToken);
            var json = JsonSerializer.Serialize(SerializePresetState(state), JsonOptions);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (row is null)
            {
                dbContext.CommandIdempotencyRecords.Add(new CommandIdempotencyRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = accountId,
                    CommandKey = PresetsStateKey,
                    RequestHash = nextVersion,
                    ResponseJson = json,
                    CompletedAt = now
                });
            }
            else
            {
                row.RequestHash = nextVersion;
                row.ResponseJson = json;
                row.CompletedAt = now;
            }

            await dbContext.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return new StoredWaterPresetCatalog(state, nextVersion);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>One day's intake, derived from the stored operations so the total cannot drift from them.</summary>
    public async Task<StoredWaterIntakeDay> ReadAsync(Guid accountId, DateOnly localDate, CancellationToken cancellationToken)
    {
        var key = IntakeKey(localDate);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, accountId, token).ConfigureAwait(false);
            await NutritionDiaryDayLock.AcquireAsync(dbContext, accountId, localDate, token).ConfigureAwait(false);
            var row = await dbContext.CommandIdempotencyRecords.AsNoTracking()
                .SingleOrDefaultAsync(r => r.UserId == accountId && r.CommandKey == key, token)
                .ConfigureAwait(false);
            var diaryRow = await ReadIntakeDiaryRowAsync(dbContext.DailyNutritionLogs.AsNoTracking(),
                accountId, localDate, token).ConfigureAwait(false);
            var json = row?.ResponseJson;
            var day = json is not null
                ? ReadPersistedIntake(accountId, localDate, json)
                : WaterIntakeDay.Restore(accountId, localDate, Math.Max(0, diaryRow?.WaterIntakeMl ?? 0), [], []);
            if (json is not null)
                RequireMatchingDiaryTotal(day, diaryRow);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return new StoredWaterIntakeDay(day, json is not null ? row?.RequestHash : null);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Replaces the day's intake at the expected version.</summary>
    public async Task<StoredWaterIntakeDay?> TryReplaceAsync(
        WaterIntakeDay day,
        string? expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(day);
        var accountId = day.AccountId;
        var key = IntakeKey(day.Date);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<StoredWaterIntakeDay?>(async token =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, accountId, token).ConfigureAwait(false);

            await NutritionDiaryDayLock.AcquireAsync(dbContext, accountId, day.Date, token).ConfigureAwait(false);
            var row = await dbContext.CommandIdempotencyRecords
                .SingleOrDefaultAsync(r => r.UserId == accountId && r.CommandKey == key, token)
                .ConfigureAwait(false);
            var currentToken = row?.RequestHash;
            var diaryRow = await ReadIntakeDiaryRowAsync(dbContext.DailyNutritionLogs,
                accountId, day.Date, token).ConfigureAwait(false);
            if (!string.Equals(currentToken, expectedVersion, StringComparison.Ordinal) ||
                (row is null && Math.Max(0, diaryRow?.WaterIntakeMl ?? 0) != day.LegacyMillilitres))
            {
                await transaction.RollbackAsync(token).ConfigureAwait(false);
                return null;
            }

            // Recheck while writing because a legacy update can commit after the initial read.
            if (row?.ResponseJson is { } previous)
                RequireMatchingDiaryTotal(ReadPersistedIntake(accountId, day.Date, previous), diaryRow);

            var nextVersion = NextVersionToken(currentToken);
            var payload = new PersistedIntakeDay(
                day.LegacyMillilitres,
                day.Entries.Select(e => new PersistedIntakeEntry(e.Id, e.Millilitres, e.Undone)).ToArray(),
                day.Operations.Select(o => new PersistedIntakeOperation(o.OperationId, o.EntryId, o.Kind, o.Millilitres)).ToArray());
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (row is null)
            {
                dbContext.CommandIdempotencyRecords.Add(new CommandIdempotencyRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = accountId,
                    CommandKey = key,
                    RequestHash = nextVersion,
                    ResponseJson = json,
                    CompletedAt = now
                });
            }
            else
            {
                row.RequestHash = nextVersion;
                row.ResponseJson = json;
                row.CompletedAt = now;
            }

            if (diaryRow is null)
            {
                diaryRow = new DailyNutritionLogRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = accountId,
                    LocalDate = day.Date,
                    CreatedAt = now
                };
                dbContext.DailyNutritionLogs.Add(diaryRow);
            }
            diaryRow.WaterIntakeMl = day.TotalMillilitres;
            diaryRow.UpdatedAt = now;

            await dbContext.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return new StoredWaterIntakeDay(day, nextVersion);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the diary row the intake state is stored in.</summary>
    private static async Task<DailyNutritionLogRecord?> ReadIntakeDiaryRowAsync(
        IQueryable<DailyNutritionLogRecord> rows, Guid accountId, DateOnly date, CancellationToken cancellationToken)
    {
        var matching = await rows.Where(row => row.UserId == accountId && row.LocalDate == date && !row.IsDeleted)
            .Take(2).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (matching.Length > 1)
            throw new InvalidOperationException(LegacyIntakeConflict);
        return matching.SingleOrDefault();
    }

    /// <summary>Refuses a write when the nutrition day's water total disagrees with the hydration record, so the two sources cannot silently diverge.</summary>
    private static void RequireMatchingDiaryTotal(WaterIntakeDay day, DailyNutritionLogRecord? diaryRow)
    {
        // Earlier backends could persist a ledger before any diary row existed.
        // When both exist, neither a read nor a write can safely resolve disagreement.
        if (diaryRow is not null && diaryRow.WaterIntakeMl != day.TotalMillilitres)
            throw new InvalidOperationException(LegacyIntakeConflict);
    }

    /// <summary>Rebuilds a day's intake by replaying its stored operations in order.</summary>
    private static WaterIntakeDay ReadPersistedIntake(Guid accountId, DateOnly localDate, string json)
    {
        var payload = JsonSerializer.Deserialize<PersistedIntakeDay>(json, JsonOptions)
            ?? new PersistedIntakeDay(0, [], []);
        return WaterIntakeDay.Restore(
            accountId,
            localDate,
            payload.LegacyMillilitres,
            payload.Entries.Select(e => new WaterIntakeEntry(e.Id, e.Millilitres, e.Undone)),
            payload.Operations.Select(o => new WaterIntakeChange(o.OperationId, o.EntryId, o.Kind, o.Millilitres)));
    }

    async Task<StoredWaterReminderConfiguration> IWaterReminderConfigurationStore.ReadAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, accountId, token).ConfigureAwait(false);
            var row = await dbContext.CommandIdempotencyRecords.AsNoTracking()
                .SingleOrDefaultAsync(r => r.UserId == accountId && r.CommandKey == RemindersStateKey, token)
                .ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            if (row?.ResponseJson is null)
                return new StoredWaterReminderConfiguration(
                    WaterReminderConfiguration.Create(accountId, false, null, null, null),
                    null);

            var payload = JsonSerializer.Deserialize<PersistedReminderConfig>(row.ResponseJson, JsonOptions)
                ?? new PersistedReminderConfig(false, [], null, null, []);
            return new StoredWaterReminderConfiguration(DeserializeReminderConfig(accountId, payload), row.RequestHash);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Replaces the reminder configuration at the expected version.</summary>
    public async Task<StoredWaterReminderConfiguration?> TryReplaceAsync(
        WaterReminderConfiguration configuration,
        string? expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var accountId = configuration.AccountId;
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<StoredWaterReminderConfiguration?>(async token =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, accountId, token).ConfigureAwait(false);

            var row = await dbContext.CommandIdempotencyRecords
                .SingleOrDefaultAsync(r => r.UserId == accountId && r.CommandKey == RemindersStateKey, token)
                .ConfigureAwait(false);
            var currentToken = row?.RequestHash;
            if (!string.Equals(currentToken, expectedVersion, StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(token).ConfigureAwait(false);
                return null;
            }

            var nextVersion = NextVersionToken(currentToken);
            var json = JsonSerializer.Serialize(SerializeReminderConfig(configuration), JsonOptions);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (row is null)
            {
                dbContext.CommandIdempotencyRecords.Add(new CommandIdempotencyRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = accountId,
                    CommandKey = RemindersStateKey,
                    RequestHash = nextVersion,
                    ResponseJson = json,
                    CompletedAt = now
                });
            }
            else
            {
                row.RequestHash = nextVersion;
                row.ResponseJson = json;
                row.CompletedAt = now;
            }

            await dbContext.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return new StoredWaterReminderConfiguration(configuration, nextVersion);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Storage key of a day's intake state.</summary>
    private static string IntakeKey(DateOnly date) => $"{IntakeStatePrefix}{date:yyyy-MM-dd}";

    /// <summary>The version token for the next write.</summary>
    private static string NextVersionToken(string? currentToken) =>
        int.TryParse(currentToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? (parsed + 1).ToString(CultureInfo.InvariantCulture)
            : "1";

    /// <summary>Maps the preset state onto its stored shape.</summary>
    private static PersistedPresetState SerializePresetState(WaterPresetState state) => new(
        state.Catalog.Presets.Select(p => new PersistedPresetItem(p.Id, p.Label, p.Portion.Millilitres)).ToArray(),
        state.Operations.Select(o => new PersistedPresetOperation(o.OperationId, o.Kind, o.PresetId, o.Label, o.Millilitres, o.OrderedIds.ToArray())).ToArray());

    /// <summary>Reads the stored preset document, falling back to the defaults for an absent or unreadable value.</summary>
    private static WaterPresetState DeserializePresetState(Guid accountId, PersistedPresetState payload)
    {
        var catalog = WaterPresetCatalog.Restore(
            accountId,
            payload.Presets.Select(p => WaterPreset.Create(p.Id, p.Label, p.Millilitres)));
        var operations = payload.Operations.Select(o => o.Kind switch
        {
            WaterPresetMutationKind.Create => WaterPresetMutation.Create(o.OperationId, o.Label ?? string.Empty, o.Millilitres),
            WaterPresetMutationKind.Update => WaterPresetMutation.Update(o.OperationId, o.PresetId, o.Label ?? string.Empty, o.Millilitres),
            WaterPresetMutationKind.Delete => WaterPresetMutation.Delete(o.OperationId, o.PresetId),
            WaterPresetMutationKind.Reorder => WaterPresetMutation.Reorder(o.OperationId, o.OrderedIds),
            _ => throw new InvalidOperationException("Unsupported preset mutation kind.")
        });
        return WaterPresetState.Restore(catalog, operations);
    }

    /// <summary>Maps the reminder configuration onto its stored shape.</summary>
    private static PersistedReminderConfig SerializeReminderConfig(WaterReminderConfiguration config) => new(
        config.Settings.Enabled,
        config.Settings.LocalTimes.ToArray(),
        config.Settings.QuietStart,
        config.Settings.QuietEnd,
        config.Operations.Select(o => new PersistedReminderOperation(
            o.OperationId,
            o.Settings.Enabled,
            o.Settings.LocalTimes.ToArray(),
            o.Settings.QuietStart,
            o.Settings.QuietEnd)).ToArray());

    /// <summary>Reads the stored reminder settings, falling back to reminders off when the document is unreadable.</summary>
    private static WaterReminderConfiguration DeserializeReminderConfig(Guid accountId, PersistedReminderConfig payload)
    {
        var settings = payload.Enabled
            ? FixedWaterReminderSettings.Enable(payload.LocalTimes, payload.QuietStart, payload.QuietEnd)
            : FixedWaterReminderSettings.Disabled();
        var operations = payload.Operations.Select(o => new WaterReminderConfigurationChange(
            o.OperationId,
            o.Enabled
                ? FixedWaterReminderSettings.Enable(o.LocalTimes, o.QuietStart, o.QuietEnd)
                : FixedWaterReminderSettings.Disabled()));
        return WaterReminderConfiguration.Restore(accountId, settings, operations);
    }

    /// <summary>One stored preset, kept small enough for the whole catalog to live in one document.</summary>
    private sealed record PersistedPresetItem(Guid Id, string Label, int Millilitres);
    /// <summary>One preset change, replayed in order so the catalog cannot drift from its history.</summary>
    private sealed record PersistedPresetOperation(Guid OperationId, WaterPresetMutationKind Kind, Guid PresetId, string? Label, int Millilitres, Guid[] OrderedIds);
    /// <summary>The stored preset document: the operations and the catalogue they produce.</summary>
    private sealed record PersistedPresetState(PersistedPresetItem[] Presets, PersistedPresetOperation[] Operations);
    /// <summary>One intake entry inside a day, with the amount in millilitres.</summary>
    private sealed record PersistedIntakeEntry(Guid Id, int Millilitres, bool Undone);
    /// <summary>One intake change, replayed so the day's total is derived rather than stored twice.</summary>
    private sealed record PersistedIntakeOperation(Guid OperationId, Guid EntryId, WaterIntakeChangeKind Kind, int Millilitres);
    /// <summary>One stored day, including the legacy single total kept so data written before the entry list still reads.</summary>
    private sealed record PersistedIntakeDay(int LegacyMillilitres, PersistedIntakeEntry[] Entries, PersistedIntakeOperation[] Operations);
    /// <summary>One reminder change, replayed so the last write wins deterministically.</summary>
    private sealed record PersistedReminderOperation(Guid OperationId, bool Enabled, TimeOnly[] LocalTimes, TimeOnly? QuietStart, TimeOnly? QuietEnd);
    /// <summary>The stored reminder settings: whether they are on and when the first one fires.</summary>
    private sealed record PersistedReminderConfig(bool Enabled, TimeOnly[] LocalTimes, TimeOnly? QuietStart, TimeOnly? QuietEnd, PersistedReminderOperation[] Operations);
}
