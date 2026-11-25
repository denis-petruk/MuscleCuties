
namespace MuscleCuties.Infrastructure.PostgreSql.Persistence;

/// <summary>The outcome of an idempotent command: the answer and whether this call produced it or replayed it.</summary>
public sealed record IdempotencyExecution<TResponse>(TResponse Response, bool Replayed);

/// <summary>Thrown when a command key is reused with different content, which is a client bug rather than a retry.</summary>
public sealed class PostgresIdempotencyConflictException(string commandKey)
    : InvalidOperationException($"Idempotency key '{commandKey}' was already used with a different request.");

/// <summary>Runs a tenant command and its replay journal write in one retriable transaction.</summary>
public sealed class PostgresIdempotencyExecutor
{
    private const int MaximumCommandKeyLength = 128;
    private const int Sha256HexLength = 64;
    private const string IdempotencyConstraint = "uq_command_idempotency_user_key";
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The request's hash, compared on replay so the same key with different content is refused.</summary>
    public static string ComputeRequestHash<TRequest>(TRequest request)
    {
        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request);
        return Convert.ToHexString(SHA256.HashData(requestBytes)).ToLowerInvariant();
    }

    /// <summary>Runs the operation once per key and replays the stored response afterwards.</summary>
    public async Task<IdempotencyExecution<TResponse>> ExecuteAsync<TResponse>(
        PostgresDbContext context,
        Guid userId,
        string commandKey,
        string requestHash,
        Func<PostgresDbContext, CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);
        if (userId == Guid.Empty)
            throw new ArgumentException("A non-empty authenticated user ID is required.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(commandKey);
        if (commandKey.Length > MaximumCommandKeyLength)
            throw new ArgumentOutOfRangeException(nameof(commandKey), $"Command keys cannot exceed {MaximumCommandKeyLength} characters.");
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        if (requestHash.Length != Sha256HexLength || requestHash.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("The request hash must be a SHA-256 hexadecimal value.", nameof(requestHash));

        var normalizedHash = requestHash.ToLowerInvariant();
        var executionStrategy = context.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync<IdempotencyExecution<TResponse>>(async strategyCancellationToken =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(strategyCancellationToken);
            try
            {
                await PostgresTenantContext.SetForCurrentTransactionAsync(context, userId, strategyCancellationToken);
                var existing = await context.CommandIdempotencyRecords.SingleOrDefaultAsync(
                    entry => entry.UserId == userId && entry.CommandKey == commandKey,
                    strategyCancellationToken);
                if (existing is not null)
                {
                    EnsureMatchingRequest(existing, normalizedHash, commandKey);
                    var replay = DeserializeResponse<TResponse>(existing);
                    await transaction.CommitAsync(strategyCancellationToken);
                    return new IdempotencyExecution<TResponse>(replay, true);
                }

                var journalEntry = new CommandIdempotencyRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    CommandKey = commandKey,
                    RequestHash = normalizedHash
                };
                context.CommandIdempotencyRecords.Add(journalEntry);
                await context.SaveChangesAsync(strategyCancellationToken);

                var response = await operation(context, strategyCancellationToken);
                journalEntry.ResponseJson = JsonSerializer.Serialize(response, _jsonOptions);
                journalEntry.CompletedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(strategyCancellationToken);
                await transaction.CommitAsync(strategyCancellationToken);
                return new IdempotencyExecution<TResponse>(response, false);
            }
            catch (DbUpdateException exception) when (IsIdempotencyUniqueViolation(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return await ReadWinnerAsync<TResponse>(context, userId, commandKey, normalizedHash, strategyCancellationToken);
            }
            catch
            {
                context.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>Reads the response the first caller stored.</summary>
    private async Task<IdempotencyExecution<TResponse>> ReadWinnerAsync<TResponse>(
        PostgresDbContext context,
        Guid userId,
        string commandKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        await using var replayTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await PostgresTenantContext.SetForCurrentTransactionAsync(context, userId, cancellationToken);
        var winner = await context.CommandIdempotencyRecords.SingleOrDefaultAsync(
            entry => entry.UserId == userId && entry.CommandKey == commandKey,
            cancellationToken);
        if (winner is null)
            throw new InvalidOperationException("The idempotency constraint conflicted, but no committed journal row was found.");

        EnsureMatchingRequest(winner, requestHash, commandKey);
        var response = DeserializeResponse<TResponse>(winner);
        await replayTransaction.CommitAsync(cancellationToken);
        return new IdempotencyExecution<TResponse>(response, true);
    }

    /// <summary>Refuses a replay whose request hash differs from the recorded one.</summary>
    private static void EnsureMatchingRequest(CommandIdempotencyRecord entry, string requestHash, string commandKey)
    {
        if (!string.Equals(entry.RequestHash, requestHash, StringComparison.Ordinal))
            throw new PostgresIdempotencyConflictException(commandKey);
    }

    /// <summary>The stored answer, deserialised as the caller's response type.</summary>
    private TResponse DeserializeResponse<TResponse>(CommandIdempotencyRecord entry)
    {
        if (entry.ResponseJson is null || entry.CompletedAt is null)
            throw new InvalidOperationException("A committed idempotency journal row is incomplete.");

        return JsonSerializer.Deserialize<TResponse>(entry.ResponseJson, _jsonOptions)
            ?? throw new InvalidOperationException("The persisted idempotency response could not be restored.");
    }

    /// <summary>True when the insert lost the race on the idempotency key.</summary>
    private static bool IsIdempotencyUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: IdempotencyConstraint
        };
}
