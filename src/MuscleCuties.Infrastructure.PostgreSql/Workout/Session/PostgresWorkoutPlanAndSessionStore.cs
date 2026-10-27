using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Session;

public sealed class PostgresWorkoutPlanAndSessionStore(PostgresDbContext dbContext)
{
    private const int OverviewScheduleWeeks = 4;
    private const int CompletedWorkoutPercent = 100;
    private const int DaysPerWeek = 7;
    private const int DefaultCycleLengthDays = 28;
    private const int FallbackMinutesPerExercise = 12;
    private const string PlannedDurationMinuteFormat = "0.#";
    private const string StrengthTag = "STRENGTH";
    private const string CardioTag = "CARDIO";
    private const string RecoveryTag = "RECOVERY";
    private const string RestTag = "REST";

    public static int ToStableWorkoutIntId(Guid id)
    {
        var bytes = id.ToByteArray();
        var raw = BitConverter.ToInt32(bytes, 0) ^
                  BitConverter.ToInt32(bytes, 4) ^
                  BitConverter.ToInt32(bytes, 8) ^
                  BitConverter.ToInt32(bytes, 12);
        var positive = raw & 0x7FFF_FFFF;
        return positive == 0 ? 1 : positive;
    }

    public async Task<WorkoutOverviewResponse> LoadOverviewAsync(
        Guid userId,
        WorkoutOverviewRequest request,
        CancellationToken cancellationToken = default)
    {

        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var phase = await ResolveCyclePhaseAsync(userId, today, cancellationToken).ConfigureAwait(false);
            var activePlan = await dbContext.WorkoutPlans.AsNoTracking()
                .Where(plan => plan.UserId == userId && plan.IsActive)
                .OrderByDescending(plan => plan.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            var workoutDays = activePlan is null
                ? []
                : await dbContext.WorkoutDays.AsNoTracking()
                    .Where(d => d.UserId == userId && d.WorkoutPlanId == activePlan.Id && !d.IsRemovedByReadiness && !d.IsRemovedBySafety)
                    .OrderBy(d => d.WeekIndex)
                    .ThenBy(d => d.DayOfWeek)
                    .ThenBy(d => d.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

            // A revision can retire the plan while a previously started workout is still open.
            // Keep that exact day discoverable so the user can resume or abandon it.
            var inProgressDayId = await dbContext.WorkoutSessionLogs.AsNoTracking()
                .Where(session => session.UserId == userId && !session.IsDeleted && session.CompletedAt == null &&
                    session.PlannedWorkoutDayId != null)
                .OrderByDescending(session => session.StartedAt)
                .Select(session => session.PlannedWorkoutDayId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            var activeScheduledDayIds = workoutDays.Select(day => day.Id).ToHashSet();
            if (inProgressDayId is { } dayId && workoutDays.All(day => day.Id != dayId))
            {
                var inProgressDay = await dbContext.WorkoutDays.AsNoTracking().SingleOrDefaultAsync(
                    day => day.UserId == userId && day.Id == dayId, cancellationToken).ConfigureAwait(false);
                if (inProgressDay is not null) workoutDays.Add(inProgressDay);
            }

            var dayIds = workoutDays.Select(d => d.Id).ToHashSet();
            var dayExercises = dayIds.Count == 0
                ? []
                : await dbContext.WorkoutDayExercises.AsNoTracking()
                    .Where(e => e.UserId == userId && dayIds.Contains(e.WorkoutDayId) && !e.IsRemovedByReadiness)
                    .OrderBy(e => e.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

            var exerciseIds = dayExercises.Select(e => e.ExerciseId).Distinct().ToArray();
            var exercisesById = exerciseIds.Length == 0
                ? new Dictionary<int, Exercise>()
                : await dbContext.Exercises.AsNoTracking()
                    .Where(e => exerciseIds.Contains(e.Id))
                    .ToDictionaryAsync(e => e.Id, cancellationToken)
                    .ConfigureAwait(false);

            var exercisesByDayId = dayExercises
                .GroupBy(e => e.WorkoutDayId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<WorkoutDayExerciseRecord>)g.ToList());

            var safeStart = WorkoutScheduleMathRule.ClampSafeDate(request.StartDate.ToDateTime(TimeOnly.MinValue));
            var startMonday = DateOnly.FromDateTime(WorkoutScheduleMathRule.GetSafeWeekStart(safeStart));
            var endDate = startMonday.AddDays(OverviewScheduleWeeks * DaysPerWeek - 1);

            var workoutLogs = await dbContext.WorkoutLogs.AsNoTracking()
                .Where(l => l.UserId == userId && l.Date >= startMonday.AddDays(-7) && l.Date <= endDate)
                .OrderByDescending(l => l.Date)
                .ThenByDescending(l => l.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            var scheduledDays = workoutDays.Where(day => activeScheduledDayIds.Contains(day.Id)).ToList();
            var workouts = BuildWorkoutListEntries(scheduledDays, exercisesByDayId, exercisesById, workoutLogs);
            var schedule = BuildScheduleEntries(startMonday, today, scheduledDays, exercisesByDayId, exercisesById, workoutLogs);
            var previews = workoutDays.Select(day =>
            {
                var exList = exercisesByDayId.GetValueOrDefault(day.Id, []);
                var names = exList
                    .Select(e => exercisesById.GetValueOrDefault(e.ExerciseId)?.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n!)
                    .ToArray();
                var tags = BuildActivityTags(day, exList, exercisesById)
                    .Where(t => t != RestTag)
                    .Select(tag => new WorkoutActivitySummaryDto(
                        tag,
                        BuildSectionTitle(tag),
                        exList.Count(e => ClassifyExerciseTag(e, day.WorkoutType, exercisesById) == tag)))
                    .ToArray();
                return new WorkoutDayExercisePreview(ToStableWorkoutIntId(day.Id), names, tags);
            }).ToArray();

            var inProgressWorkout = inProgressDayId is { } currentId
                ? BuildWorkoutListEntries(workoutDays.Where(day => day.Id == currentId).ToArray(),
                    exercisesByDayId, exercisesById, workoutLogs)
                    .FirstOrDefault(entry => entry.WorkoutDayId == ToStableWorkoutIntId(currentId))
                : null;
            return new WorkoutOverviewResponse(
                activePlan?.Name,
                phase.ToString(),
                workouts,
                schedule,
                previews) { InProgressWorkout = inProgressWorkout is null ? null : inProgressWorkout with { IsInProgress = true } };
        }).ConfigureAwait(false);

    public async Task RescheduleAsync(
        Guid userId,
        RescheduleWorkoutDayRequest request,
        CancellationToken cancellationToken = default)
    {
        WorkoutScheduleMathRule.EnsureValidReschedule(request.WorkoutDayId, request.TargetDate);
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            await WorkoutSessionAccountLock.RequireNoPreviewAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);

            var activePlan = await dbContext.WorkoutPlans
                .Where(p => p.UserId == userId && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("No active workout plan found.");

            var days = await dbContext.WorkoutDays
                .Where(d => d.UserId == userId && d.WorkoutPlanId == activePlan.Id && !d.IsRemovedByReadiness && !d.IsRemovedBySafety)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var sourceDay = days.FirstOrDefault(d => ToStableWorkoutIntId(d.Id) == request.WorkoutDayId)
                ?? throw new KeyNotFoundException("Workout session was not found or does not belong to the user.");

            var targetDay = days.FirstOrDefault(d => d.Date == request.TargetDate)
                ?? days.FirstOrDefault(d => d.DayOfWeek == (int)request.TargetDate.DayOfWeek)
                ?? throw new InvalidOperationException("Specified schedule dates are not in the active plan.");

            if (sourceDay.Id != targetDay.Id)
            {
                (sourceDay.WorkoutType, targetDay.WorkoutType) = (targetDay.WorkoutType, sourceDay.WorkoutType);
                (sourceDay.Name, targetDay.Name) = (targetDay.Name, sourceDay.Name);

                var exercises = await dbContext.WorkoutDayExercises
                    .Where(e => e.UserId == userId && (e.WorkoutDayId == sourceDay.Id || e.WorkoutDayId == targetDay.Id))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (var exercise in exercises)
                {
                    exercise.WorkoutDayId = exercise.WorkoutDayId == sourceDay.Id
                        ? targetDay.Id
                        : sourceDay.Id;
                }

                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<WorkoutSessionDetailDto> GetDetailAsync(
        Guid userId,
        int workoutDayId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (workoutDayId <= 0)
                throw new ArgumentOutOfRangeException(nameof(workoutDayId));

            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var phase = await ResolveCyclePhaseAsync(userId, today, cancellationToken).ConfigureAwait(false);
            var day = await ResolveWorkoutDayAsync(userId, workoutDayId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Workout session was not found.");

            var stableDayId = ToStableWorkoutIntId(day.Id);
            if (string.Equals(day.WorkoutType, nameof(WorkoutType.Rest), StringComparison.OrdinalIgnoreCase))
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new WorkoutSessionDetailDto(
                    stableDayId,
                    "Living happy life",
                    "Pure rest day",
                    "No exercises today. Log the rest if you actually took it.",
                    true,
                    [],
                    []);
            }

            var activeSession = await dbContext.WorkoutSessionLogs.AsNoTracking().FirstOrDefaultAsync(
                session => session.UserId == userId && session.PlannedWorkoutDayId == day.Id &&
                    !session.IsDeleted && session.CompletedAt == null, cancellationToken).ConfigureAwait(false);
            var prescription = PlannedPrescriptionSnapshot.Deserialize(activeSession?.PlannedPrescriptionSnapshotJson);
            if (activeSession?.PlannedPrescriptionSnapshotJson is not null &&
                (prescription is null || prescription.DayId != day.Id || prescription.Exercises is null))
                throw new InvalidOperationException("The active workout prescription is unavailable.");
            if (prescription is not null)
            {
                day.Name = prescription.Name;
                day.WorkoutType = prescription.WorkoutType;
                day.ActivityTypeId = prescription.ActivityTypeId;
                day.PlannedMainDurationMinutes = prescription.PlannedMainDurationMinutes;
            }
            var orderedExercises = prescription is not null
                ? prescription.Exercises.Select(exercise => exercise.ToRecord(userId, day.Id)).ToList()
                : await dbContext.WorkoutDayExercises.AsNoTracking()
                    .Where(e => e.UserId == userId && e.WorkoutDayId == day.Id && !e.IsRemovedByReadiness)
                    .OrderBy(e => e.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

            var exerciseIds = orderedExercises.Select(e => e.ExerciseId).Distinct().ToArray();
            var exercisesById = exerciseIds.Length == 0
                ? new Dictionary<int, Exercise>()
                : await dbContext.Exercises.AsNoTracking()
                    .Where(e => exerciseIds.Contains(e.Id))
                    .ToDictionaryAsync(e => e.Id, cancellationToken)
                    .ConfigureAwait(false);

            var currentLogs = dbContext.WorkoutLogs.AsNoTracking()
                .Where(l => l.UserId == userId && l.WorkoutDayId == day.Id);
            currentLogs = activeSession is null
                ? currentLogs.Where(log => log.Date == today)
                : currentLogs.Where(log => log.PlannedSessionId == activeSession.Id);
            var currentLog = await currentLogs
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            var currentExerciseLogs = currentLog is null
                ? []
                : await dbContext.WorkoutExerciseLogs.AsNoTracking()
                    .Where(l => l.UserId == userId && l.WorkoutLogId == currentLog.Id)
                    .OrderByDescending(l => l.CreatedAt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

            var currentByDayExerciseId = currentExerciseLogs
                .GroupBy(l => l.WorkoutDayExerciseId)
                .ToDictionary(g => g.Key, g => g.First());

            var previousLogs = exerciseIds.Length == 0
                ? []
                : await dbContext.WorkoutExerciseLogs.AsNoTracking()
                    .Where(l => l.UserId == userId &&
                                exerciseIds.Contains(l.ExerciseId) &&
                                (currentLog == null || l.WorkoutLogId != currentLog.Id))
                    .OrderByDescending(l => l.CreatedAt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

            var latestPreviousByExerciseId = previousLogs
                .GroupBy(l => l.ExerciseId)
                .ToDictionary(g => g.Key, g => g.First());

            var notesByExerciseId = exerciseIds.Length == 0
                ? new Dictionary<int, string>()
                : await dbContext.UserMovementNotes.AsNoTracking()
                    .Where(n => n.UserId == userId && exerciseIds.Contains(n.ExerciseId))
                    .ToDictionaryAsync(n => n.ExerciseId, n => n.Notes, cancellationToken)
                    .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            var exerciseDtos = orderedExercises.Select(dayEx =>
            {
                var catalogEx = exercisesById.GetValueOrDefault(dayEx.ExerciseId);
                var prevLog = latestPreviousByExerciseId.GetValueOrDefault(dayEx.ExerciseId);
                var currLog = currentByDayExerciseId.GetValueOrDefault(dayEx.Id);
                var movementNote = notesByExerciseId.GetValueOrDefault(dayEx.ExerciseId, string.Empty);
                return BuildExerciseDetailDto(dayEx, day.WorkoutType, catalogEx, prevLog, currLog, movementNote);
            }).ToArray();

            var activities = BuildActivityDetails(day, orderedExercises, exercisesById, exerciseDtos);
            return new WorkoutSessionDetailDto(
                stableDayId,
                day.Name,
                BuildWorkoutDaySubtitle(day, orderedExercises, exercisesById),
                BuildWorkoutDaySummary(day, orderedExercises),
                false,
                exerciseDtos,
                activities)
            {
                IsOptional = day.IsOptional,
                IsEasierAlternative = day.IsEasierAlternative
            };
        }).ConfigureAwait(false);

    public async Task SetSessionOptionalAsync(Guid userId, SetWorkoutSessionOptionalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.WorkoutDayId <= 0 || (request.IsEasierAlternative && !request.IsOptional))
            throw new ArgumentOutOfRangeException(nameof(request));
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            await WorkoutSessionAccountLock.RequireNoPreviewAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            var day = await ResolveWorkoutDayAsync(userId, request.WorkoutDayId, cancellationToken).ConfigureAwait(false)
                      ?? throw new KeyNotFoundException("Workout session was not found.");
            var hasLog = await dbContext.WorkoutLogs.AnyAsync(l => l.UserId == userId && l.WorkoutDayId == day.Id, cancellationToken)
                .ConfigureAwait(false);
            if (hasLog)
                throw new InvalidOperationException("A logged session cannot be marked optional.");
            var tracked = await dbContext.WorkoutDays.SingleAsync(d => d.Id == day.Id && d.UserId == userId, cancellationToken).ConfigureAwait(false);
            tracked.IsOptional = request.IsOptional;
            tracked.IsEasierAlternative = request.IsEasierAlternative;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task SetExerciseOptionalAsync(Guid userId, SetWorkoutExerciseOptionalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.WorkoutDayExerciseId <= 0 || request.OptionalSets < 0)
            throw new ArgumentOutOfRangeException(nameof(request));
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            await WorkoutSessionAccountLock.RequireNoPreviewAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            var ex = await ResolveWorkoutDayExerciseAsync(userId, request.WorkoutDayExerciseId, cancellationToken, track: true)
                     ?? throw new KeyNotFoundException("Workout exercise was not found.");
            var day = await dbContext.WorkoutDays.SingleAsync(d => d.Id == ex.WorkoutDayId && d.UserId == userId, cancellationToken).ConfigureAwait(false);
            if (request.OptionalSets >= ex.Sets && request.OptionalSets != 0)
                throw new ArgumentOutOfRangeException(nameof(request.OptionalSets), "At least one planned set must remain required.");
            var logged = await dbContext.WorkoutExerciseLogs.AnyAsync(l => l.UserId == userId && l.WorkoutDayExerciseId == ex.Id, cancellationToken)
                .ConfigureAwait(false);
            if (logged) throw new InvalidOperationException("A logged exercise cannot be marked optional.");
            ex.IsOptional = request.IsOptional;
            ex.OptionalSets = request.IsOptional ? request.OptionalSets : 0;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task LogAsync(
        Guid userId,
        LogWorkoutSessionRequest request,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.WorkoutDayId <= 0)
                throw new ArgumentOutOfRangeException(nameof(request.WorkoutDayId));

            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);

            WorkoutDayRecord? day;
            PlannedPrescriptionSnapshot? sessionPrescription = null;
            if (request.PlannedSessionId is { } plannedSessionId)
            {
                var plannedSession = await dbContext.WorkoutSessionLogs.AsNoTracking()
                    .Where(session => session.UserId == userId && session.Id == plannedSessionId &&
                        !session.IsDeleted && session.CompletedAt == null)
                    .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                var plannedDayId = plannedSession?.PlannedWorkoutDayId;
                day = plannedDayId is { } id ? await dbContext.WorkoutDays.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken).ConfigureAwait(false) : null;
                if (day is null || ToStableWorkoutIntId(day.Id) != request.WorkoutDayId)
                    throw new InvalidOperationException("The planned workout session is unavailable.");
                sessionPrescription = PlannedPrescriptionSnapshot.Deserialize(plannedSession?.PlannedPrescriptionSnapshotJson);
                if (plannedSession?.PlannedPrescriptionSnapshotJson is not null &&
                    (sessionPrescription is null || sessionPrescription.DayId != day.Id || sessionPrescription.Exercises is null))
                    throw new InvalidOperationException("The active workout prescription is unavailable.");
            }
            else day = await ResolveWorkoutDayAsync(userId, request.WorkoutDayId, cancellationToken).ConfigureAwait(false);
            if (day is null) throw new KeyNotFoundException("Workout session was not found.");

            var nowUtc = DateTime.UtcNow;
            if (string.Equals(day.WorkoutType, nameof(WorkoutType.Rest), StringComparison.OrdinalIgnoreCase))
            {
                var existingRestLog = await dbContext.WorkoutLogs
                    .Where(l => l.UserId == userId && l.WorkoutDayId == day.Id && l.Date == request.Date)
                    .OrderByDescending(l => l.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (existingRestLog is null)
                {
                    dbContext.WorkoutLogs.Add(new WorkoutLogRecord
                    {
                        Id = Guid.CreateVersion7(),
                        UserId = userId,
                        WorkoutDayId = day.Id,
                        Date = request.Date,
                        CompletionPercent = 100,
                        CreatedAt = nowUtc
                    });
                }
                else
                {
                    existingRestLog.CompletionPercent = 100;
                    existingRestLog.CreatedAt = nowUtc;
                }

                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var dayExercises = sessionPrescription is not null
                ? sessionPrescription.Exercises.Select(exercise => exercise.ToRecord(userId, day.Id)).ToList()
                : await dbContext.WorkoutDayExercises.AsNoTracking()
                    .Where(e => e.UserId == userId && e.WorkoutDayId == day.Id)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

            var byStableId = dayExercises.ToDictionary(e => ToStableWorkoutIntId(e.Id));
            var byExerciseId = dayExercises.GroupBy(e => e.ExerciseId).ToDictionary(g => g.Key, g => g.First());

            var validInputs = new List<(WorkoutDayExerciseRecord DayExercise, WorkoutExerciseLogDto Input)>();
            foreach (var input in request.Exercises ?? [])
            {
                if (byStableId.TryGetValue(input.WorkoutDayExerciseId, out var matched) ||
                    byExerciseId.TryGetValue(input.ExerciseId, out matched))
                {
                    validInputs.RemoveAll(x => x.DayExercise.Id == matched.Id);
                    validInputs.Add((matched, input));
                }
            }

            if (validInputs.Count == 0)
                throw new InvalidOperationException("Log at least one exercise before saving this workout.");

            var exerciseIds = dayExercises.Select(e => e.ExerciseId).Distinct().ToArray();
            var catalogExercises = await dbContext.Exercises.AsNoTracking()
                .Where(e => exerciseIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, cancellationToken)
                .ConfigureAwait(false);

            foreach (var (dayExercise, input) in validInputs)
            {
                var catalogEx = catalogExercises.GetValueOrDefault(dayExercise.ExerciseId);
                var usesEndurance = UsesEnduranceMetrics(dayExercise, catalogEx?.Name ?? string.Empty);
                if (!usesEndurance)
                {
                    var name = catalogEx?.Name ?? "this exercise";
                    if (input.CompletedSets <= 0 || input.CompletedReps <= 0)
                        throw new InvalidOperationException($"Add sets and reps for {name} before logging it.");
                    if (input.WeightKg is null or < 0f)
                        throw new InvalidOperationException($"Add kg for {name} before logging it. Use 0 kg for bodyweight.");
                }
            }

            var primaryLog = await dbContext.WorkoutLogs
                .Where(l => l.UserId == userId && l.WorkoutDayId == day.Id && l.Date == request.Date &&
                            l.PlannedSessionId == request.PlannedSessionId)
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (primaryLog is null)
            {
                primaryLog = new WorkoutLogRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    WorkoutDayId = day.Id,
                    PlannedSessionId = request.PlannedSessionId,
                    Date = request.Date,
                    CompletionPercent = 0,
                    CreatedAt = nowUtc
                };
                dbContext.WorkoutLogs.Add(primaryLog);
            }
            else
            {
                primaryLog.CreatedAt = nowUtc;
            }

            var existingExerciseLogs = await dbContext.WorkoutExerciseLogs
                .Where(l => l.UserId == userId && l.WorkoutLogId == primaryLog.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var (dayExercise, input) in validInputs)
            {
                var existing = existingExerciseLogs.FirstOrDefault(e => e.WorkoutDayExerciseId == dayExercise.Id);
                if (existing is null)
                {
                    existing = new WorkoutExerciseLogRecord
                    {
                        Id = Guid.CreateVersion7(),
                        UserId = userId,
                        WorkoutLogId = primaryLog.Id,
                        WorkoutDayExerciseId = dayExercise.Id,
                        ExerciseId = dayExercise.ExerciseId,
                        CreatedAt = nowUtc
                    };
                    dbContext.WorkoutExerciseLogs.Add(existing);
                    existingExerciseLogs.Add(existing);
                }

                existing.ExerciseId = dayExercise.ExerciseId;
                existing.CompletedSets = Math.Max(0, input.CompletedSets);
                existing.CompletedReps = Math.Max(0, input.CompletedReps);
                existing.WeightKg = input.WeightKg is >= 0f ? (decimal)Math.Round(input.WeightKg.Value, 2) : null;
                existing.CompletedDurationSeconds = input.CompletedDurationSeconds is >= 0 ? input.CompletedDurationSeconds : null;
                existing.DistanceKm = input.DistanceKm is >= 0f ? (decimal)Math.Round(input.DistanceKm.Value, 2) : null;
                existing.AverageHeartRateBpm = input.AverageHeartRateBpm is >= 0 ? input.AverageHeartRateBpm : null;
                existing.PaceSecondsPerKm = input.PaceSecondsPerKm is >= 0 ? input.PaceSecondsPerKm : null;
                existing.PowerWatts = input.PowerWatts is >= 0 ? input.PowerWatts : null;
                existing.CadenceRpm = input.CadenceRpm is >= 0 ? input.CadenceRpm : null;
                existing.EffortRating = input.EffortRating is >= 1 and <= 10 ? input.EffortRating : null;
                existing.CreatedAt = nowUtc;
            }

            var completedCount = existingExerciseLogs.Count(log =>
            {
                var dayEx = dayExercises.FirstOrDefault(d => d.Id == log.WorkoutDayExerciseId);
                if (dayEx is null) return false;
                var catEx = catalogExercises.GetValueOrDefault(dayEx.ExerciseId);
                return IsExerciseLogComplete(log, dayEx, catEx?.Name ?? string.Empty);
            });
            primaryLog.CompletionPercent = dayExercises.Count == 0
                ? 100
                : Math.Clamp((int)Math.Round(completedCount * 100.0 / dayExercises.Count), 0, 100);

            // Mirror into WorkoutSessionLogRecord + ExerciseSetLogRecord for Workout Archive & Dashboard streaks
            await UpsertArchiveSessionLogAsync(
                userId,
                day,
                request.Date,
                nowUtc,
                primaryLog.CompletionPercent >= CompletedWorkoutPercent,
                existingExerciseLogs,
                catalogExercises,
                request.PlannedSessionId,
                cancellationToken).ConfigureAwait(false);

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

    public async Task<IReadOnlyList<WorkoutSwapCandidateDto>> GetSwapCandidatesAsync(
        Guid userId,
        int workoutDayExerciseId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync<IReadOnlyList<WorkoutSwapCandidateDto>>(async () =>
        {
            if (workoutDayExerciseId <= 0)
                return [];

            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var dayExercise = await ResolveWorkoutDayExerciseAsync(userId, workoutDayExerciseId, cancellationToken)
                .ConfigureAwait(false);
            if (dayExercise is null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return [];
            }

            var catalogExercise = await dbContext.Exercises.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == dayExercise.ExerciseId, cancellationToken)
                .ConfigureAwait(false);
            var currentPlanningId = ExtractPlanningId(catalogExercise?.Code);
            if (currentPlanningId is null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return [];
            }

            var allDefinitions = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
                .Include(d => d.MuscleContributions)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var currentDef = allDefinitions.FirstOrDefault(d => d.Id == currentPlanningId.Value);
            if (currentDef is null || currentDef.MuscleContributions.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return [];
            }

            var primaryMuscleId = currentDef.MuscleContributions
                .OrderByDescending(c => c.Fraction)
                .First()
                .MuscleGroupId;

            var (equipment, injuryFlags) = await LoadSwapEligibilityAsync(userId, cancellationToken)
                .ConfigureAwait(false);

            var muscleGroups = await dbContext.WorkoutMuscleGroups.AsNoTracking()
                .ToDictionaryAsync(mg => mg.Id, mg => mg.Name, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return allDefinitions
                .Where(e => e.Id != currentPlanningId.Value)
                .Where(e => e.IsBodyweight || (e.Required & equipment) == e.Required)
                .Where(e => (e.Contraindications & injuryFlags) == 0)
                .Select(e =>
                {
                    var matchScore = ComputeMuscleMatchScore(
                        currentDef.MuscleContributions,
                        e.MuscleContributions,
                        primaryMuscleId);
                    return (Exercise: e, MatchScore: matchScore);
                })
                .Where(x => x.MatchScore > 0)
                .OrderByDescending(x => x.MatchScore)
                .Take(8)
                .Select(x => new WorkoutSwapCandidateDto(
                    x.Exercise.Id,
                    x.Exercise.Name,
                    $"{(int)(x.MatchScore * 100)}% muscle match",
                    x.Exercise.IsBodyweight ? "Bodyweight" : FormatEquipmentName(x.Exercise.Required),
                    string.Join(", ", x.Exercise.MuscleContributions
                        .Where(c => c.Fraction >= 0.3m)
                        .OrderByDescending(c => c.Fraction)
                        .Take(3)
                        .Select(c => muscleGroups.GetValueOrDefault(c.MuscleGroupId, string.Empty))
                        .Where(n => !string.IsNullOrWhiteSpace(n)))))
                .ToArray();
        }).ConfigureAwait(false);

    public async Task SwapAsync(
        Guid userId,
        SwapWorkoutExerciseRequest request,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.WorkoutDayExerciseId <= 0 || request.NewExerciseId <= 0)
                throw new ArgumentOutOfRangeException(nameof(request));

            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            await WorkoutSessionAccountLock.RequireNoPreviewAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);

            var dayExercise = await ResolveWorkoutDayExerciseAsync(userId, request.WorkoutDayExerciseId, cancellationToken, track: true)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Workout exercise was not found.");

            var currentCatalogExercise = await dbContext.Exercises.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == dayExercise.ExerciseId, cancellationToken)
                .ConfigureAwait(false);
            var originalPlanningId = ExtractPlanningId(currentCatalogExercise?.Code);

            var newExerciseDef = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == request.NewExerciseId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Exercise definition {request.NewExerciseId} was not found.");

            var (equipment, injuryFlags) = await LoadSwapEligibilityAsync(userId, cancellationToken)
                .ConfigureAwait(false);

            if ((newExerciseDef.Contraindications & injuryFlags) != 0)
                throw new InvalidOperationException($"Exercise '{newExerciseDef.Name}' is contraindicated by active injuries.");

            if (!newExerciseDef.IsBodyweight && (newExerciseDef.Required & equipment) != newExerciseDef.Required)
                throw new InvalidOperationException($"Exercise '{newExerciseDef.Name}' requires equipment not available in current profile.");

            var targetCode = $"ENGINE_{request.NewExerciseId}";
            var catalogExercise = await dbContext.Exercises.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Code == targetCode || e.Id == request.NewExerciseId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Catalog exercise for planning ID {request.NewExerciseId} was not found.");

            dayExercise.ExerciseId = catalogExercise.Id;

            if (request.SavePreference && originalPlanningId is not null)
            {
                var existingPref = await dbContext.UserExercisePreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.OriginalExerciseId == originalPlanningId.Value, cancellationToken)
                    .ConfigureAwait(false);

                if (existingPref is not null)
                {
                    existingPref.PreferredExerciseId = request.NewExerciseId;
                    existingPref.CreatedAt = DateTime.UtcNow;
                }
                else
                {
                    dbContext.UserExercisePreferences.Add(new UserExercisePreferenceRecord
                    {
                        Id = Guid.CreateVersion7(),
                        UserId = userId,
                        OriginalExerciseId = originalPlanningId.Value,
                        PreferredExerciseId = request.NewExerciseId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

    public async Task<WorkoutMovementNoteDto> GetMovementNoteAsync(
        Guid userId,
        int workoutDayExerciseId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (workoutDayExerciseId <= 0)
                throw new ArgumentOutOfRangeException(nameof(workoutDayExerciseId));

            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var dayExercise = await ResolveWorkoutDayExerciseAsync(userId, workoutDayExerciseId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Workout exercise was not found.");

            var note = await dbContext.UserMovementNotes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.UserId == userId && n.ExerciseId == dayExercise.ExerciseId, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new WorkoutMovementNoteDto(workoutDayExerciseId, note?.Notes ?? string.Empty);
        }).ConfigureAwait(false);

    public async Task SaveMovementNoteAsync(
        Guid userId,
        SaveWorkoutMovementNoteRequest request,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.WorkoutDayExerciseId <= 0)
                throw new ArgumentOutOfRangeException(nameof(request.WorkoutDayExerciseId));

            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var dayExercise = await ResolveWorkoutDayExerciseAsync(userId, request.WorkoutDayExerciseId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Workout exercise was not found.");

            var existing = await dbContext.UserMovementNotes
                .FirstOrDefaultAsync(n => n.UserId == userId && n.ExerciseId == dayExercise.ExerciseId, cancellationToken)
                .ConfigureAwait(false);

            var trimmed = request.Notes?.Trim() ?? string.Empty;
            var nowUtc = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (existing is not null)
                    dbContext.UserMovementNotes.Remove(existing);
            }
            else if (existing is not null)
            {
                existing.Notes = trimmed;
                existing.UpdatedAt = nowUtc;
            }
            else
            {
                var catalogExercise = await dbContext.Exercises.AsNoTracking()
                    .SingleOrDefaultAsync(e => e.Id == dayExercise.ExerciseId, cancellationToken)
                    .ConfigureAwait(false);
                dbContext.UserMovementNotes.Add(new UserMovementNoteRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    ExerciseId = dayExercise.ExerciseId,
                    ExerciseName = catalogExercise?.Name ?? "Exercise",
                    Notes = trimmed,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

    private async Task<WorkoutDayRecord?> ResolveWorkoutDayAsync(
        Guid userId,
        int workoutDayId,
        CancellationToken cancellationToken)
    {
        var allDays = await dbContext.WorkoutDays.AsNoTracking()
            .Where(d => d.UserId == userId && ((!d.IsRemovedByReadiness && !d.IsRemovedBySafety) ||
                dbContext.WorkoutSessionLogs.Any(session => session.UserId == userId &&
                    session.PlannedWorkoutDayId == d.Id && !session.IsDeleted && session.CompletedAt == null)))
            .OrderByDescending(d => d.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var exact = allDays.FirstOrDefault(d => ToStableWorkoutIntId(d.Id) == workoutDayId);
        if (exact is not null)
            return exact;

        if (workoutDayId is >= 0 and <= 6)
        {
            var activePlan = await dbContext.WorkoutPlans.AsNoTracking()
                .Where(p => p.UserId == userId && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (activePlan is not null)
            {
                return allDays.FirstOrDefault(d =>
                    d.WorkoutPlanId == activePlan.Id &&
                    d.DayOfWeek == workoutDayId &&
                    !string.Equals(d.WorkoutType, nameof(WorkoutType.Rest), StringComparison.OrdinalIgnoreCase))
                    ?? allDays.FirstOrDefault(d => d.WorkoutPlanId == activePlan.Id && d.DayOfWeek == workoutDayId);
            }
        }

        return null;
    }

    private async Task<WorkoutDayExerciseRecord?> ResolveWorkoutDayExerciseAsync(
        Guid userId,
        int workoutDayExerciseId,
        CancellationToken cancellationToken,
        bool track = false)
    {
        var query = track
            ? dbContext.WorkoutDayExercises.Where(e => e.UserId == userId)
            : dbContext.WorkoutDayExercises.AsNoTracking().Where(e => e.UserId == userId);

        var rows = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.FirstOrDefault(e => ToStableWorkoutIntId(e.Id) == workoutDayExerciseId);
    }

    private async Task<CyclePhase> ResolveCyclePhaseAsync(
        Guid userId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles.AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        var cycles = await dbContext.CycleLogs.AsNoTracking()
            .Where(c => c.UserId == userId && c.StartDate <= today)
            .OrderBy(c => c.StartDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var history = cycles.Select(c => new CycleObservation(
            c.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            c.EndDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            c.CycleLength)).ToArray();
        var latest = cycles.LastOrDefault();
        var active = latest is not null && (latest.EndDate is null || latest.EndDate > today)
            ? new CycleObservation(
                latest.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                latest.EndDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                latest.CycleLength)
            : null;
        var cycleLength = profile?.CycleLength > 0 ? profile.CycleLength : DefaultCycleLengthDays;
        return CyclePredictionDomainRule.CreatePrediction(
            active,
            history,
            cycleLength,
            today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified)).CurrentPhase;
    }

    private async Task UpsertArchiveSessionLogAsync(
        Guid userId,
        WorkoutDayRecord day,
        DateOnly date,
        DateTime nowUtc,
        bool isCompleted,
        IReadOnlyList<WorkoutExerciseLogRecord> exerciseLogs,
        IReadOnlyDictionary<int, Exercise> catalogExercises,
        Guid? plannedSessionId,
        CancellationToken cancellationToken)
    {
        var dayStartUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEndUtc = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var session = plannedSessionId is { } sessionId
            ? await dbContext.WorkoutSessionLogs.SingleAsync(s => s.UserId == userId && s.Id == sessionId &&
                s.PlannedWorkoutDayId == day.Id && !s.IsDeleted, cancellationToken).ConfigureAwait(false)
            : await dbContext.WorkoutSessionLogs
            .Where(s => s.UserId == userId && !s.IsDeleted && s.PlannedWorkoutDayId == null && s.WorkoutName == day.Name &&
                        s.StartedAt >= dayStartUtc && s.StartedAt <= dayEndUtc)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var durationSeconds = exerciseLogs.Sum(e => e.CompletedDurationSeconds ?? 0);
        if (durationSeconds <= 0)
            durationSeconds = Math.Max(1800, exerciseLogs.Count * 600);

        if (session is null)
        {
            session = new WorkoutSessionLogRecord
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                WorkoutName = day.Name,
                StartedAt = dayStartUtc.AddHours(9),
                CompletedAt = isCompleted ? nowUtc : null,
                TotalDurationSeconds = durationSeconds,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };
            dbContext.WorkoutSessionLogs.Add(session);
        }
        else
        {
            if (isCompleted && plannedSessionId is null)
                session.CompletedAt ??= nowUtc;
            if (plannedSessionId is null) session.TotalDurationSeconds = durationSeconds;
            session.UpdatedAt = nowUtc;
        }

        var existingSets = await dbContext.ExerciseSetLogs
            .Where(s => s.UserId == userId && s.WorkoutSessionLogId == session.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existingSets.Count > 0)
            dbContext.ExerciseSetLogs.RemoveRange(existingSets);

        decimal totalVolume = 0m;
        foreach (var exLog in exerciseLogs)
        {
            var exName = catalogExercises.GetValueOrDefault(exLog.ExerciseId)?.Name ?? "Exercise";
            var setsCount = Math.Max(1, exLog.CompletedSets);
            var reps = Math.Max(0, exLog.CompletedReps);
            var weight = exLog.WeightKg ?? 0m;

            for (var setNum = 1; setNum <= setsCount; setNum++)
            {
                totalVolume += weight * reps;
                dbContext.ExerciseSetLogs.Add(new ExerciseSetLogRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    WorkoutSessionLogId = session.Id,
                    ExerciseId = exLog.ExerciseId.ToString(CultureInfo.InvariantCulture),
                    ExerciseName = exName,
                    SetNumber = setNum,
                    WeightKg = weight,
                    RepsCompleted = reps,
                    Rpe = exLog.EffortRating,
                    IsWarmup = false,
                    RestTimeSeconds = 90,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                });
            }
        }

        session.TotalVolumeKg = Math.Round(totalVolume, 1);
    }

    private async Task<(EquipmentSet Equipment, InjuryFlag InjuryFlags)> LoadSwapEligibilityAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles.AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        var equipmentEnum = profile is not null && Enum.TryParse<Equipment>(profile.EquipmentLevel, true, out var eq)
            ? eq
            : Equipment.FullGym;
        var equipmentSet = ExercisePickerRule.MapEquipment(equipmentEnum);

        var activeInjuries = await dbContext.WorkoutInjuryLogs.AsNoTracking()
            .Where(i => i.UserId == userId && i.Status != "Cleared")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var flags = InjuryFlag.None;
        foreach (var injury in activeInjuries)
            flags |= (InjuryFlag)injury.SiteFlag;

        return (equipmentSet, flags);
    }

    private static IReadOnlyList<WorkoutListEntry> BuildWorkoutListEntries(
        IReadOnlyList<WorkoutDayRecord> workoutDays,
        IReadOnlyDictionary<Guid, IReadOnlyList<WorkoutDayExerciseRecord>> exercisesByDayId,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        IReadOnlyList<WorkoutLogRecord> workoutLogs)
    {
        var daysByWeekday = workoutDays
            .GroupBy(d => d.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.ToList());
        var latestLogByDayId = workoutLogs
            .GroupBy(l => l.WorkoutDayId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.Date).ThenByDescending(l => l.CreatedAt).First());

        var entries = new List<WorkoutListEntry>();
        foreach (var dayOfWeek in Enumerable.Range(0, DaysPerWeek))
        {
            if (!daysByWeekday.TryGetValue(dayOfWeek, out var scheduledDays) || scheduledDays.Count == 0)
            {
                entries.Add(new WorkoutListEntry(
                    0,
                    RestTag,
                    WorkoutScheduleMathRule.FormatDayLabel((DayOfWeek)dayOfWeek),
                    "Living happy life",
                    "Rest day",
                    "Rest, recover, enjoy the day.",
                    true,
                    false,
                    0,
                    0));
                continue;
            }

            var activeDays = scheduledDays
                .Where(d => !string.Equals(d.WorkoutType, nameof(WorkoutType.Rest), StringComparison.OrdinalIgnoreCase))
                .ToList();
            var visibleDays = activeDays.Count > 0 ? activeDays : [scheduledDays[0]];
            foreach (var day in visibleDays)
            {
                var latestLog = latestLogByDayId.GetValueOrDefault(day.Id);
                var stableId = ToStableWorkoutIntId(day.Id);
                if (string.Equals(day.WorkoutType, nameof(WorkoutType.Rest), StringComparison.OrdinalIgnoreCase))
                {
                    entries.Add(new WorkoutListEntry(
                        stableId,
                        RestTag,
                        WorkoutScheduleMathRule.FormatDayLabel((DayOfWeek)day.DayOfWeek),
                        "Living happy life",
                        "Rest day",
                        "Rest, recover, enjoy the day.",
                        true,
                        latestLog?.CompletionPercent >= 100,
                        0,
                        0));
                    continue;
                }

                var exList = exercisesByDayId.GetValueOrDefault(day.Id, []);
                var primaryTag = BuildPrimaryTag(day, exList, exercisesById);
                var activityCount = BuildActivityTags(day, exList, exercisesById).Count(t => t != RestTag);
                entries.Add(new WorkoutListEntry(
                    stableId,
                    primaryTag,
                    WorkoutScheduleMathRule.FormatDayLabel((DayOfWeek)day.DayOfWeek),
                    day.Name,
                    BuildDurationText(day, exList),
                    BuildDetailsText(day, exList, exercisesById),
                    false,
                    latestLog?.CompletionPercent >= 100,
                    exList.Count,
                    activityCount) { IsOptional = day.IsOptional, IsEasierAlternative = day.IsEasierAlternative });
            }
        }

        return entries;
    }

    private static IReadOnlyList<WorkoutScheduleEntry> BuildScheduleEntries(
        DateOnly startMonday,
        DateOnly today,
        IReadOnlyList<WorkoutDayRecord> workoutDays,
        IReadOnlyDictionary<Guid, IReadOnlyList<WorkoutDayExerciseRecord>> exercisesByDayId,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        IReadOnlyList<WorkoutLogRecord> workoutLogs)
    {
        var totalDays = OverviewScheduleWeeks * DaysPerWeek;
        var logsByDate = workoutLogs
            .GroupBy(l => l.Date)
            .ToDictionary(g => g.Key, g => g.ToList());
        var result = new List<WorkoutScheduleEntry>(totalDays);

        for (var i = 0; i < totalDays; i++)
        {
            var date = startMonday.AddDays(i);
            var weekIndex = i / DaysPerWeek;
            var dayOfWeek = (int)date.DayOfWeek;
            var matchingDays = MatchWorkoutDays(workoutDays, date, weekIndex, dayOfWeek);
            var dayLogs = logsByDate.GetValueOrDefault(date);
            var dayLabel = WorkoutScheduleMathRule.FormatDayLabel(date.DayOfWeek);
            var activeDays = matchingDays
                .Where(day => !string.Equals(day.WorkoutType, nameof(WorkoutType.Rest), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(day => string.Equals(day.WorkoutType, nameof(WorkoutType.Strength),
                    StringComparison.OrdinalIgnoreCase))
                .ThenBy(day => day.Id)
                .ToArray();

            if (activeDays.Length == 0)
            {
                var restDay = matchingDays.FirstOrDefault();
                var isRestCompleted = dayLogs is not null && dayLogs.Any(log => log.CompletionPercent >= 100);
                result.Add(new WorkoutScheduleEntry(
                    date,
                    weekIndex,
                    dayLabel,
                    RestTag,
                    restDay?.Name ?? "Living happy life",
                    restDay is not null ? ToStableWorkoutIntId(restDay.Id) : 0,
                    date == today,
                    isRestCompleted,
                    true));
                continue;
            }

            var primaryDay = activeDays[0];
            var exList = exercisesByDayId.GetValueOrDefault(primaryDay.Id, []);
            var tag = BuildPrimaryTag(primaryDay, exList, exercisesById);
            var completedDayIds = dayLogs is null
                ? new HashSet<Guid>()
                : dayLogs.Where(log => log.CompletionPercent >= 100)
                    .Select(log => log.WorkoutDayId)
                    .ToHashSet();
            var isCompleted = activeDays.All(day => completedDayIds.Contains(day.Id));
            var title = string.Join(" + ", activeDays.Select(day => day.Name)
                .Distinct(StringComparer.Ordinal));
            result.Add(new WorkoutScheduleEntry(
                date,
                weekIndex,
                dayLabel,
                tag,
                title,
                ToStableWorkoutIntId(primaryDay.Id),
                date == today,
                isCompleted,
                false)
            {
                IsOptional = activeDays.All(day => day.IsOptional),
                IsEasierAlternative = activeDays.All(day => day.IsEasierAlternative),
                CompanionWorkoutDayId = activeDays.Skip(1).FirstOrDefault() is { } companionDay
                    ? ToStableWorkoutIntId(companionDay.Id)
                    : null
            });
        }

        return result;
    }

    private static IReadOnlyList<WorkoutDayRecord> MatchWorkoutDays(
        IReadOnlyList<WorkoutDayRecord> workoutDays,
        DateOnly date,
        int weekIndex,
        int dayOfWeek)
    {
        var exactMatches = workoutDays.Where(day =>
                day.Date == date || (day.WeekIndex == weekIndex && day.DayOfWeek == dayOfWeek))
            .ToArray();
        if (exactMatches.Length > 0)
            return exactMatches;

        return workoutDays.Where(day => day.DayOfWeek == dayOfWeek).ToArray();
    }

    private static WorkoutExerciseDetailDto BuildExerciseDetailDto(
        WorkoutDayExerciseRecord dayExercise,
        string workoutType,
        Exercise? exercise,
        WorkoutExerciseLogRecord? previousLog,
        WorkoutExerciseLogRecord? currentLog,
        string movementNotes)
    {
        var name = exercise?.Name ?? "Exercise";
        var usesEndurance = UsesEnduranceMetrics(dayExercise, name);
        var isTimeUnderTension = ContainsAny(name, "plank", "copenhagen");
        var isCycling = ContainsAny(name, "bike", "cycle", "cycling", "ride");
        var isRunning = ContainsAny(name, "run", "jog", "sprint", "tempo");
        var isSwimming = ContainsAny(name, "swim", "pool");
        var isHiit = ContainsAny(name, "hiit", "interval") && !isCycling && !isRunning;
        var isDistanceCardio = isCycling || isRunning || isSwimming || ContainsAny(name, "walk");

        var activityTag = ClassifyExerciseTag(dayExercise, workoutType, exercise);
        var isLogged = IsExerciseLogComplete(currentLog, dayExercise, name);

        var setsCount = currentLog?.CompletedSets ?? (dayExercise.Sets > 0 ? dayExercise.Sets : 3);
        var repsCount = currentLog?.CompletedReps ?? (dayExercise.Reps > 0 ? dayExercise.Reps : 10);
        var weightVal = currentLog?.WeightKg is { } cw ? (double)cw : (previousLog?.WeightKg is { } pw ? (double)pw : 0.0);

        var sets = usesEndurance
            ? Array.Empty<WorkoutSetDetailDto>()
            : Enumerable.Range(1, Math.Max(1, setsCount))
                .Select(setNum => new WorkoutSetDetailDto(setNum, weightVal, repsCount, null, false, 90))
                .ToArray();

        var targetText = isTimeUnderTension
            ? (dayExercise.DurationSeconds is > 0 ? $"{dayExercise.DurationSeconds.Value / 60}m under tension" : "Time under tension")
            : usesEndurance
                ? (dayExercise.DurationSeconds is > 0 ? $"{dayExercise.DurationSeconds.Value / 60}m steady" : "Steady effort")
                : (dayExercise.Sets > 0 && dayExercise.Reps > 0 ? $"{dayExercise.Sets} sets x {dayExercise.Reps} reps" : "Flexible");

        var previousText = previousLog is null
            ? "No previous log"
            : usesEndurance
                ? "Logged before"
                : $"Last {(previousLog.WeightKg is > 0m ? $"{previousLog.WeightKg.Value:0.#} kg" : "Bodyweight")} with {previousLog.CompletedSets} x {previousLog.CompletedReps}";

        var recommendationText = usesEndurance
            ? "Own the full time with clean, controlled form."
            : previousLog?.WeightKg is > 0m
                ? $"Repeat {previousLog.WeightKg.Value:0.#} kg"
                : "Pick a steady starting weight.";

        return new WorkoutExerciseDetailDto(
            ToStableWorkoutIntId(dayExercise.Id),
            dayExercise.ExerciseId,
            name,
            activityTag,
            isLogged,
            sets)
        {
            PlannedSets = dayExercise.Sets,
            OptionalSets = dayExercise.OptionalSets,
            IsOptional = dayExercise.IsOptional,
            ActivityTitle = BuildSectionTitle(activityTag),
            Description = string.IsNullOrWhiteSpace(exercise?.Description)
                ? "Supports today's workout focus with clear, trackable work."
                : exercise.Description,
            ImageUrl = exercise?.ImageUrl ?? string.Empty,
            VideoUrl = exercise?.VideoUrl ?? string.Empty,
            TechniqueNotes = exercise?.TechniqueNotes ?? "Move with controlled tempo and steady breathing.",
            QuickTipsText = "Brace your core and keep each rep controlled.",
            TargetText = targetText,
            PreviousText = previousText,
            RecommendationText = recommendationText,
            MovementNotes = movementNotes,
            LoggedSetsText = currentLog is not null ? currentLog.CompletedSets.ToString(CultureInfo.InvariantCulture) :
                dayExercise.Sets > 0 ? dayExercise.Sets.ToString(CultureInfo.InvariantCulture) : string.Empty,
            LoggedRepsText = currentLog is not null ? currentLog.CompletedReps.ToString(CultureInfo.InvariantCulture) :
                dayExercise.Reps > 0 ? dayExercise.Reps.ToString(CultureInfo.InvariantCulture) : string.Empty,
            LoggedWeightText = currentLog?.WeightKg is >= 0m
                ? $"{currentLog.WeightKg.Value:0.#}"
                : (previousLog?.WeightKg is > 0m ? $"{previousLog.WeightKg.Value:0.#}" : string.Empty),
            LoggedDurationMinutesText = currentLog?.CompletedDurationSeconds is >= 0
                ? $"{Math.Ceiling(currentLog.CompletedDurationSeconds.Value / 60d):0}"
                : (dayExercise.DurationSeconds is > 0 ? $"{Math.Ceiling(dayExercise.DurationSeconds.Value / 60d):0}" : string.Empty),
            LoggedDistanceKmText = currentLog?.DistanceKm is >= 0m ? $"{currentLog.DistanceKm.Value:0.#}" : string.Empty,
            LoggedPaceText = currentLog?.PaceSecondsPerKm is > 0
                ? $"{currentLog.PaceSecondsPerKm.Value / 60}:{currentLog.PaceSecondsPerKm.Value % 60:00}"
                : string.Empty,
            LoggedHeartRateText = currentLog?.AverageHeartRateBpm is > 0
                ? currentLog.AverageHeartRateBpm.Value.ToString(CultureInfo.InvariantCulture)
                : string.Empty,
            LoggedPowerWattsText = currentLog?.PowerWatts is > 0
                ? currentLog.PowerWatts.Value.ToString(CultureInfo.InvariantCulture)
                : string.Empty,
            LoggedCadenceRpmText = currentLog?.CadenceRpm is > 0
                ? currentLog.CadenceRpm.Value.ToString(CultureInfo.InvariantCulture)
                : string.Empty,
            LoggedEffortText = currentLog?.EffortRating is >= 1 and <= 10
                ? currentLog.EffortRating.Value.ToString(CultureInfo.InvariantCulture)
                : string.Empty,
            DurationLabel = isTimeUnderTension ? "Minutes under tension" : "Minutes",
            DistanceLabel = isSwimming ? "Meters" : "km",
            PaceLabel = isSwimming ? "Pace / 100m" : "Pace / km",
            UsesEnduranceMetrics = usesEndurance,
            UsesDurationMetric = usesEndurance,
            UsesDistanceMetric = isDistanceCardio,
            UsesPaceMetric = isRunning || isSwimming,
            UsesHeartRateMetric = isCycling || isRunning || isSwimming || isHiit || isDistanceCardio,
            UsesPowerMetric = isCycling,
            UsesCadenceMetric = isCycling,
            UsesEffortMetric = isCycling || isRunning || isSwimming || isHiit,
            UsesWeight = !usesEndurance
        };
    }

    private static IReadOnlyList<WorkoutActivityDetailDto> BuildActivityDetails(
        WorkoutDayRecord day,
        IReadOnlyList<WorkoutDayExerciseRecord> orderedExercises,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        IReadOnlyList<WorkoutExerciseDetailDto> exerciseDtos)
    {
        var byTag = exerciseDtos
            .GroupBy(e => e.ActivityTag)
            .ToDictionary(g => g.Key, g => g.ToList());
        var tags = BuildActivityTags(day, orderedExercises, exercisesById)
            .Where(t => byTag.ContainsKey(t))
            .ToList();
        if (exerciseDtos.Count == 0 && !string.Equals(day.WorkoutType, nameof(WorkoutType.Rest), StringComparison.OrdinalIgnoreCase))
        {
            var tag = BuildPrimaryTag(day, orderedExercises, exercisesById);
            return [new WorkoutActivityDetailDto(1, 1, tag, day.Name, BuildSectionSubtitle(tag),
                day.PlannedMainDurationMinutes is { } duration ? $"{duration:0.#} min" : string.Empty,
                string.Empty, [])];
        }
        if (tags.Count == 0 && exerciseDtos.Count > 0)
            tags.Add(BuildPrimaryTag(day, orderedExercises, exercisesById));

        return tags.Select((tag, index) =>
        {
            var group = byTag.GetValueOrDefault(tag) ?? [];
            var loggedCount = group.Count(e => e.IsLogged);
            var summary = loggedCount == group.Count
                ? "Everything here is logged."
                : $"{loggedCount} of {group.Count} logged";
            var metric = tag == StrengthTag
                ? $"{group.Count} {(group.Count == 1 ? "exercise" : "exercises")}"
                : $"{group.Count} {(group.Count == 1 ? "movement" : "movements")}";
            return new WorkoutActivityDetailDto(
                index + 1,
                tags.Count,
                tag,
                BuildSectionTitle(tag),
                BuildSectionSubtitle(tag),
                metric,
                summary,
                group.Select(e => e.WorkoutDayExerciseId).ToArray());
        }).ToArray();
    }

    private static bool IsExerciseLogComplete(
        WorkoutExerciseLogRecord? log,
        WorkoutDayExerciseRecord dayExercise,
        string exerciseName)
    {
        if (log is null) return false;
        if (UsesEnduranceMetrics(dayExercise, exerciseName))
            return log.CompletedDurationSeconds is > 0;
        return log.CompletedSets > 0 && log.CompletedReps > 0 && log.WeightKg is >= 0m;
    }

    private static bool UsesEnduranceMetrics(WorkoutDayExerciseRecord dayExercise, string name) =>
        dayExercise.DurationSeconds is > 0 ||
        ContainsAny(name, "plank", "copenhagen", "bike", "cycle", "cycling", "ride", "run", "jog",
            "sprint", "tempo", "swim", "pool", "hiit", "interval", "yoga", "vinyasa", "mobility",
            "recovery", "breathing", "pilates", "walk");

    private static string BuildPrimaryTag(
        WorkoutDayRecord day,
        IReadOnlyList<WorkoutDayExerciseRecord> exercises,
        IReadOnlyDictionary<int, Exercise> exercisesById)
    {
        if (string.Equals(day.WorkoutType, nameof(WorkoutType.Rest), StringComparison.OrdinalIgnoreCase))
            return RestTag;
        var tags = BuildActivityTags(day, exercises, exercisesById);
        return tags.FirstOrDefault() ?? StrengthTag;
    }

    private static IReadOnlyList<string> BuildActivityTags(
        WorkoutDayRecord day,
        IReadOnlyList<WorkoutDayExerciseRecord> exercises,
        IReadOnlyDictionary<int, Exercise> exercisesById)
    {
        if (string.Equals(day.WorkoutType, nameof(WorkoutType.Rest), StringComparison.OrdinalIgnoreCase))
            return [RestTag];

        var tags = exercises
            .Select(e => ClassifyExerciseTag(e, day.WorkoutType, exercisesById))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (tags.Count == 0)
        {
            tags.Add(day.WorkoutType switch
            {
                nameof(WorkoutType.Cardio) => CardioTag,
                nameof(WorkoutType.Recovery) => RecoveryTag,
                _ => StrengthTag
            });
        }

        return tags;
    }

    private static string ClassifyExerciseTag(
        WorkoutDayExerciseRecord exercise,
        string workoutType,
        IReadOnlyDictionary<int, Exercise> exercisesById) =>
        ClassifyExerciseTag(exercise, workoutType, exercisesById.GetValueOrDefault(exercise.ExerciseId));

    private static string ClassifyExerciseTag(
        WorkoutDayExerciseRecord exercise,
        string workoutType,
        Exercise? catalogExercise)
    {
        var name = catalogExercise?.Name ?? string.Empty;
        if (ContainsAny(name, "yoga", "vinyasa", "mobility", "recovery", "breathing", "pilates", "stretch"))
            return RecoveryTag;
        if (ContainsAny(name, "run", "jog", "sprint", "tempo", "cycle", "cycling", "ride", "bike", "swim", "pool", "hiit", "interval", "walk", "dance"))
            return CardioTag;
        if (string.Equals(workoutType, nameof(WorkoutType.Recovery), StringComparison.OrdinalIgnoreCase))
            return RecoveryTag;
        if (string.Equals(workoutType, nameof(WorkoutType.Cardio), StringComparison.OrdinalIgnoreCase) && exercise.DurationSeconds is > 0)
            return CardioTag;
        return StrengthTag;
    }

    private static string BuildSectionTitle(string tag) => tag switch
    {
        CardioTag => "Cardio & Conditioning",
        RecoveryTag => "Mobility & Recovery",
        RestTag => "Rest & Recovery",
        _ => "Strength & Sculpt"
    };

    private static string BuildSectionSubtitle(string tag) => tag switch
    {
        CardioTag => "Paced conditioning work.",
        RecoveryTag => "Controlled movement and breathing.",
        _ => "Progressive sets and reps."
    };

    private static string BuildDurationText(WorkoutDayRecord day, IReadOnlyList<WorkoutDayExerciseRecord> exercises)
    {
        if (day.PlannedMainDurationMinutes is > 0m)
            return $"{day.PlannedMainDurationMinutes.Value.ToString(PlannedDurationMinuteFormat, CultureInfo.CurrentCulture)} min";

        var seconds = exercises.Sum(e => e.DurationSeconds ?? 0);
        var doses = exercises.Where(e => e.Sets > 0 && e.Reps > 0)
            .Select(e => new WorkoutExerciseDose(e.Sets, e.Reps))
            .ToArray();
        if (seconds > 0 && doses.Length > 0)
            return $"{Math.Max(1, WorkoutDurationRule.EstimateStrengthMinutes(doses) + (int)Math.Ceiling(seconds / 60d))} min";
        if (seconds > 0)
            return $"{Math.Max(1, (int)Math.Ceiling(seconds / 60d))} min";
        if (string.Equals(day.WorkoutType, nameof(WorkoutType.Strength), StringComparison.OrdinalIgnoreCase) && doses.Length > 0)
            return $"{WorkoutDurationRule.EstimateStrengthMinutes(doses)} min";
        return exercises.Count == 0 ? "Flexible" : $"{Math.Max(20, exercises.Count * FallbackMinutesPerExercise)} min";
    }

    private static string BuildDetailsText(
        WorkoutDayRecord day,
        IReadOnlyList<WorkoutDayExerciseRecord> exercises,
        IReadOnlyDictionary<int, Exercise> exercisesById)
    {
        var names = exercises
            .Select(e => exercisesById.GetValueOrDefault(e.ExerciseId)?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Take(2)
            .ToArray();
        return names.Length > 0
            ? string.Join(" and ", names)
            : (exercises.Count > 0 ? "Exercise details ready" : $"Planned {day.Name} session.");
    }

    private static string BuildWorkoutDaySubtitle(
        WorkoutDayRecord day,
        IReadOnlyList<WorkoutDayExerciseRecord> exercises,
        IReadOnlyDictionary<int, Exercise> exercisesById)
    {
        if (string.Equals(day.WorkoutType, nameof(WorkoutType.Recovery), StringComparison.OrdinalIgnoreCase))
            return $"{day.Name} session for easy movement and recovery.";
        if (string.Equals(day.WorkoutType, nameof(WorkoutType.Cardio), StringComparison.OrdinalIgnoreCase))
            return $"{day.Name} session paced to support recovery.";
        if (exercises.Count == 0)
            return $"{day.Name} session planned.";
        var names = exercises
            .Select(e => exercisesById.GetValueOrDefault(e.ExerciseId)?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Take(2)
            .ToArray();
        return names.Length > 0
            ? $"{string.Join(" and ", names)} anchor the session."
            : "Strength work with clear sets, reps, and room to progress.";
    }

    private static string BuildWorkoutDaySummary(
        WorkoutDayRecord day,
        IReadOnlyList<WorkoutDayExerciseRecord> exercises)
    {
        var duration = BuildDurationText(day, exercises);
        if (exercises.Count == 0)
            return $"{day.Name}, about {duration}";
        return $"{exercises.Count} {(exercises.Count == 1 ? "exercise" : "exercises")}, about {duration}";
    }

    private static int? ExtractPlanningId(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || !code.StartsWith("ENGINE_", StringComparison.OrdinalIgnoreCase))
            return null;
        return int.TryParse(code["ENGINE_".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
    }

    private static double ComputeMuscleMatchScore(
        ICollection<ExerciseMuscleContribution> current,
        ICollection<ExerciseMuscleContribution> candidate,
        int primaryMuscleId)
    {
        if (candidate.Count == 0)
            return 0;
        var candidatePrimary = candidate.FirstOrDefault(c => c.MuscleGroupId == primaryMuscleId);
        if (candidatePrimary is null || candidatePrimary.Fraction < 0.3m)
            return 0;

        var currentMap = current.ToDictionary(c => c.MuscleGroupId, c => (double)c.Fraction);
        var dot = 0.0;
        var normA = 0.0;
        var normB = 0.0;
        foreach (var (_, frac) in currentMap)
            normA += frac * frac;
        foreach (var c in candidate)
        {
            var frac = (double)c.Fraction;
            normB += frac * frac;
            if (currentMap.TryGetValue(c.MuscleGroupId, out var a))
                dot += a * frac;
        }

        return normA <= 0 || normB <= 0 ? 0 : dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private static string FormatEquipmentName(EquipmentSet required)
    {
        if (required == EquipmentSet.None) return "Bodyweight";
        return required.ToString();
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}
