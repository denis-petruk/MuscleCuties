using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using Npgsql;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;

public sealed class PostgresWorkoutActiveSessionStore(
    PostgresDbContext dbContext,
    PostgresIdempotencyExecutor idempotencyExecutor,
    TimeProvider timeProvider) : IWorkoutActiveSessionStore
{
    private const int CompletedWorkoutPercent = 100;
    public async Task<ActiveWorkoutSessionSnapshot?> GetPlannedSessionAsync(Guid userId, int workoutDayId, CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            var sessions = await dbContext.WorkoutSessionLogs.AsNoTracking()
                .Where(row => row.UserId == userId && !row.IsDeleted && row.CompletedAt == null && row.PlannedWorkoutDayId != null)
                .OrderByDescending(row => row.StartedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
            var session = sessions.FirstOrDefault(row => PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(row.PlannedWorkoutDayId!.Value) == workoutDayId);
            var result = session is null ? null : await LoadSessionSnapshotAsync(dbContext, userId, session.Id, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }).ConfigureAwait(false);
    private const string StartCommandPrefix = "workout.session.start:";
    private const string ResumeTimingCommandPrefix = "workout.session.timing.resume:";
    private const string CheckpointTimingCommandPrefix = "workout.session.timing.checkpoint:";
    private const string UpsertSetCommandPrefix = "workout.session.set:";
    private const string FinishCommandPrefix = "workout.session.finish:";

    public Task<ActiveWorkoutSessionSnapshot?> ResumeTimingAsync(Guid userId, ResumeWorkoutTimingCommand command,
        CancellationToken cancellationToken) => UpdateTimingAsync(userId, command.SessionId,
        BuildCommandKey(ResumeTimingCommandPrefix, command.IdempotencyKey),
        PostgresIdempotencyExecutor.ComputeRequestHash(new { command.SessionId }), null, cancellationToken);

    public Task<ActiveWorkoutSessionSnapshot?> CheckpointTimingAsync(Guid userId, CheckpointWorkoutTimingCommand command,
        CancellationToken cancellationToken) => UpdateTimingAsync(userId, command.SessionId,
        BuildCommandKey(CheckpointTimingCommandPrefix, command.IdempotencyKey),
        PostgresIdempotencyExecutor.ComputeRequestHash(new { command.SessionId, command.IntervalId, command.MeasuredSeconds, command.Pause }),
        command, cancellationToken);

    private async Task<ActiveWorkoutSessionSnapshot?> UpdateTimingAsync(Guid userId, Guid sessionId, string commandKey,
        string requestHash, CheckpointWorkoutTimingCommand? checkpoint, CancellationToken cancellationToken)
    {
        var execution = await idempotencyExecutor.ExecuteAsync<ActiveWorkoutSessionSnapshot?>(dbContext, userId,
            commandKey, requestHash, async (context, ct) =>
            {
                await WorkoutSessionAccountLock.AcquireAsync(context, userId, ct).ConfigureAwait(false);
                await LockSessionAsync(context, sessionId, ct).ConfigureAwait(false);
                var session = await context.WorkoutSessionLogs.SingleOrDefaultAsync(row => row.UserId == userId && row.Id == sessionId, ct)
                    .ConfigureAwait(false);
                if (session is null) return null;
                if (session.IsDeleted || session.CompletedAt is not null || session.PlannedWorkoutDayId is null)
                    throw new InvalidOperationException("An active planned workout is required.");
                var now = timeProvider.GetUtcNow().UtcDateTime;
                if (checkpoint is null)
                {
                    if (await context.WorkoutDays.AsNoTracking().AnyAsync(day => day.UserId == userId &&
                            day.Id == session.PlannedWorkoutDayId && day.IsRemovedBySafety, ct).ConfigureAwait(false))
                        throw new WorkoutActivityNoLongerSafeException();
                    // Discard unacknowledged time from any interrupted interval.
                    session.TimingIntervalId = Guid.CreateVersion7();
                    session.TimingIntervalStartedAtUtc = now;
                    session.TimingIntervalLastConfirmedAtUtc = now;
                    session.TimingIntervalConfirmedSeconds = WorkoutActiveTimeRule.NoSeconds;
                }
                else
                {
                    if (session.TimingIntervalId != checkpoint.IntervalId || session.TimingIntervalStartedAtUtc is not { } started ||
                        session.TimingIntervalLastConfirmedAtUtc is not { } lastConfirmed ||
                        !WorkoutActiveTimeRule.IsValidCheckpoint(checkpoint.MeasuredSeconds, session.TimingIntervalConfirmedSeconds, now - started) ||
                        !WorkoutActiveTimeRule.IsFreshCheckpoint(checkpoint.MeasuredSeconds, session.TimingIntervalConfirmedSeconds, now - lastConfirmed))
                        throw new WorkoutTimingIntervalExpiredException();
                    session.ConfirmedActiveSeconds = checked(session.ConfirmedActiveSeconds + checkpoint.MeasuredSeconds - session.TimingIntervalConfirmedSeconds);
                    session.TimingIntervalConfirmedSeconds = checkpoint.MeasuredSeconds;
                    session.TimingIntervalLastConfirmedAtUtc = now;
                    if (checkpoint.Pause)
                    {
                        session.TimingIntervalId = null;
                        session.TimingIntervalStartedAtUtc = null;
                        session.TimingIntervalLastConfirmedAtUtc = null;
                        session.TimingIntervalConfirmedSeconds = WorkoutActiveTimeRule.NoSeconds;
                    }
                }
                session.UpdatedAt = now;
                await context.SaveChangesAsync(ct).ConfigureAwait(false);
                return await LoadSessionSnapshotAsync(context, userId, sessionId, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        return execution.Replayed && execution.Response is not null ? execution.Response with { Replayed = true } : execution.Response;
    }
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
        // Preserve the pre-classification hash shape for in-flight legacy retries.
        var requestHash = command.UnplannedActivity is null
            ? PostgresIdempotencyExecutor.ComputeRequestHash(new
            {
                command.SessionId, command.WorkoutName, command.StartedAtUtc,
                command.Notes, command.PlannedWorkoutDayId
            })
            : PostgresIdempotencyExecutor.ComputeRequestHash(new
            {
                command.SessionId, command.WorkoutName, command.StartedAtUtc,
                command.Notes, command.PlannedWorkoutDayId, command.UnplannedActivity
            });

        var execution = await idempotencyExecutor.ExecuteAsync(
            dbContext,
            userId,
            commandKey,
            requestHash,
            async (context, ct) =>
            {
                await WorkoutSessionAccountLock.AcquireAsync(context, userId, ct).ConfigureAwait(false);
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

                WorkoutDayRecord? plannedDay = null;
                if (command.UnplannedActivity is { } unplanned &&
                    (!Enum.IsDefined(unplanned) || unplanned == WorkoutActivityDto.RestDay || command.PlannedWorkoutDayId is not null))
                    throw new InvalidOperationException("The unplanned activity is invalid.");
                WorkoutAccountPlanningStateRecord? planningState = null;
                DateOnly? localDate = null;
                string? plannedPrescriptionJson = null;
                if (command.PlannedWorkoutDayId is { } plannedId)
                {
                    var activeDays = await context.WorkoutDays.AsNoTracking().Where(day => day.UserId == userId &&
                        !day.IsRemovedByReadiness && !day.IsRemovedBySafety && !day.IsRemovedByUserAdjustment &&
                        context.WorkoutPlans.Any(plan => plan.UserId == userId && plan.Id == day.WorkoutPlanId && plan.IsActive))
                        .ToListAsync(ct).ConfigureAwait(false);
                    var matches = activeDays.Where(day => PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(day.Id) == plannedId).ToArray();
                    if (matches.Length != 1 || matches[0].WorkoutType == nameof(WorkoutType.Rest))
                        throw new InvalidOperationException("The planned workout is unavailable.");
                    plannedDay = matches[0];
                    planningState = await context.WorkoutAccountPlanningStates.AsNoTracking()
                        .SingleOrDefaultAsync(row => row.UserId == userId, ct).ConfigureAwait(false);
                    if (planningState?.TimeZoneId is not { } zoneId)
                        throw new InvalidOperationException("Establish the account timezone before starting a workout.");
                    localDate = WorkoutAccountCalendarRule.LocalDate(timeProvider.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(zoneId));
                    if (plannedDay.Date != localDate)
                        throw new InvalidOperationException("The planned workout is not scheduled for today.");
                    if (await context.WorkoutSessionLogs.AnyAsync(row => row.UserId == userId && !row.IsDeleted &&
                        row.CompletedAt == null && row.PlannedWorkoutDayId != null, ct).ConfigureAwait(false))
                        throw new InvalidOperationException("An active workout is already in progress.");
                    var plannedExercises = await context.WorkoutDayExercises.AsNoTracking()
                        .Where(row => row.UserId == userId && row.WorkoutDayId == plannedDay.Id && !row.IsRemovedByReadiness)
                        .OrderBy(row => row.Id).ToListAsync(ct).ConfigureAwait(false);
                    plannedPrescriptionJson = new PlannedPrescriptionSnapshot(plannedDay.Id, plannedDay.Name,
                        plannedDay.WorkoutType, plannedDay.ActivityTypeId, plannedDay.PlannedMainDurationMinutes,
                        plannedExercises.Select(PlannedExerciseSnapshot.FromRecord).ToArray()).Serialize();
                }

                session = new WorkoutSessionLogRecord
                {
                    Id = command.SessionId,
                    UserId = userId,
                    WorkoutName = plannedDay?.Name ?? command.WorkoutName,
                    PlannedWorkoutDayId = plannedDay?.Id,
                    UnplannedActivityType = command.UnplannedActivity is { } activity ? (int)activity : null,
                    PlannedTimeZoneId = planningState?.TimeZoneId,
                    PlannedTimeZoneVersion = planningState?.TimeZoneVersion,
                    PlannedLocalDate = localDate,
                    PlannedPrescriptionSnapshotJson = plannedPrescriptionJson,
                    StartedAt = plannedDay is null ? DateTime.SpecifyKind(command.StartedAtUtc, DateTimeKind.Utc) : nowUtc,
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
                await WorkoutSessionAccountLock.AcquireAsync(context, userId, ct).ConfigureAwait(false);
                await LockSessionAsync(context, command.SessionId, ct).ConfigureAwait(false);
                var session = await context.WorkoutSessionLogs.SingleOrDefaultAsync(
                        row => row.UserId == userId && row.Id == command.SessionId,
                        ct)
                    .ConfigureAwait(false);
                if (session is null)
                    return null;
                if (session.IsDeleted)
                    throw new InvalidOperationException("Cannot finish a canceled workout session.");
                if (session.CompletedAt is not null)
                    return await LoadSessionSnapshotAsync(context, userId, command.SessionId, ct).ConfigureAwait(false);

                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                if (session.PlannedWorkoutDayId is { } plannedDayId)
                {
                    var plannedDay = await context.WorkoutDays.AsNoTracking().SingleOrDefaultAsync(
                        row => row.UserId == userId && row.Id == plannedDayId, ct).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("The planned workout is unavailable.");
                    var prescription = PlannedPrescriptionSnapshot.Deserialize(session.PlannedPrescriptionSnapshotJson);
                    if (session.PlannedPrescriptionSnapshotJson is not null &&
                        (prescription is null || prescription.DayId != plannedDayId || prescription.Exercises is null))
                        throw new InvalidOperationException("The active workout prescription is unavailable.");
                    var hasExercises = prescription is not null ? prescription.Exercises.Count > 0 :
                        await context.WorkoutDayExercises.AnyAsync(row => row.UserId == userId &&
                            row.WorkoutDayId == plannedDayId, ct).ConfigureAwait(false);
                    if (session.TimingIntervalId is not null || session.ConfirmedActiveSeconds <= 0 ||
                        command.TotalDurationSeconds != session.ConfirmedActiveSeconds)
                        throw new InvalidOperationException("Complete the activity before finishing the session.");
                    var sessionWorkoutType = prescription?.WorkoutType ?? plannedDay.WorkoutType;
                    if (!hasExercises && sessionWorkoutType != nameof(WorkoutType.Rest))
                        await PostgresWorkoutProgressStore.CreditPlannedSessionAsync(context, userId, session, nowUtc, ct).ConfigureAwait(false);
                    if (hasExercises)
                    {
                        var log = await context.WorkoutLogs.AsNoTracking().Where(row => row.UserId == userId &&
                                row.WorkoutDayId == plannedDayId && row.PlannedSessionId == session.Id &&
                                row.CompletionPercent >= CompletedWorkoutPercent &&
                                row.CreatedAt >= session.StartedAt)
                            .OrderByDescending(row => row.CreatedAt).FirstOrDefaultAsync(ct).ConfigureAwait(false);
                        if (log is null) throw new InvalidOperationException("Complete the planned exercises before finishing the session.");
                        if (plannedDay.WorkoutType != nameof(WorkoutType.Rest))
                            await PostgresWorkoutProgressStore.CreditPlannedSessionAsync(context, userId, session, nowUtc, ct).ConfigureAwait(false);
                    }
                }
                foreach (var set in command.FinalSets ?? [])
                    await ApplySetUpsertAsync(context, userId, session.Id, set, nowUtc, ct).ConfigureAwait(false);

                if (session.PlannedWorkoutDayId is null && session.UnplannedActivityType is not null)
                {
                    if (command.TotalDurationSeconds <= 0)
                        throw new InvalidOperationException("Complete the activity before finishing the session.");
                    var calendar = await context.WorkoutAccountPlanningStates.AsNoTracking()
                        .SingleOrDefaultAsync(row => row.UserId == userId, ct).ConfigureAwait(false);
                    if (calendar?.TimeZoneId is not { } timeZoneId)
                        throw new InvalidOperationException("Establish the account timezone before finishing a workout.");
                    var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                    session.UnplannedCompletionLocalDate = WorkoutAccountCalendarRule.LocalDate(new DateTimeOffset(nowUtc, TimeSpan.Zero), zone);
                    session.UnplannedCompletionTimeZoneId = timeZoneId;
                    session.UnplannedCompletionTimeZoneVersion = calendar.TimeZoneVersion;
                }

                // A planned completion is credited using the accepted server instant. Keep its
                // archive timestamp on that same instant rather than trusting the device clock.
                session.CompletedAt = session.PlannedWorkoutDayId is not null || session.UnplannedActivityType is not null
                    ? nowUtc : DateTime.SpecifyKind(command.CompletedAtUtc, DateTimeKind.Utc);
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

                // Abandoning the session removes it from active work but keeps its logged sets.
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
            sets,
            PlannedWorkoutDayId: session.PlannedWorkoutDayId is { } dayId
                ? PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(dayId) : null)
        {
            ConfirmedActiveSeconds = session.ConfirmedActiveSeconds,
            TimingIntervalId = session.TimingIntervalId,
            TimingIntervalConfirmedSeconds = session.TimingIntervalConfirmedSeconds
        };
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
