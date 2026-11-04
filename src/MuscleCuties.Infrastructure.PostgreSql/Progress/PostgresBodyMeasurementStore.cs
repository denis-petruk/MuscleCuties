using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Progress;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Progress.BodyMeasurements;

namespace MuscleCuties.Infrastructure.PostgreSql.Progress;

public sealed class PostgresBodyMeasurementStore(
    PostgresDbContext dbContext,
    TimeProvider timeProvider) : IBodyMeasurementStore
{
    private const string MeasurementsStateKey = "state:progress:measurements";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BodyMeasurementOutputDto?> TryReplayCreateAsync(
        Guid ownerId,
        CreateBodyMeasurementInputDto input,
        CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(ownerId, cancellationToken).ConfigureAwait(false);
        var signature = CreateSignature(input);
        var receipt = state.Receipts.FirstOrDefault(r => r.CommandId == input.CommandId);
        if (receipt is null) return null;
        if (!string.Equals(receipt.Signature, signature, StringComparison.Ordinal))
            throw new InvalidOperationException("Command identity was reused with a different measurement payload.");
        return receipt.Output;
    }

    public async Task<BodyMeasurementOutputDto?> TryReplayCorrectionAsync(
        Guid ownerId,
        CorrectBodyMeasurementInputDto input,
        CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(ownerId, cancellationToken).ConfigureAwait(false);
        var signature = CorrectSignature(input);
        var receipt = state.Receipts.FirstOrDefault(r => r.CommandId == input.CommandId);
        if (receipt is null) return null;
        if (!string.Equals(receipt.Signature, signature, StringComparison.Ordinal))
            throw new InvalidOperationException("Command identity was reused with a different correction payload.");
        return receipt.Output;
    }

    public async Task<bool> WasDeletionAppliedAsync(
        Guid ownerId,
        DeleteBodyMeasurementInputDto input,
        CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(ownerId, cancellationToken).ConfigureAwait(false);
        var signature = DeleteSignature(input);
        var receipt = state.Receipts.FirstOrDefault(r => r.CommandId == input.CommandId);
        if (receipt is null) return false;
        if (!string.Equals(receipt.Signature, signature, StringComparison.Ordinal))
            throw new InvalidOperationException("Command identity was reused with a different deletion payload.");
        return true;
    }

    public async Task<BodyMeasurement> CreateAsync(
        Guid ownerId,
        Guid commandId,
        BodyMeasurement measurement,
        CancellationToken cancellationToken)
    {
        var signature = $"create:{measurement.Id:N}:{BodyMeasurementCodes.SiteCode(measurement.Site)}:{BodyMeasurementCodes.SideCode(measurement.Side)}:{measurement.LocalDate:yyyy-MM-dd}:{measurement.EnteredValue.ToString(CultureInfo.InvariantCulture)}:{BodyMeasurementCodes.UnitCode(measurement.EnteredUnit)}";
        return await MutateStateAsync(ownerId, cancellationToken, state =>
        {
            var existingReceipt = state.Receipts.FirstOrDefault(r => r.CommandId == commandId);
            if (existingReceipt is not null)
            {
                if (!string.Equals(existingReceipt.Signature, signature, StringComparison.Ordinal))
                    throw new InvalidOperationException("Command identity was reused with a different measurement payload.");
                return RestoreDomain(state.Items.Single(i => i.Id == measurement.Id));
            }

            if (state.Items.Any(i => i.Id == measurement.Id))
                throw new InvalidOperationException("A measurement with this identity already exists.");
            var restoredActive = state.Items.Where(i => !i.IsDeleted).Select(RestoreDomain);
            if (restoredActive.Any(other => other.OccupiesSameDailySlotAs(measurement)))
                throw new InvalidOperationException("An active reading already exists for this site, side, and date.");

            var persisted = ToPersisted(measurement);
            var output = ToOutput(measurement);
            state.Items.Add(persisted);
            state.Receipts.Add(new PersistedReceipt(commandId, signature, output));
            return measurement;
        }).ConfigureAwait(false);
    }

    public async Task<BodyMeasurement?> LoadAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(ownerId, cancellationToken).ConfigureAwait(false);
        var item = state.Items.FirstOrDefault(i => i.Id == id);
        return item is null ? null : RestoreDomain(item);
    }

    public async Task<BodyMeasurement> CorrectAsync(
        Guid ownerId,
        Guid commandId,
        BodyMeasurement measurement,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var signature = $"correct:{measurement.Id:N}:{expectedVersion}:{BodyMeasurementCodes.SiteCode(measurement.Site)}:{BodyMeasurementCodes.SideCode(measurement.Side)}:{measurement.LocalDate:yyyy-MM-dd}:{measurement.EnteredValue.ToString(CultureInfo.InvariantCulture)}:{BodyMeasurementCodes.UnitCode(measurement.EnteredUnit)}";
        return await MutateStateAsync(ownerId, cancellationToken, state =>
        {
            var existingReceipt = state.Receipts.FirstOrDefault(r => r.CommandId == commandId);
            if (existingReceipt is not null)
            {
                if (!string.Equals(existingReceipt.Signature, signature, StringComparison.Ordinal))
                    throw new InvalidOperationException("Command identity was reused with a different correction payload.");
                return RestoreDomain(state.Items.Single(i => i.Id == measurement.Id));
            }

            var index = state.Items.FindIndex(i => i.Id == measurement.Id);
            if (index < 0)
                throw new KeyNotFoundException("Measurement was not found for the current account.");
            if (state.Items[index].Version != expectedVersion)
                throw new InvalidOperationException("The measurement version is stale.");

            var restoredActive = state.Items.Where(i => !i.IsDeleted && i.Id != measurement.Id).Select(RestoreDomain);
            if (restoredActive.Any(other => other.OccupiesSameDailySlotAs(measurement)))
                throw new InvalidOperationException("An active reading already exists for this site, side, and date.");

            state.Items[index] = ToPersisted(measurement);
            state.Receipts.Add(new PersistedReceipt(commandId, signature, ToOutput(measurement)));
            return measurement;
        }).ConfigureAwait(false);
    }

    public async Task DeleteAsync(
        Guid ownerId,
        Guid commandId,
        BodyMeasurement measurement,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var signature = $"delete:{measurement.Id:N}:{expectedVersion}";
        await MutateStateAsync(ownerId, cancellationToken, state =>
        {
            var existingReceipt = state.Receipts.FirstOrDefault(r => r.CommandId == commandId);
            if (existingReceipt is not null)
            {
                if (!string.Equals(existingReceipt.Signature, signature, StringComparison.Ordinal))
                    throw new InvalidOperationException("Command identity was reused with a different deletion payload.");
                return measurement;
            }

            var index = state.Items.FindIndex(i => i.Id == measurement.Id);
            if (index < 0)
                throw new KeyNotFoundException("Measurement was not found for the current account.");
            if (state.Items[index].Version != expectedVersion)
                throw new InvalidOperationException("The measurement version is stale.");

            state.Items[index] = ToPersisted(measurement);
            state.Receipts.Add(new PersistedReceipt(commandId, signature, null));
            return measurement;
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BodyMeasurement>> LoadHistoryAsync(
        Guid ownerId,
        DateOnly from,
        DateOnly through,
        CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(ownerId, cancellationToken).ConfigureAwait(false);
        return state.Items
            .Where(i => !i.IsDeleted && i.LocalDate >= from && i.LocalDate <= through)
            .OrderBy(i => i.LocalDate)
            .ThenBy(i => i.Site)
            .ThenBy(i => i.Side)
            .ThenBy(i => i.Id)
            .Select(RestoreDomain)
            .ToArray();
    }

    private async Task<PersistedMeasurementState> ReadStateAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, ownerId, token).ConfigureAwait(false);
            var row = await dbContext.CommandIdempotencyRecords.AsNoTracking()
                .SingleOrDefaultAsync(r => r.UserId == ownerId && r.CommandKey == MeasurementsStateKey, token)
                .ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            if (row?.ResponseJson is null)
                return new PersistedMeasurementState([], []);
            return JsonSerializer.Deserialize<PersistedMeasurementState>(row.ResponseJson, JsonOptions)
                ?? new PersistedMeasurementState([], []);
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> MutateStateAsync<T>(
        Guid ownerId,
        CancellationToken cancellationToken,
        Func<PersistedMeasurementState, T> mutate)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, ownerId, token).ConfigureAwait(false);
            var row = await dbContext.CommandIdempotencyRecords
                .SingleOrDefaultAsync(r => r.UserId == ownerId && r.CommandKey == MeasurementsStateKey, token)
                .ConfigureAwait(false);
            var state = row?.ResponseJson is null
                ? new PersistedMeasurementState([], [])
                : JsonSerializer.Deserialize<PersistedMeasurementState>(row.ResponseJson, JsonOptions)
                  ?? new PersistedMeasurementState([], []);

            var result = mutate(state);
            var nextVersion = row is not null && int.TryParse(row.RequestHash, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? (v + 1).ToString(CultureInfo.InvariantCulture)
                : "1";
            var json = JsonSerializer.Serialize(state, JsonOptions);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (row is null)
            {
                dbContext.CommandIdempotencyRecords.Add(new CommandIdempotencyRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = ownerId,
                    CommandKey = MeasurementsStateKey,
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
            return result;
        }, cancellationToken).ConfigureAwait(false);
    }

    private BodyMeasurement RestoreDomain(PersistedMeasurement item) =>
        BodyMeasurement.Restore(
            item.Id,
            item.Site,
            item.Side,
            item.LocalDate,
            item.EnteredValue,
            item.EnteredUnit,
            item.Version,
            item.IsDeleted,
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(1));

    private static PersistedMeasurement ToPersisted(BodyMeasurement m) =>
        new(m.Id, m.Site, m.Side, m.LocalDate, m.EnteredValue, m.EnteredUnit, m.ValueCentimeters, m.Version, m.IsDeleted);

    private static BodyMeasurementOutputDto ToOutput(BodyMeasurement m) =>
        new(
            m.Id,
            BodyMeasurementCodes.SiteCode(m.Site),
            BodyMeasurementCodes.SideCode(m.Side),
            m.LocalDate,
            m.EnteredValue,
            BodyMeasurementCodes.UnitCode(m.EnteredUnit),
            m.ValueCentimeters,
            m.Version);

    private static string CreateSignature(CreateBodyMeasurementInputDto input) =>
        $"create:{input.Id:N}:{input.Site}:{input.Side}:{input.LocalDate:yyyy-MM-dd}:{Convert.ToDecimal(input.Value).ToString(CultureInfo.InvariantCulture)}:{input.Unit}";

    private static string CorrectSignature(CorrectBodyMeasurementInputDto input) =>
        $"correct:{input.Id:N}:{input.ExpectedVersion}:{input.Site}:{input.Side}:{input.LocalDate:yyyy-MM-dd}:{Convert.ToDecimal(input.Value).ToString(CultureInfo.InvariantCulture)}:{input.Unit}";

    private static string DeleteSignature(DeleteBodyMeasurementInputDto input) =>
        $"delete:{input.Id:N}:{input.ExpectedVersion}";

    private sealed record PersistedMeasurement(
        Guid Id,
        BodyMeasurementSite Site,
        BodyMeasurementSide Side,
        DateOnly LocalDate,
        decimal EnteredValue,
        BodyMeasurementUnit EnteredUnit,
        decimal ValueCentimeters,
        int Version,
        bool IsDeleted);

    private sealed record PersistedReceipt(Guid CommandId, string Signature, BodyMeasurementOutputDto? Output);
    private sealed record PersistedMeasurementState(List<PersistedMeasurement> Items, List<PersistedReceipt> Receipts);
}
