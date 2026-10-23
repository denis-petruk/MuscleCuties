using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Fasting;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Fasting;

namespace MuscleCuties.Infrastructure.PostgreSql.Fasting;

public sealed class PostgresFastingStore(
    PostgresDbContext dbContext,
    TimeProvider timeProvider) : IFastingSessionStore
{
    private const string FastingStateKey = "state:fasting:sessions";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<FastingSessionOutputDto?> TryReplayCommandAsync(
        Guid accountId,
        Guid commandId,
        string payloadSignature,
        CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(accountId, cancellationToken).ConfigureAwait(false);
        var receipt = state.Receipts.FirstOrDefault(r => r.CommandId == commandId);
        if (receipt is null) return null;
        if (!string.Equals(receipt.Signature, payloadSignature, StringComparison.Ordinal))
            throw new InvalidOperationException("Command identity was reused for a different fasting operation.");
        var session = state.Sessions.FirstOrDefault(s => s.Id == receipt.SessionId);
        return session is null ? null : FastingSessionUseCase.Map(RestoreDomain(accountId, session), timeProvider.GetUtcNow(), false);
    }

    public async Task<IReadOnlyList<FastingSession>> LoadAllAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(accountId, cancellationToken).ConfigureAwait(false);
        return state.Sessions.Select(s => RestoreDomain(accountId, s)).ToArray();
    }

    public async Task<FastingSession?> LoadByIdAsync(Guid accountId, Guid sessionId, CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(accountId, cancellationToken).ConfigureAwait(false);
        var item = state.Sessions.FirstOrDefault(s => s.Id == sessionId);
        return item is null ? null : RestoreDomain(accountId, item);
    }

    public async Task<FastingSession> SaveAsync(
        Guid accountId,
        Guid commandId,
        string payloadSignature,
        FastingSession session,
        int? expectedVersion,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, accountId, token).ConfigureAwait(false);

            var row = await dbContext.CommandIdempotencyRecords
                .SingleOrDefaultAsync(r => r.UserId == accountId && r.CommandKey == FastingStateKey, token)
                .ConfigureAwait(false);
            var state = row?.ResponseJson is null
                ? new PersistedFastingState([], [])
                : JsonSerializer.Deserialize<PersistedFastingState>(row.ResponseJson, JsonOptions)
                  ?? new PersistedFastingState([], []);

            var receipt = state.Receipts.FirstOrDefault(r => r.CommandId == commandId);
            if (receipt is not null)
            {
                if (!string.Equals(receipt.Signature, payloadSignature, StringComparison.Ordinal))
                    throw new InvalidOperationException("Command identity was reused for a different fasting operation.");
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return RestoreDomain(accountId, state.Sessions.Single(s => s.Id == receipt.SessionId));
            }

            var index = state.Sessions.FindIndex(s => s.Id == session.Id);
            if (expectedVersion is null)
            {
                if (index >= 0)
                    throw new InvalidOperationException("A fasting session with this identity already exists.");
                state.Sessions.Add(ToPersisted(session));
            }
            else
            {
                if (index < 0)
                    throw new KeyNotFoundException("Fasting record was not found for the current account.");
                if (state.Sessions[index].Version != expectedVersion.Value)
                    throw new InvalidOperationException("The fasting record changed elsewhere. Reload before trying again.");
                state.Sessions[index] = ToPersisted(session);
            }

            state.Receipts.Add(new PersistedFastingReceipt(commandId, payloadSignature, session.Id));
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
                    UserId = accountId,
                    CommandKey = FastingStateKey,
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
            return session;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PersistedFastingState> ReadStateAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, accountId, token).ConfigureAwait(false);
            var row = await dbContext.CommandIdempotencyRecords.AsNoTracking()
                .SingleOrDefaultAsync(r => r.UserId == accountId && r.CommandKey == FastingStateKey, token)
                .ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            if (row?.ResponseJson is null)
                return new PersistedFastingState([], []);
            return JsonSerializer.Deserialize<PersistedFastingState>(row.ResponseJson, JsonOptions)
                ?? new PersistedFastingState([], []);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static FastingSession RestoreDomain(Guid accountId, PersistedFastingSession s) =>
        FastingSession.Restore(
            s.Id,
            accountId,
            s.AttestationPolicyVersion,
            s.StartedAtUtc,
            s.DisplayUtcOffsetMinutes,
            s.Status,
            s.EndedAtUtc,
            s.PlannedEndAtUtc,
            s.EnablePlannedEndReminder,
            s.IsManuallyCorrected,
            s.LastCorrectedAtUtc,
            s.Version,
            s.Pauses.Select(p => new FastingPauseInterval(p.PausedAtUtc, p.ResumedAtUtc)));

    private static PersistedFastingSession ToPersisted(FastingSession s) => new(
        s.Id,
        s.AttestationPolicyVersion,
        s.StartedAtUtc,
        s.DisplayUtcOffsetMinutes,
        s.Status,
        s.EndedAtUtc,
        s.PlannedEndAtUtc,
        s.EnablePlannedEndReminder,
        s.IsManuallyCorrected,
        s.LastCorrectedAtUtc,
        s.Version,
        s.Pauses.Select(p => new PersistedPause(p.PausedAtUtc, p.ResumedAtUtc)).ToArray());

    private sealed record PersistedPause(DateTimeOffset PausedAtUtc, DateTimeOffset? ResumedAtUtc);

    private sealed record PersistedFastingSession(
        Guid Id,
        string AttestationPolicyVersion,
        DateTimeOffset StartedAtUtc,
        int DisplayUtcOffsetMinutes,
        FastingSessionStatus Status,
        DateTimeOffset? EndedAtUtc,
        DateTimeOffset? PlannedEndAtUtc,
        bool EnablePlannedEndReminder,
        bool IsManuallyCorrected,
        DateTimeOffset? LastCorrectedAtUtc,
        int Version,
        PersistedPause[] Pauses);

    private sealed record PersistedFastingReceipt(Guid CommandId, string Signature, Guid SessionId);
    private sealed record PersistedFastingState(List<PersistedFastingSession> Sessions, List<PersistedFastingReceipt> Receipts);
}
