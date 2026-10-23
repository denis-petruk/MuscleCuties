using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;

public sealed class PostgresWorkoutActiveSessionStore(
    PostgresDbContext dbContext,
    PostgresIdempotencyExecutor idempotencyExecutor,
    TimeProvider timeProvider) : IWorkoutActiveSessionStore
{
    private const string StartCommandPrefix = "workout.session.start:";
    private const string UpsertSetCommandPrefix = "workout.session.set:";
    private const string FinishCommandPrefix = "workout.session.finish:";
    private const string CancelCommandPrefix = "workout.session.cancel:";

    public async Task<ActiveWorkoutSessionSnapshot?> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            var snapshot = await LoadSessionSnapshotAsync(dbContext, userId, sessionId, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return snapshot;
        }).ConfigureAwait(false);

    public async Task<ActiveWorkoutSessionSnapshot> StartSessionAsync(
        Guid userId,
        StartWorkoutSessionCommand command,
        CancellationToken cancellationToken)
    {
        var commandKey = BuildCommandKey(StartCommandPrefix, command.IdempotencyKey);
        var requestHash = PostgresIdempotencyExecutor.ComputeRequestHash(new
        {
            command.SessionId,
            command.WorkoutName,
            command.StartedAtUtc,
            command.Notes
        });

        var execution = await idempotencyExecutor.ExecuteAsync(
            dbContext,
            userId,
            commandKey,
            requestHash,
            async (context, ct) =>
            {
                await LockSessionAsync(context, command.SessionId, ct).ConfigureAwait(false);
                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                var session = await context.WorkoutSessionLogs.SingleOrDefaultAsync(
                        row => row.UserId == userId && row.Id == command.SessionId,
                        ct)
                    .ConfigureAwait(false);
                if (session is not null)
                {
                    if (session.IsDeleted)
                        throw new InvalidOperationException("Cannot restart a canceled workout session.");
                    return (await LoadSessionSnapshotAsync(context, userId, command.SessionId, ct)
                        .ConfigureAwait(false))!;
                }

                session = new WorkoutSessionLogRecord
                {
                    Id = command.SessionId,
                    UserId = userId,
                    WorkoutName = command.WorkoutName,
                    StartedAt = DateTime.SpecifyKind(command.StartedAtUtc, DateTimeKind.Utc),
                    Notes = command.Notes,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };
                context.WorkoutSessionLogs.Add(session);
                try
                {
                    await context.SaveChangesAsync(ct).ConfigureAwait(false);
                }
                catch (DbUpdateException exception) when (IsUniqueViolation(exception))
                {
                    throw new InvalidOperationException("The workout session ID conflicts with another record.");
                }

                return (await LoadSessionSnapshotAsync(context, userId, command.SessionId, ct)
                    .ConfigureAwait(false))!;
            },
            cancellationToken).ConfigureAwait(false);

        return execution.Replayed
            ? execution.Response with { Replayed = true }
            : execution.Response;
    }

    public async Task<ActiveWorkoutSessionSnapshot?> UpsertSetAsync(
        Guid userId,
        UpsertWorkoutSessionSetCommand command,
        CancellationToken cancellationToken)
    {
        var commandKey = BuildCommandKey(UpsertSetCommandPrefix, command.IdempotencyKey);
        var requestHash = PostgresIdempotencyExecutor.ComputeRequestHash(new
        {
            command.SessionId,
            command.Set
        });

        var execution = await idempotencyExecutor.ExecuteAsync<ActiveWorkoutSessionSnapshot?>(
            dbContext,
            userId,
            commandKey,
            requestHash,
            async (context, ct) =>
            {
                await LockSessionAsync(context, command.SessionId, ct).ConfigureAwait(false);
                var session = await context.WorkoutSessionLogs.SingleOrDefaultAsync(
                        row => row.UserId == userId && row.Id == command.SessionId,
                        ct)
                    .ConfigureAwait(false);
                if (session is null)
                    return null;
                if (session.IsDeleted || session.CompletedAt is not null)
                    throw new InvalidOperationException("Cannot modify sets on a completed or canceled workout session.");

                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                await ApplySetUpsertAsync(context, userId, session.Id, command.Set, nowUtc, ct).ConfigureAwait(false);
                await context.SaveChangesAsync(ct).ConfigureAwait(false);
                await RecalculateSessionVolumeAsync(context, userId, session, nowUtc, ct).ConfigureAwait(false);
                await context.SaveChangesAsync(ct).ConfigureAwait(false);

                return await LoadSessionSnapshotAsync(context, userId, command.SessionId, ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return execution.Replayed && execution.Response is not null
            ? execution.Response with { Replayed = true }
            : execution.Response;
    }

    public async Task<ActiveWorkoutSessionSnapshot?> FinishSessionAsync(
        Guid userId,
        FinishWorkoutSessionCommand command,
        CancellationToken cancellationToken)
    {
        var commandKey = BuildCommandKey(FinishCommandPrefix, command.IdempotencyKey);
        var requestHash = PostgresIdempotencyExecutor.ComputeRequestHash(new
        {
            command.SessionId,
            command.CompletedAtUtc,
            command.TotalDurationSeconds,
            command.RpeRating,
            command.Notes,
            FinalSets = command.FinalSets ?? []
        });

        var execution = await idempotencyExecutor.ExecuteAsync<ActiveWorkoutSessionSnapshot?>(
            dbContext,
            userId,
            commandKey,
            requestHash,
            async (context, ct) =>
            {
                await LockSessionAsync(context, command.SessionId, ct).ConfigureAwait(false);
                var session = await context.WorkoutSessionLogs.SingleOrDefaultAsync(
                        row => row.UserId == userId && row.Id == command.SessionId,
                        ct)
                    .ConfigureAwait(false);
                if (session is null)
                    return null;
                if (session.IsDeleted)
                    throw new InvalidOperationException("Cannot finish a canceled workout session.");

                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                foreach (var set in command.FinalSets ?? [])
                    await ApplySetUpsertAsync(context, userId, session.Id, set, nowUtc, ct).ConfigureAwait(false);

                session.CompletedAt = DateTime.SpecifyKind(command.CompletedAtUtc, DateTimeKind.Utc);
                session.TotalDurationSeconds = command.TotalDurationSeconds;
                session.RpeRating = command.RpeRating;
                if (command.Notes is not null)
                    session.Notes = command.Notes;

                await context.SaveChangesAsync(ct).ConfigureAwait(false);
                await RecalculateSessionVolumeAsync(context, userId, session, nowUtc, ct).ConfigureAwait(false);
                await context.SaveChangesAsync(ct).ConfigureAwait(false);

                return await LoadSessionSnapshotAsync(context, userId, command.SessionId, ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return execution.Replayed && execution.Response is not null
            ? execution.Response with { Replayed = true }
            : execution.Response;
    }

    public async Task<ActiveWorkoutSessionSnapshot?> CancelSessionAsync(
        Guid userId,
        CancelWorkoutSessionCommand command,
        CancellationToken cancellationToken)
    {
        var commandKey = BuildCommandKey(CancelCommandPrefix, command.IdempotencyKey);
        var requestHash = PostgresIdempotencyExecutor.ComputeRequestHash(new { command.SessionId });

        var execution = await idempotencyExecutor.ExecuteAsync<ActiveWorkoutSessionSnapshot?>(
            dbContext,
            userId,
            commandKey,
            requestHash,
            async (context, ct) =>
            {
                await LockSessionAsync(context, command.SessionId, ct).ConfigureAwait(false);
                var session = await context.WorkoutSessionLogs.SingleOrDefaultAsync(
                        row => row.UserId == userId && row.Id == command.SessionId,
                        ct)
                    .ConfigureAwait(false);
                if (session is null)
                    return null;
                if (session.CompletedAt is not null)
                    throw new InvalidOperationException("Cannot cancel an already completed workout session.");

                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                session.IsDeleted = true;
                session.DeletedAt = nowUtc;
                session.UpdatedAt = nowUtc;

                var sets = await context.ExerciseSetLogs
                    .Where(row => row.UserId == userId && row.WorkoutSessionLogId == session.Id && !row.IsDeleted)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                foreach (var set in sets)
                {
                    set.IsDeleted = true;
                    set.DeletedAt = nowUtc;
                    set.UpdatedAt = nowUtc;
                }

                await context.SaveChangesAsync(ct).ConfigureAwait(false);
                return await LoadSessionSnapshotAsync(context, userId, command.SessionId, ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return execution.Replayed && execution.Response is not null
            ? execution.Response with { Replayed = true }
            : execution.Response;
    }

    private static async Task ApplySetUpsertAsync(
        PostgresDbContext context,
        Guid userId,
        Guid sessionId,
        ActiveExerciseSetInput input,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var row = await context.ExerciseSetLogs.SingleOrDefaultAsync(
                existing => existing.UserId == userId && existing.Id == input.SetId,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is not null && row.WorkoutSessionLogId != sessionId)
            throw new InvalidOperationException("The exercise set ID belongs to a different workout session.");

        row ??= new ExerciseSetLogRecord
        {
            Id = input.SetId,
            UserId = userId,
            WorkoutSessionLogId = sessionId,
            CreatedAt = nowUtc
        };
        row.ExerciseId = input.ExerciseId;
        row.ExerciseName = input.ExerciseName;
        row.SetNumber = input.SetNumber;
        row.WeightKg = input.WeightKg;
        row.RepsCompleted = input.RepsCompleted;
        row.Rpe = input.Rpe;
        row.IsWarmup = input.IsWarmup;
        row.RestTimeSeconds = input.RestTimeSeconds;
        row.IsDeleted = false;
        row.DeletedAt = null;
        row.UpdatedAt = nowUtc;

        if (context.Entry(row).State == EntityState.Detached)
            context.ExerciseSetLogs.Add(row);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new InvalidOperationException("The exercise set ID conflicts with another record.");
        }
    }

    private static async Task RecalculateSessionVolumeAsync(
        PostgresDbContext context,
        Guid userId,
        WorkoutSessionLogRecord session,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var activeWorkingSets = await context.ExerciseSetLogs.AsNoTracking()
            .Where(row => row.UserId == userId &&
                          row.WorkoutSessionLogId == session.Id &&
                          !row.IsDeleted &&
                          !row.IsWarmup)
            .Select(row => new { row.WeightKg, row.RepsCompleted })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        session.TotalVolumeKg = activeWorkingSets.Sum(set => set.WeightKg * set.RepsCompleted);
        session.UpdatedAt = nowUtc;
    }

    private static async Task<ActiveWorkoutSessionSnapshot?> LoadSessionSnapshotAsync(
        PostgresDbContext context,
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await context.WorkoutSessionLogs.AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == userId && row.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null)
            return null;

        var sets = await context.ExerciseSetLogs.AsNoTracking()
            .Where(row => row.UserId == userId && row.WorkoutSessionLogId == sessionId && !row.IsDeleted)
            .OrderBy(row => row.SetNumber)
            .ThenBy(row => row.CreatedAt)
            .ThenBy(row => row.Id)
            .Select(row => new ActiveExerciseSetSnapshot(
                row.Id,
                row.ExerciseId,
                row.ExerciseName,
                row.SetNumber,
                row.WeightKg,
                row.RepsCompleted,
                row.Rpe,
                row.IsWarmup,
                row.RestTimeSeconds,
                DateTime.SpecifyKind(row.UpdatedAt, DateTimeKind.Utc)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ActiveWorkoutSessionSnapshot(
            session.Id,
            session.WorkoutName,
            DateTime.SpecifyKind(session.StartedAt, DateTimeKind.Utc),
            session.CompletedAt is { } completed ? DateTime.SpecifyKind(completed, DateTimeKind.Utc) : null,
            session.TotalDurationSeconds,
            session.TotalVolumeKg,
            session.Notes,
            session.RpeRating,
            session.CompletedAt is not null && !session.IsDeleted,
            session.IsDeleted,
            sets);
    }

    private static Task<int> LockSessionAsync(
        PostgresDbContext context,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({sessionId.ToString("D")}, 0))",
            cancellationToken);

    private static string BuildCommandKey(string prefix, string idempotencyKey)
    {
        var normalized = prefix + idempotencyKey.Trim();
        return normalized.Length <= 128
            ? normalized
            : prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey.Trim()))).ToLowerInvariant();
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
