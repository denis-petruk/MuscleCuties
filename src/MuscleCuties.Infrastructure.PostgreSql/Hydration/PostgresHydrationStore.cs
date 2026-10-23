using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Hydration;

namespace MuscleCuties.Infrastructure.PostgreSql.Hydration;

public sealed class PostgresHydrationStore(
    PostgresDbContext dbContext,
    TimeProvider timeProvider)
    : IWaterPresetInitializationStore, IWaterIntakeStore, IWaterReminderConfigurationStore
{
    private const string PresetsStateKey = "state:hydration:presets";
    private const string RemindersStateKey = "state:hydration:reminders";
    private const string IntakeStatePrefix = "state:hydration:intake:";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    public async Task<StoredWaterIntakeDay> ReadAsync(Guid accountId, DateOnly localDate, CancellationToken cancellationToken)
    {
        var key = IntakeKey(localDate);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, accountId, token).ConfigureAwait(false);
            var row = await dbContext.CommandIdempotencyRecords.AsNoTracking()
                .SingleOrDefaultAsync(r => r.UserId == accountId && r.CommandKey == key, token)
                .ConfigureAwait(false);
            var diaryWaterMl = await dbContext.DailyNutritionLogs.AsNoTracking()
                .Where(d => d.UserId == accountId && d.LocalDate == localDate && !d.IsDeleted)
                .Select(d => (int?)d.WaterIntakeMl)
                .FirstOrDefaultAsync(token)
                .ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);

            if (row?.ResponseJson is null)
            {
                var legacyBaseline = Math.Max(0, diaryWaterMl ?? 0);
                return new StoredWaterIntakeDay(WaterIntakeDay.Restore(accountId, localDate, legacyBaseline, [], []), null);
            }

            var payload = JsonSerializer.Deserialize<PersistedIntakeDay>(row.ResponseJson, JsonOptions)
                ?? new PersistedIntakeDay(0, [], []);
            var day = WaterIntakeDay.Restore(
                accountId,
                localDate,
                payload.LegacyMillilitres,
                payload.Entries.Select(e => new WaterIntakeEntry(e.Id, e.Millilitres, e.Undone)),
                payload.Operations.Select(o => new WaterIntakeChange(o.OperationId, o.EntryId, o.Kind, o.Millilitres)));
            return new StoredWaterIntakeDay(day, row.RequestHash);
        }, cancellationToken).ConfigureAwait(false);
    }

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

            var row = await dbContext.CommandIdempotencyRecords
                .SingleOrDefaultAsync(r => r.UserId == accountId && r.CommandKey == key, token)
                .ConfigureAwait(false);
            var currentToken = row?.RequestHash;
            if (!string.Equals(currentToken, expectedVersion, StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(token).ConfigureAwait(false);
                return null;
            }

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

            var diaryRow = await dbContext.DailyNutritionLogs
                .FirstOrDefaultAsync(d => d.UserId == accountId && d.LocalDate == day.Date && !d.IsDeleted, token)
                .ConfigureAwait(false);
            if (diaryRow is not null)
            {
                diaryRow.WaterIntakeMl = day.TotalMillilitres;
                diaryRow.UpdatedAt = now;
            }

            await dbContext.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return new StoredWaterIntakeDay(day, nextVersion);
        }, cancellationToken).ConfigureAwait(false);
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

    private static string IntakeKey(DateOnly date) => $"{IntakeStatePrefix}{date:yyyy-MM-dd}";

    private static string NextVersionToken(string? currentToken) =>
        int.TryParse(currentToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? (parsed + 1).ToString(CultureInfo.InvariantCulture)
            : "1";

    private static PersistedPresetState SerializePresetState(WaterPresetState state) => new(
        state.Catalog.Presets.Select(p => new PersistedPresetItem(p.Id, p.Label, p.Portion.Millilitres)).ToArray(),
        state.Operations.Select(o => new PersistedPresetOperation(o.OperationId, o.Kind, o.PresetId, o.Label, o.Millilitres, o.OrderedIds.ToArray())).ToArray());

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

    private sealed record PersistedPresetItem(Guid Id, string Label, int Millilitres);
    private sealed record PersistedPresetOperation(Guid OperationId, WaterPresetMutationKind Kind, Guid PresetId, string? Label, int Millilitres, Guid[] OrderedIds);
    private sealed record PersistedPresetState(PersistedPresetItem[] Presets, PersistedPresetOperation[] Operations);
    private sealed record PersistedIntakeEntry(Guid Id, int Millilitres, bool Undone);
    private sealed record PersistedIntakeOperation(Guid OperationId, Guid EntryId, WaterIntakeChangeKind Kind, int Millilitres);
    private sealed record PersistedIntakeDay(int LegacyMillilitres, PersistedIntakeEntry[] Entries, PersistedIntakeOperation[] Operations);
    private sealed record PersistedReminderOperation(Guid OperationId, bool Enabled, TimeOnly[] LocalTimes, TimeOnly? QuietStart, TimeOnly? QuietEnd);
    private sealed record PersistedReminderConfig(bool Enabled, TimeOnly[] LocalTimes, TimeOnly? QuietStart, TimeOnly? QuietEnd, PersistedReminderOperation[] Operations);
}
