using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.V2;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Session;

public sealed class PostgresWorkoutPlanAndSessionStore(PostgresDbContext dbContext)
{
    private const int OverviewScheduleWeeks = 4;
    private const int CompletedWorkoutPercent = 100;
    private const int DaysPerWeek = 7;
    private const int DefaultCycleLengthDays = 28;
    private const int FallbackMinutesPerExercise = 12;
    private const int MaximumSwapCandidates = 8;
    private const string PlannedDurationMinuteFormat = "0";
    private const string StrengthTag = WorkoutActivityTagRule.Strength;
    private const string CardioTag = WorkoutActivityTagRule.Cardio;
    private const string RecoveryTag = WorkoutActivityTagRule.Recovery;
    private const string RestTag = WorkoutActivityTagRule.Rest;

    // Accepted V2 occurrences retain definition IDs for progression and immutable prescriptions.
    // Older day rows use public catalog IDs. The two database sequences overlap.
    private static bool UsesDefinitionId(WorkoutDayExerciseRecord row) =>
        row.PlanningTemplateId == "MC-V2" || row.PlanningSlotId?.StartsWith("v2-", StringComparison.Ordinal) == true;

    private async Task<Dictionary<int, Exercise>> LoadCatalogAsync(IEnumerable<int> storedIds,
        bool usesDefinitionIds, CancellationToken cancellationToken)
    {
        var ids = storedIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        if (!usesDefinitionIds)
            return await dbContext.Exercises.AsNoTracking().Where(row => ids.Contains(row.Id))
                .ToDictionaryAsync(row => row.Id, cancellationToken).ConfigureAwait(false);

        var definitions = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
            .Where(row => ids.Contains(row.Id)).Select(row => new { row.Id, row.SourceExerciseId })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var codes = definitions.Where(row => row.SourceExerciseId.HasValue)
            .ToDictionary(row => row.Id, row => $"ENGINE_{row.SourceExerciseId!.Value}");
        var wanted = codes.Values.ToArray();
        var mirrors = await dbContext.Exercises.AsNoTracking().Where(row => wanted.Contains(row.Code))
            .ToDictionaryAsync(row => row.Code, cancellationToken).ConfigureAwait(false);
        if (codes.Count != ids.Length || mirrors.Count != ids.Length)
            throw new InvalidOperationException("The reviewed exercise catalog mapping is incomplete.");
        return codes.ToDictionary(pair => pair.Key, pair => mirrors[pair.Value]);
    }

    private async Task<Dictionary<int, Exercise>> LoadSessionCatalogAsync(
        IReadOnlyCollection<WorkoutDayExerciseRecord> rows, CancellationToken cancellationToken)
    {
        var legacy = await LoadCatalogAsync(rows.Where(row => !UsesDefinitionId(row)).Select(row => row.ExerciseId),
            false, cancellationToken);
        var reviewed = await LoadCatalogAsync(rows.Where(UsesDefinitionId).Select(row => row.ExerciseId),
            true, cancellationToken);
        foreach (var (id, exercise) in reviewed)
        {
            if (legacy.TryGetValue(id, out var other) && other.Id != exercise.Id)
                throw new InvalidOperationException("Historical workout rows have conflicting exercise identities.");
            legacy[id] = exercise;
        }
        return legacy;
    }

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

            await WorkoutSessionAccountLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            var cycleReadinessCurrent = await PostgresCycleReadinessReconciler.ReconcileAsync(
                dbContext, userId, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);

            var planningState = await dbContext.WorkoutAccountPlanningStates.AsNoTracking()
                .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken).ConfigureAwait(false);
            var today = PlanningSafetyInputs.AccountToday(planningState?.TimeZoneId)
                ?? DateOnly.FromDateTime(DateTime.UtcNow);
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

            if (cycleReadinessCurrent && dayIds.Count > 0)
            {
                var overlays = await dbContext.WorkoutCycleReadinessOccurrences.AsNoTracking()
                    .Where(row => row.UserId == userId && dayIds.Contains(row.WorkoutDayId))
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                foreach (var overlay in overlays)
                {
                    var saved = PlannedPrescriptionSnapshot.Deserialize(overlay.PrescriptionJson);
                    var day = workoutDays.SingleOrDefault(item => item.Id == overlay.WorkoutDayId);
                    if (day is null || day.Date != overlay.LocalDate) continue;
                    var baseline = new PlannedPrescriptionSnapshot(day.Id, day.Name, day.WorkoutType, day.ActivityTypeId,
                        day.PlannedMainDurationMinutes, dayExercises.Where(item => item.WorkoutDayId == day.Id)
                            .Select(PlannedExerciseSnapshot.FromRecord).ToArray()) { ActivityPrescriptionJson = day.PlannedActivityPrescriptionJson };
                    if (!PostgresCycleReadinessReconciler.MatchesBaseline(baseline, saved)) continue;
                    var doses = saved!.Exercises.ToDictionary(exercise => exercise.Id);
                    foreach (var exercise in dayExercises.Where(item => item.WorkoutDayId == overlay.WorkoutDayId))
                        if (doses.TryGetValue(exercise.Id, out var dose))
                        { exercise.Sets = dose.Sets; exercise.OptionalSets = dose.OptionalSets; }
                }
            }

            var canonicalRows = dayExercises.Where(UsesDefinitionId).ToArray();
            var mirrored = await LoadCatalogAsync(canonicalRows.Select(row => row.ExerciseId), true, cancellationToken);
            var exercisesById = await LoadCatalogAsync(dayExercises.Where(row => !UsesDefinitionId(row))
                .Select(row => row.ExerciseId), false, cancellationToken);
            // Normalize only detached overview rows so a retired legacy day and an active V2 day can coexist.
            foreach (var row in canonicalRows)
            {
                var exercise = mirrored[row.ExerciseId];
                row.ExerciseId = exercise.Id;
                exercisesById[exercise.Id] = exercise;
            }

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
            return WorkoutOverviewBuilder.Build(
                activePlan?.Name, phase.ToString(), startMonday, today,
                workoutDays, scheduledDays, exercisesByDayId, exercisesById, workoutLogs, inProgressDayId);
        }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Pure overview assembly, extracted from LoadOverviewAsync so it can be driven with
    ///     materialized records. No database access happens here.
    /// </summary>
    public static class WorkoutOverviewBuilder
    {
        public static WorkoutOverviewResponse Build(
            string? planName,
            string phase,
            DateOnly startMonday,
            DateOnly today,
            IReadOnlyList<WorkoutDayRecord> workoutDays,
            IReadOnlyList<WorkoutDayRecord> scheduledDays,
            IReadOnlyDictionary<Guid, IReadOnlyList<WorkoutDayExerciseRecord>> exercisesByDayId,
            IReadOnlyDictionary<int, Exercise>? exercisesById,
            IReadOnlyList<WorkoutLogRecord> workoutLogs,
            Guid? inProgressDayId)
        {
            exercisesById ??= new Dictionary<int, Exercise>();
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
            return new WorkoutOverviewResponse(planName, phase, workouts, schedule, previews)
            {
                InProgressWorkout = inProgressWorkout is null ? null : inProgressWorkout with { IsInProgress = true }
            };
        }
    }

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
            if (sourceDay.WorkoutType == RestWorkoutType || sourceDay.Date is not { } sourceDate)
                throw new WorkoutRescheduleRejectedException(WorkoutRescheduleRejectedException.SessionNotMovableCode);
            // Only real calendar dates of the active plan are targets; no weekday guess picks a session.
            var targetRows = days.Where(d => d.Date == request.TargetDate).ToArray();
            if (targetRows.Length == 0)
                throw new InvalidOperationException("Specified schedule dates are not in the active plan.");

            if (sourceDate != request.TargetDate)
            {
                if (await dbContext.WorkoutSessionLogs.AsNoTracking().AnyAsync(log => log.UserId == userId &&
                        log.PlannedWorkoutDayId == sourceDay.Id, cancellationToken).ConfigureAwait(false))
                    throw new WorkoutRescheduleRejectedException(WorkoutRescheduleRejectedException.SessionAlreadyStartedCode);

                // The whole occurrence moves: activity, prescription, duration and exercises stay with
                // the row; only its date, weekday, week and same-day order change.
                var targetRest = targetRows.Where(d => d.WorkoutType == RestWorkoutType).ToArray();
                var sourceWeekIndex = sourceDay.WeekIndex;
                sourceDay.Date = request.TargetDate;
                sourceDay.DayOfWeek = (int)request.TargetDate.DayOfWeek;
                sourceDay.WeekIndex = targetRows[0].WeekIndex;
                var sourceDateEmpty = !days.Any(d => d.Id != sourceDay.Id && d.Date == sourceDate &&
                                                     d.WorkoutType != RestWorkoutType);
                foreach (var rest in targetRest)
                {
                    if (sourceDateEmpty)
                    {
                        // The vacated date keeps its rest placeholder.
                        rest.Date = sourceDate;
                        rest.DayOfWeek = (int)sourceDate.DayOfWeek;
                        rest.WeekIndex = sourceWeekIndex;
                        sourceDateEmpty = false;
                    }
                    else
                    {
                        dbContext.WorkoutDays.Remove(rest);
                        days.Remove(rest);
                    }
                }

                ReorderDay(days, request.TargetDate);
                ReorderDay(days, sourceDate);
                var reasons = await ValidateRescheduledWindowAsync(userId, days, request.TargetDate, cancellationToken)
                    .ConfigureAwait(false);
                if (reasons.Count > 0)
                    throw new WorkoutRescheduleRejectedException(reasons);

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
            await WorkoutSessionAccountLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);

            var planningState = await dbContext.WorkoutAccountPlanningStates.AsNoTracking()
                .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken).ConfigureAwait(false);
            var today = PlanningSafetyInputs.AccountToday(planningState?.TimeZoneId)
                ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var phase = await ResolveCyclePhaseAsync(userId, today, cancellationToken).ConfigureAwait(false);
            var day = await ResolveWorkoutDayAsync(userId, workoutDayId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Workout session was not found.");

            var stableDayId = ToStableWorkoutIntId(day.Id);
            if (string.Equals(day.WorkoutType, nameof(WorkoutType.Rest), StringComparison.OrdinalIgnoreCase))
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new WorkoutSessionDetailDto(
                    stableDayId,
                    "Recovery day",
                    "A full day away from training.",
                    "No exercises today. Log the rest if you actually took it.",
                    true,
                    [],
                    []);
            }

            var activeSession = await dbContext.WorkoutSessionLogs.AsNoTracking().FirstOrDefaultAsync(
                session => session.UserId == userId && session.PlannedWorkoutDayId == day.Id &&
                    !session.IsDeleted && session.CompletedAt == null, cancellationToken).ConfigureAwait(false);
            var recordedSession = activeSession ?? await dbContext.WorkoutSessionLogs.AsNoTracking()
                .Where(session => session.UserId == userId && session.PlannedWorkoutDayId == day.Id &&
                    !session.IsDeleted && session.CompletedAt != null)
                .OrderByDescending(session => session.CompletedAt).ThenByDescending(session => session.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            var prescription = PlannedPrescriptionSnapshot.Deserialize(recordedSession?.PlannedPrescriptionSnapshotJson);
            if (recordedSession?.PlannedPrescriptionSnapshotJson is not null &&
                (prescription is null || prescription.DayId != day.Id || prescription.Exercises is null))
                throw new InvalidOperationException("The active workout prescription is unavailable.");
            var generationV2 = prescription?.GenerationV2;
            if (generationV2 is null)
            {
                var acceptedGenerationV2 = await AcceptedGenerationPlanV2.LoadSessionAsync(
                    dbContext, userId, day, cancellationToken).ConfigureAwait(false);
                if (recordedSession is not null && acceptedGenerationV2 is not null)
                    throw new InvalidOperationException("The active V2 workout prescription is unavailable.");
                generationV2 = acceptedGenerationV2;
            }
            if (prescription is not null)
            {
                day.Name = prescription.Name;
                day.WorkoutType = prescription.WorkoutType;
                day.ActivityTypeId = prescription.ActivityTypeId;
                day.PlannedMainDurationMinutes = prescription.PlannedMainDurationMinutes;
                day.PlannedActivityPrescriptionJson = prescription.ActivityPrescriptionJson;
            }
            var orderedExercises = prescription is not null
                ? prescription.Exercises.Select(exercise => exercise.ToRecord(userId, day.Id)).ToList()
                : await dbContext.WorkoutDayExercises.AsNoTracking()
                    .Where(e => e.UserId == userId && e.WorkoutDayId == day.Id && !e.IsRemovedByReadiness)
                    .OrderBy(e => e.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            var generationExercises = generationV2?.Exercises.ToDictionary(item => item.ExerciseId)
                ?? new Dictionary<int, GenerationExerciseV2>();
            if (generationV2 is not null && (generationV2.Exercises.Count != orderedExercises.Count ||
                orderedExercises.Any(item => !generationExercises.ContainsKey(item.ExerciseId))))
                throw new InvalidOperationException("The accepted V2 exercise prescription does not match this workout.");

            if (recordedSession is null)
            {
                if (await PostgresCycleReadinessReconciler.ReconcileAsync(dbContext, userId, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false))
                {
                    var overlay = await dbContext.WorkoutCycleReadinessOccurrences.AsNoTracking()
                        .SingleOrDefaultAsync(row => row.UserId == userId && row.WorkoutDayId == day.Id && row.LocalDate == day.Date,
                            cancellationToken).ConfigureAwait(false);
                    var saved = PlannedPrescriptionSnapshot.Deserialize(overlay?.PrescriptionJson);
                    var baseline = new PlannedPrescriptionSnapshot(day.Id, day.Name, day.WorkoutType, day.ActivityTypeId,
                        day.PlannedMainDurationMinutes, orderedExercises.Select(PlannedExerciseSnapshot.FromRecord).ToArray())
                        { ActivityPrescriptionJson = day.PlannedActivityPrescriptionJson };
                    if (PostgresCycleReadinessReconciler.MatchesBaseline(baseline, saved))
                    {
                        prescription = saved;
                        orderedExercises = saved!.Exercises.Select(exercise => exercise.ToRecord(userId, day.Id)).ToList();
                    }
                }
            }

            generationV2 = AcceptedGenerationPlanV2.ApplyOccurrenceDose(generationV2,
                orderedExercises.Select(PlannedExerciseSnapshot.FromRecord).ToArray());
            generationExercises = generationV2?.Exercises.ToDictionary(item => item.ExerciseId)
                ?? new Dictionary<int, GenerationExerciseV2>();
            if (generationV2 is not null)
                orderedExercises = orderedExercises.OrderBy(item => generationExercises[item.ExerciseId].OrderIndex).ToList();
            var exerciseIds = orderedExercises.Select(e => e.ExerciseId).Distinct().ToArray();
            var exercisesById = await LoadSessionCatalogAsync(orderedExercises, cancellationToken);

            // Bodyweight/slider movements use zero added kg; resistance-band movements use repetition-based
            // logging without fabricated kg resistance. Join via ENGINE_<planningId> -> SourceExerciseId first.
            var planningIdByExerciseId = exercisesById.ToDictionary(
                pair => pair.Key,
                pair => ExtractPlanningId(pair.Value.Code) ?? pair.Key);
            var definitionLookupIds = planningIdByExerciseId.Values.Concat(exerciseIds).Distinct().ToArray();
            var definitions = definitionLookupIds.Length == 0
                ? []
                : await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
                    .Where(d => definitionLookupIds.Contains(d.Id) ||
                                (d.SourceExerciseId != null && definitionLookupIds.Contains(d.SourceExerciseId.Value)))
                    .Select(d => new { d.Id, d.SourceExerciseId, d.Required })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            var bodyweightExerciseIds = new HashSet<int>();
            var bandResistanceExerciseIds = new HashSet<int>();
            foreach (var exerciseId in exerciseIds)
            {
                var planningId = planningIdByExerciseId.GetValueOrDefault(exerciseId, exerciseId);
                var def = definitions.FirstOrDefault(d => d.SourceExerciseId == planningId)
                    ?? definitions.FirstOrDefault(d => d.Id == exerciseId);
                if (def is null)
                    continue;

                if ((def.Required & ~EquipmentSet.Sliders) == EquipmentSet.None)
                    bodyweightExerciseIds.Add(exerciseId);

                if ((def.Required & EquipmentSet.Bands) != EquipmentSet.None &&
                    (def.Required & (EquipmentSet.Barbell | EquipmentSet.Dumbbell |
                                     EquipmentSet.Kettlebell | EquipmentSet.Cables | EquipmentSet.Machines)) == EquipmentSet.None)
                    bandResistanceExerciseIds.Add(exerciseId);
            }

            var currentLogs = dbContext.WorkoutLogs.AsNoTracking()
                .Where(l => l.UserId == userId && l.WorkoutDayId == day.Id);
            currentLogs = recordedSession is null
                ? currentLogs.Where(log => log.Date == (day.Date ?? today))
                : currentLogs.Where(log => log.PlannedSessionId == recordedSession.Id);
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

            var currentObservedSets = recordedSession is null
                ? []
                : await dbContext.ExerciseSetLogs.AsNoTracking()
                    .Where(set => set.UserId == userId && set.WorkoutSessionLogId == recordedSession.Id &&
                        !set.IsDeleted && set.IsObserved)
                    .OrderBy(set => set.SetNumber).ThenBy(set => set.CreatedAt).ThenBy(set => set.Id)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

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

            var userProfile = await dbContext.UserProfiles.AsNoTracking()
                .SingleOrDefaultAsync(profile => profile.UserId == userId, cancellationToken)
                .ConfigureAwait(false);
            var progressionTier = ResolveExperienceTier(userProfile);
            var observedHistory = await PostgresSnapshotProgressionHistory.LoadAsync(
                dbContext, userId, exerciseIds, recordedSession?.Id, cancellationToken,
                generationV2?.Exercises.Where(item => item.IsUnilateral).Select(item => item.ExerciseId).ToHashSet())
                .ConfigureAwait(false);

            var publicExerciseIds = exercisesById.Values.Select(exercise => exercise.Id).Distinct().ToArray();
            var notesByExerciseId = publicExerciseIds.Length == 0
                ? new Dictionary<int, string>()
                : await dbContext.UserMovementNotes.AsNoTracking()
                    .Where(n => n.UserId == userId && publicExerciseIds.Contains(n.ExerciseId))
                    .ToDictionaryAsync(n => n.ExerciseId, n => n.Notes, cancellationToken)
                    .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);


            var exerciseDtos = orderedExercises.Select(dayEx =>
            {
                var catalogEx = exercisesById.GetValueOrDefault(dayEx.ExerciseId);
                var prevLog = latestPreviousByExerciseId.GetValueOrDefault(dayEx.ExerciseId);
                var currLog = currentByDayExerciseId.GetValueOrDefault(dayEx.Id);
                var movementNote = catalogEx is null ? string.Empty : notesByExerciseId.GetValueOrDefault(catalogEx.Id, string.Empty);
                var observedWeeks = observedHistory.GetValueOrDefault(dayEx.ExerciseId);
                var generationExercise = generationExercises.GetValueOrDefault(dayEx.ExerciseId);
                if (prescription?.SplitSupersetWorkoutDayExerciseIds.Contains(dayEx.Id) == true &&
                    generationExercise is not null)
                    generationExercise = generationExercise with { SupersetGroupId = null };
                var actualSets = currentObservedSets.Where(set => set.ExerciseId == dayEx.ExerciseId.ToString(CultureInfo.InvariantCulture))
                    .ToArray();
                return BuildExerciseDetailDto(dayEx, day.WorkoutType, catalogEx, prevLog, currLog, movementNote,
                    progressionTier, observedWeeks, bodyweightExerciseIds.Contains(dayEx.ExerciseId),
                    generationExercise, actualSets, bandResistanceExerciseIds.Contains(dayEx.ExerciseId));
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
                GeneralWarmupMinutes = generationV2?.GeneralWarmupMinutes,
                EstimatedTotalMinutes = generationV2?.EstimatedTotalMinutes,
                AttachedFinisher = generationV2?.AttachedFinisher,
                IsOptional = day.IsOptional,
                IsEasierAlternative = day.IsEasierAlternative,
                DoseAdjustmentReason = prescription?.DoseAdjustmentReasonCode switch
                {
                    CycleReadinessPrescriptionRule.DailyReadinessAdjustment => WorkoutDoseAdjustmentReason.DailyReadiness,
                    CycleReadinessPrescriptionRule.ProvisionalPhaseAdjustment => WorkoutDoseAdjustmentReason.ProvisionalPhaseFeeling,
                    CycleReadinessPrescriptionRule.RepeatedPhaseAdjustment => WorkoutDoseAdjustmentReason.RepeatedPhasePattern,
                    CycleReadinessPrescriptionRule.SafetyReviewRequired => WorkoutDoseAdjustmentReason.SafetyReview,
                    _ => null
                },
                DoseAdjustmentConfidence = Enum.TryParse<WorkoutDoseAdjustmentConfidence>(prescription?.DoseAdjustmentConfidence, out var confidence)
                    && Enum.IsDefined(confidence) ? confidence : null,
                DoseAdjustmentPolicyVersion = prescription?.DoseAdjustmentPolicyVersion
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

    public async Task SplitSupersetAsync(Guid userId, SplitWorkoutSupersetRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SessionId == Guid.Empty || request.WorkoutDayExerciseId <= 0 || request.SupersetGroupId <= 0)
            throw new ArgumentOutOfRangeException(nameof(request));
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);

            var session = await dbContext.WorkoutSessionLogs.SingleOrDefaultAsync(row => row.UserId == userId &&
                row.Id == request.SessionId && !row.IsDeleted && row.CompletedAt == null, cancellationToken)
                .ConfigureAwait(false) ?? throw new InvalidOperationException("The workout session is not active.");
            if (session.PlannedWorkoutDayId is not { } dayId)
                throw new InvalidOperationException("A planned workout session is required to split a superset.");
            var snapshot = PlannedPrescriptionSnapshot.Deserialize(session.PlannedPrescriptionSnapshotJson);
            if (snapshot is null || snapshot.DayId != dayId || snapshot.GenerationV2 is not { } generation ||
                snapshot.Exercises is null)
                throw new InvalidOperationException("The V2 workout prescription is unavailable.");

            var target = snapshot.Exercises.SingleOrDefault(exercise =>
                ToStableWorkoutIntId(exercise.Id) == request.WorkoutDayExerciseId)
                ?? throw new KeyNotFoundException("Workout exercise was not found in this session.");
            var targetGeneration = generation.Exercises.SingleOrDefault(exercise => exercise.ExerciseId == target.ExerciseId);
            if (targetGeneration?.SupersetGroupId != request.SupersetGroupId)
                throw new InvalidOperationException("The superset changed. Reload the workout and try again.");

            var splitIds = generation.Exercises.Where(exercise => exercise.SupersetGroupId == request.SupersetGroupId)
                .Select(exercise => snapshot.Exercises.SingleOrDefault(item => item.ExerciseId == exercise.ExerciseId)?.Id
                    ?? throw new InvalidOperationException("The accepted superset does not match this session."))
                .ToHashSet();
            if (splitIds.Count < 2 || !splitIds.Contains(target.Id))
                throw new InvalidOperationException("The accepted superset does not match this session.");

            session.PlannedPrescriptionSnapshotJson = (snapshot with
            {
                SplitSupersetWorkoutDayExerciseIds = snapshot.SplitSupersetWorkoutDayExerciseIds
                    .Concat(splitIds).Distinct().ToArray()
            }).Serialize();
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
            var logDate = request.Date;
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
                logDate = plannedSession?.PlannedLocalDate ?? day.Date ?? request.Date;
                sessionPrescription = PlannedPrescriptionSnapshot.Deserialize(plannedSession?.PlannedPrescriptionSnapshotJson);
                if (plannedSession?.PlannedPrescriptionSnapshotJson is not null &&
                    (sessionPrescription is null || sessionPrescription.DayId != day.Id || sessionPrescription.Exercises is null))
                    throw new InvalidOperationException("The active workout prescription is unavailable.");
            }
            else day = await ResolveWorkoutDayAsync(userId, request.WorkoutDayId, cancellationToken).ConfigureAwait(false);
            if (day is null) throw new KeyNotFoundException("Workout session was not found.");
            if (day.IsRemovedBySafety) throw new WorkoutActivityNoLongerSafeException();

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

            var hasActivityActuals = request.ActivityActuals is { Count: > 0 } acts && acts.Any(a => a.DurationSeconds > 0);
            if (validInputs.Count == 0 && !hasActivityActuals)
                throw new InvalidOperationException("Log at least one exercise or activity before saving this workout.");

            var exerciseIds = dayExercises.Select(e => e.ExerciseId).Distinct().ToArray();
            var catalogExercises = await LoadSessionCatalogAsync(dayExercises, cancellationToken);

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
                .Where(l => l.UserId == userId && l.WorkoutDayId == day.Id &&
                            l.PlannedSessionId == request.PlannedSessionId &&
                            (request.PlannedSessionId != null || l.Date == logDate))
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
                    Date = logDate,
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
            var observedByDayExerciseId = validInputs
                .Where(pair => pair.Input.ObservedSets is { Count: > 0 })
                .ToDictionary(pair => pair.DayExercise.Id, pair => pair.Input.ObservedSets!);

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
                if (input.ObservedSets is { Count: > 0 } setsList)
                {
                    var validSets = setsList.Where(s => s.RepsCompleted > 0).ToList();
                    existing.CompletedSets = validSets.Count;
                    existing.CompletedReps = validSets.Count > 0 ? (int)Math.Round(validSets.Average(s => s.RepsCompleted)) : 0;
                    existing.WeightKg = validSets.Count > 0 ? validSets.First().LoadKg : (input.WeightKg is >= 0f ? (decimal)Math.Round(input.WeightKg.Value, 2) : null);
                    var avgRir = validSets.Where(s => s.RirActual.HasValue).Select(s => s.RirActual!.Value).ToList();
                    if (avgRir.Count > 0)
                        existing.EffortRating = Math.Clamp(10 - (int)Math.Round(avgRir.Average()), 1, 10);
                    else if (input.EffortRating is >= 1 and <= 10)
                        existing.EffortRating = input.EffortRating;
                }
                else
                {
                    existing.CompletedSets = Math.Max(0, input.CompletedSets);
                    existing.CompletedReps = Math.Max(0, input.CompletedReps);
                    existing.WeightKg = input.WeightKg is >= 0f ? (decimal)Math.Round(input.WeightKg.Value, 2) : null;
                    existing.EffortRating = input.EffortRating is >= 1 and <= 10 ? input.EffortRating : null;
                }

                existing.CompletedDurationSeconds = input.CompletedDurationSeconds is >= 0 ? input.CompletedDurationSeconds : null;
                existing.DistanceKm = input.DistanceKm is >= 0f ? (decimal)Math.Round(input.DistanceKm.Value, 2) : null;
                existing.AverageHeartRateBpm = input.AverageHeartRateBpm is >= 0 ? input.AverageHeartRateBpm : null;
                existing.PaceSecondsPerKm = input.PaceSecondsPerKm is >= 0 ? input.PaceSecondsPerKm : null;
                existing.PowerWatts = input.PowerWatts is >= 0 ? input.PowerWatts : null;
                existing.CadenceRpm = input.CadenceRpm is >= 0 ? input.CadenceRpm : null;
                existing.CreatedAt = nowUtc;
            }

            var completedCount = existingExerciseLogs.Count(log =>
            {
                var dayEx = dayExercises.FirstOrDefault(d => d.Id == log.WorkoutDayExerciseId);
                if (dayEx is null) return false;
                var catEx = catalogExercises.GetValueOrDefault(dayEx.ExerciseId);
                return IsExerciseLogComplete(log, dayEx, catEx?.Name ?? string.Empty);
            });
            primaryLog.CompletionPercent = (dayExercises.Count == 0 || (hasActivityActuals && validInputs.Count == 0))
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
                validInputs.Select(pair => pair.DayExercise.Id).ToHashSet(),
                catalogExercises,
                request.PlannedSessionId,
                observedByDayExerciseId,
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

            // The day exercise's catalog code carries the planning id; definition rows keep that
            // identity in their own column because their ids are assigned by the database.
            var currentDef = allDefinitions.FirstOrDefault(d => d.SourceExerciseId == currentPlanningId.Value);
            if (currentDef is null || currentDef.MuscleContributions.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return [];
            }

            var primaryMuscleId = currentDef.MuscleContributions
                .OrderByDescending(c => c.Fraction)
                .First()
                .MuscleGroupId;

            // Candidates come only from the reviewed directed substitutions of this exercise, filtered
            // by the planner's own eligibility context. Unknown equipment yields no candidates.
            var eligibility = await ExerciseSwapEligibilitySource.EvaluateAsync(dbContext, userId,
                currentDef.Id, allDefinitions,
                await LoadWorkoutActivityTypeIdAsync(userId, dayExercise.WorkoutDayId, cancellationToken)
                    .ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
            var eligibleIds = eligibility.Candidates.Select(candidate => candidate.Entry.Candidate.ExerciseId)
                .ToHashSet();

            var muscleGroups = await dbContext.WorkoutMuscleGroups.AsNoTracking()
                .ToDictionaryAsync(mg => mg.Id, mg => mg.Name, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return allDefinitions
                .Where(e => e.Id != currentDef.Id && e.SourceExerciseId is not null && eligibleIds.Contains(e.Id))
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
                .ThenBy(x => x.Exercise.Id)
                .Take(MaximumSwapCandidates)
                .Select(x => new WorkoutSwapCandidateDto(
                    x.Exercise.Id,
                    x.Exercise.Name,
                    $"{(int)(x.MatchScore * 100)}% muscle match",
                    FormatEquipmentName(x.Exercise.Required),
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
            await PlanningCatalogFingerprint.LockCatalogAsync(dbContext, cancellationToken).ConfigureAwait(false);

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

            // The selected exercise must be one the candidate list could have offered: a reviewed close
            // alternative to the planned exercise that passes the planner's eligibility filters.
            if (originalPlanningId is null)
                throw new InvalidOperationException(ExerciseSwapEligibilityRule.OriginalExerciseNotReviewedCode);
            var allDefinitions = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var originalDef = allDefinitions.FirstOrDefault(d => d.SourceExerciseId == originalPlanningId.Value)
                ?? throw new InvalidOperationException(ExerciseSwapEligibilityRule.OriginalExerciseNotReviewedCode);
            var eligibility = await ExerciseSwapEligibilitySource.EvaluateAsync(dbContext, userId,
                originalDef.Id, allDefinitions,
                await LoadWorkoutActivityTypeIdAsync(userId, dayExercise.WorkoutDayId, cancellationToken)
                    .ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
            if (!eligibility.Allows(newExerciseDef.Id))
                throw new InvalidOperationException(eligibility.ReasonCodes.FirstOrDefault() ??
                    ExerciseSwapEligibilityRule.SubstituteNotEligibleCode);

            // The reviewed definition names the planning exercise it mirrors; resolve that code alone
            // so an id collision can never bind a different catalog row.
            var targetPlanningId = newExerciseDef.SourceExerciseId
                ?? throw new InvalidOperationException(ExerciseSwapEligibilityRule.NoEligibleReviewedSubstituteCode);
            var targetCode = $"ENGINE_{targetPlanningId}";
            var catalogExercise = await dbContext.Exercises.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Code == targetCode, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Catalog exercise for planning ID {targetPlanningId} was not found.");

            dayExercise.ExerciseId = catalogExercise.Id;

            if (request.SavePreference)
            {
                var existingPref = await dbContext.UserExercisePreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.OriginalExerciseId == originalDef.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (existingPref is not null)
                {
                    existingPref.PreferredExerciseId = newExerciseDef.Id;
                    existingPref.CreatedAt = DateTime.UtcNow;
                }
                else
                {
                    dbContext.UserExercisePreferences.Add(new UserExercisePreferenceRecord
                    {
                        Id = Guid.CreateVersion7(),
                        UserId = userId,
                        OriginalExerciseId = originalDef.Id,
                        PreferredExerciseId = newExerciseDef.Id,
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

            var catalog = await LoadCatalogAsync([dayExercise.ExerciseId], UsesDefinitionId(dayExercise), cancellationToken);
            var catalogExercise = catalog.GetValueOrDefault(dayExercise.ExerciseId)
                ?? throw new InvalidOperationException("The exercise catalog entry is unavailable.");

            var note = await dbContext.UserMovementNotes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.UserId == userId && n.ExerciseId == catalogExercise.Id, cancellationToken)
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

            var catalog = await LoadCatalogAsync([dayExercise.ExerciseId], UsesDefinitionId(dayExercise), cancellationToken);
            var catalogExercise = catalog.GetValueOrDefault(dayExercise.ExerciseId)
                ?? throw new InvalidOperationException("The exercise catalog entry is unavailable.");

            var existing = await dbContext.UserMovementNotes
                .FirstOrDefaultAsync(n => n.UserId == userId && n.ExerciseId == catalogExercise.Id, cancellationToken)
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
                dbContext.UserMovementNotes.Add(new UserMovementNoteRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    ExerciseId = catalogExercise.Id,
                    ExerciseName = catalogExercise.Name,
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
        IReadOnlySet<Guid> updatedDayExerciseIds,
        IReadOnlyDictionary<int, Exercise> catalogExercises,
        Guid? plannedSessionId,
        IReadOnlyDictionary<Guid, IReadOnlyList<ObservedSetRecord>> observedByDayExerciseId,
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
            .Where(s => s.UserId == userId && s.WorkoutSessionLogId == session.Id && !s.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var updatedLogs = exerciseLogs.Where(log => updatedDayExerciseIds.Contains(log.WorkoutDayExerciseId)).ToArray();
        var updatedExerciseIds = updatedLogs.Select(log => log.ExerciseId.ToString(CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.Ordinal);
        dbContext.ExerciseSetLogs.RemoveRange(existingSets.Where(set =>
            set.ExerciseId is { } exerciseId && updatedExerciseIds.Contains(exerciseId)));

        var totalVolume = existingSets.Where(set => !set.IsWarmup &&
                (set.ExerciseId is null || !updatedExerciseIds.Contains(set.ExerciseId)))
            .Sum(set => set.WeightKg * set.RepsCompleted);
        foreach (var exLog in updatedLogs)
        {
            var exName = catalogExercises.GetValueOrDefault(exLog.ExerciseId)?.Name ?? "Exercise";

            if (observedByDayExerciseId.GetValueOrDefault(exLog.WorkoutDayExerciseId) is { Count: > 0 } observedSets)
            {
                foreach (var set in observedSets
                             .Where(set => set.RepsCompleted > 0)
                             .OrderBy(set => set.SetIndex))
                {
                    totalVolume += set.LoadKg * set.RepsCompleted;
                    dbContext.ExerciseSetLogs.Add(new ExerciseSetLogRecord
                    {
                        Id = Guid.CreateVersion7(),
                        UserId = userId,
                        WorkoutSessionLogId = session.Id,
                        ExerciseId = exLog.ExerciseId.ToString(CultureInfo.InvariantCulture),
                        ExerciseName = exName,
                        SetNumber = set.SetIndex,
                        WeightKg = set.LoadKg,
                        RepsCompleted = set.RepsCompleted,
                        Rpe = set.RirActual is { } rir ? 10m - rir : null,
                        RirActual = set.RirActual,
                        TechniqueAcceptable = set.TechniqueAcceptable,
                        LeftSideReps = set.LeftSideReps,
                        RightSideReps = set.RightSideReps,
                        IsObserved = true,
                        IsWarmup = false,
                        RestTimeSeconds = 90,
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    });
                }

                continue;
            }

            // Legacy aggregate-only clients keep a readable archive row, but these rows are
            // explicitly not observed and never feed double progression.
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
                    IsObserved = false,
                    IsWarmup = false,
                    RestTimeSeconds = 90,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                });
            }
        }

        session.TotalVolumeKg = Math.Round(totalVolume, 1);
    }

    private const string RestWorkoutType = "Rest";
    private const int RescheduleValidationRadiusDays = 3;

    private static void ReorderDay(IEnumerable<WorkoutDayRecord> days, DateOnly date)
    {
        var sessions = days.Where(day => day.Date == date && day.WorkoutType != RestWorkoutType &&
                                         day.ActivityTypeId is { } activity &&
                                         Enum.IsDefined(typeof(WorkoutActivityType), activity))
            .OrderBy(day => day.DayOrder ?? int.MaxValue).ToArray();
        var ordered = WorkoutSessionOrderingRule.Assign(sessions.Select(day => new MixedActivityScheduledSession(
            (WorkoutActivityType)day.ActivityTypeId!.Value, date.DayOfWeek, day.PlannedMainDurationMinutes ?? 0m,
            false)).ToArray());
        foreach (var day in sessions)
            day.DayOrder = ordered.First(item => item.Activity == (WorkoutActivityType)day.ActivityTypeId!.Value).DayOrder;
    }

    /// <summary>
    /// Validates the seven consecutive dates around the target as one week with the shared schedule
    /// rule (two activities per day, one strength, hard-day and HIIT limits, muscle recovery). A
    /// session whose activity or muscles cannot be read fails closed.
    /// </summary>
    private async Task<IReadOnlyList<string>> ValidateRescheduledWindowAsync(Guid userId,
        IReadOnlyCollection<WorkoutDayRecord> days, DateOnly target, CancellationToken cancellationToken)
    {
        var window = days.Where(day => day.Date is { } date && day.WorkoutType != RestWorkoutType &&
                                       date >= target.AddDays(-RescheduleValidationRadiusDays) &&
                                       date <= target.AddDays(RescheduleValidationRadiusDays)).ToArray();
        if (window.Any(day => day.ActivityTypeId is not { } activity ||
                              !Enum.IsDefined(typeof(WorkoutActivityType), activity)))
            return [WorkoutRescheduleRejectedException.ScheduleUnknownCode];
        var dayIds = window.Select(day => day.Id).ToArray();
        var exercises = await dbContext.WorkoutDayExercises.AsNoTracking()
            .Where(row => row.UserId == userId && dayIds.Contains(row.WorkoutDayId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var legacyRows = exercises.Where(row => !UsesDefinitionId(row)).ToArray();
        var legacyCatalog = await LoadCatalogAsync(legacyRows.Select(row => row.ExerciseId), false, cancellationToken);
        var sourceByLegacyId = legacyCatalog.ToDictionary(pair => pair.Key, pair => ExtractPlanningId(pair.Value.Code));
        var sourceIds = sourceByLegacyId.Values.OfType<int>().Distinct().ToArray();
        var definitionBySource = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
            .Where(row => row.SourceExerciseId.HasValue && sourceIds.Contains(row.SourceExerciseId.Value))
            .ToDictionaryAsync(row => row.SourceExerciseId!.Value, row => row.Id, cancellationToken);
        var definitionByRow = exercises.ToDictionary(row => row.Id, row => UsesDefinitionId(row) ? row.ExerciseId :
            sourceByLegacyId.GetValueOrDefault(row.ExerciseId) is { } source ? definitionBySource.GetValueOrDefault(source) : 0);
        var definitionIds = definitionByRow.Values.Distinct().ToArray();
        var musclesByDefinitionId = (await dbContext.ExerciseMuscleContributions.AsNoTracking()
                .Where(row => definitionIds.Contains(row.ExerciseId) && row.Fraction > 0)
                .Select(row => new { row.ExerciseId, row.MuscleGroupId })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .GroupBy(row => row.ExerciseId)
            .ToDictionary(group => group.Key, group => group
                .Select(row => row.MuscleGroupId.ToString(CultureInfo.InvariantCulture)).ToHashSet(StringComparer.Ordinal));
        var sessions = window.Select(day =>
        {
            var activity = (WorkoutActivityType)day.ActivityTypeId!.Value;
            IReadOnlySet<string>? groups = null;
            if (WorkoutPreferencesRule.IsStrengthStyle(activity))
            {
                var dayExercises = exercises.Where(row => row.WorkoutDayId == day.Id).ToArray();
                var set = new HashSet<string>(StringComparer.Ordinal);
                var complete = dayExercises.Length > 0;
                foreach (var row in dayExercises)
                {
                    if (musclesByDefinitionId.TryGetValue(definitionByRow[row.Id], out var values) && values.Count > 0)
                        set.UnionWith(values);
                    else
                        complete = false;
                }
                groups = complete ? set : null;
            }
            return new WorkoutScheduleValidationSession(day.Date!.Value.DayOfWeek, activity,
                MixedActivityWeekScheduleRule.IsHardActivity(activity), groups, day.PlannedMainDurationMinutes);
        }).ToArray();
        return WorkoutScheduleValidationRule.Validate(sessions);
    }

    private async Task<int?> LoadWorkoutActivityTypeIdAsync(
        Guid userId,
        Guid workoutDayId,
        CancellationToken cancellationToken) =>
        await dbContext.WorkoutDays.AsNoTracking()
            .Where(day => day.UserId == userId && day.Id == workoutDayId)
            .Select(day => day.ActivityTypeId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

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
                    "Recovery day",
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
            var visibleDays = activeDays.Count > 0 ? activeDays.OrderBy(day => day.DayOrder).ToList() : [scheduledDays[0]];
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
                        "Recovery day",
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
                    activityCount) { IsOptional = day.IsOptional, IsEasierAlternative = day.IsEasierAlternative, DayOrder = day.DayOrder });
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
                    restDay?.Name ?? "Recovery day",
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
            var sessionItems = activeDays.Select(day =>
            {
                var dayExercises = exercisesByDayId.GetValueOrDefault(day.Id, []);
                var dayTag = BuildPrimaryTag(day, dayExercises, exercisesById);
                return new WorkoutScheduleSessionItem(
                    ToStableWorkoutIntId(day.Id),
                    dayTag,
                    day.Name,
                    day.DayOrder,
                    completedDayIds.Contains(day.Id),
                    day.IsOptional,
                    day.IsEasierAlternative);
            }).ToList();

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
                    : null,
                Sessions = sessionItems
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
        string movementNotes,
        EffectiveExperienceTier progressionTier = EffectiveExperienceTier.Tier1BeginnerOrNew,
        IReadOnlyList<SnapshotProgressionExposure>? observedWeeks = null,
        bool isBodyweight = false,
        GenerationExerciseV2? generationExercise = null,
        IReadOnlyList<ExerciseSetLogRecord>? observedSets = null,
        bool usesBandResistanceOnly = false)
    {
        var name = exercise?.Name ?? "Exercise";
        var usesEndurance = UsesEnduranceMetrics(dayExercise, name);
        var isTimeUnderTension = ContainsAny(name, "plank", "copenhagen");
        var isCycling = ContainsAny(name, "bike", "cycle", "cycling", "ride");
        var isRunning = ContainsAny(name, "run", "jog", "sprint", "tempo");
        var isSwimming = ContainsAny(name, "swim", "pool");
        var isHiit = ContainsAny(name, "hiit", "interval") && !isCycling && !isRunning;
        var isDistanceCardio = isCycling || isRunning || isSwimming ||
            WorkoutActivityTagRule.IsTimedWalk(name, dayExercise.DurationSeconds);

        var activityTag = ClassifyExerciseTag(dayExercise, workoutType, exercise);
        var isLogged = IsExerciseLogComplete(currentLog, dayExercise, name);
        var plannedSets = generationExercise?.WorkingSets ?? dayExercise.Sets;
        var warmupSets = generationExercise?.WarmUpSets ?? 0;
        var minRepetitions = generationExercise?.MinRepetitions ?? dayExercise.Reps;
        var maxRepetitions = generationExercise?.MaxRepetitions ?? dayExercise.Reps;
        var targetRir = generationExercise?.TargetRir ?? dayExercise.TargetRepetitionsInReserve ?? 0;
        var restSeconds = generationExercise?.RestSeconds ?? 90;
        var isUnilateral = generationExercise?.IsUnilateral ?? false;
        var isCalibrationRequired = string.Equals(generationExercise?.LoadStatus,
            SessionLoadStatusV2.CalibrationRequired.ToString(), StringComparison.Ordinal);
        var plannedLoad = generationExercise?.PrescribedLoadKg;

        var recommendation = usesEndurance
            ? StrengthProgressionAdviceRule.Endurance()
            : usesBandResistanceOnly
                ? (previousLog is null
                    ? StrengthProgressionAdviceRule.BandStartingTension()
                    : StrengthProgressionAdviceRule.BandProgressReps())
                : previousLog?.WeightKg is > 0m
                    ? BuildStrengthProgressionRecommendation(dayExercise, exercise, previousLog, observedWeeks,
                        progressionTier, isUnilateral, generationExercise?.MaxRepetitions)
                    : StrengthProgressionAdviceRule.StartingWeight();
        if (!usesEndurance && !usesBandResistanceOnly && string.Equals(generationExercise?.LoadStatus,
                SessionLoadStatusV2.ConservativeReturnLoad.ToString(), StringComparison.Ordinal) && plannedLoad is { } cap)
        {
            var cappedLoad = recommendation.PrescribedLoad is { } advised ? Math.Min(advised, cap) : cap;
            recommendation = recommendation with
            {
                PrescribedLoad = cappedLoad,
                LoadIncreased = false,
                RecommendationCode = recommendation.LoadIncreased
                    ? StrengthProgressionAdviceRule.MaintainCode
                    : recommendation.RecommendationCode
            };
        }
        var progressedLoad = !usesBandResistanceOnly && recommendation.LoadIncreased ? recommendation.PrescribedLoad : null;

        var setsCount = currentLog?.CompletedSets ?? (plannedSets > 0 ? plannedSets : 3);
        var repsCount = currentLog?.CompletedReps ?? (minRepetitions > 0 ? minRepetitions : 10);
        var effectiveWeight = usesBandResistanceOnly
            ? (decimal?)null
            : currentLog?.WeightKg ?? (isCalibrationRequired && !isBodyweight
                ? null
                : progressedLoad ?? plannedLoad ?? previousLog?.WeightKg ?? (isBodyweight ? 0m : (decimal?)null));
        var weightVal = effectiveWeight is { } ew ? (double)ew : 0.0;

        var sets = usesEndurance
            ? Array.Empty<WorkoutSetDetailDto>()
            : BuildSessionSetRows(warmupSets, setsCount, repsCount, weightVal, restSeconds, observedSets);

        var targetText = isTimeUnderTension
            ? (dayExercise.DurationSeconds is > 0 ? $"{dayExercise.DurationSeconds.Value / 60}m under tension" : "Time under tension")
            : usesEndurance
                ? (dayExercise.DurationSeconds is > 0 ? $"{dayExercise.DurationSeconds.Value / 60}m steady" : "Steady effort")
                : (plannedSets > 0 && minRepetitions > 0
                    ? minRepetitions == maxRepetitions
                        ? $"{plannedSets} sets x {minRepetitions} reps"
                        : $"{plannedSets} sets x {minRepetitions}-{maxRepetitions} reps"
                    : "Flexible");

        var previousText = previousLog is null
            ? "No previous log"
            : usesEndurance
                ? "Logged before"
                : usesBandResistanceOnly
                    ? $"Last Band resistance with {previousLog.CompletedSets} x {previousLog.CompletedReps}"
                    : $"Last {(previousLog.WeightKg is > 0m ? $"{previousLog.WeightKg.Value:0.#} kg" : "Bodyweight")} with {previousLog.CompletedSets} x {previousLog.CompletedReps}";

        return new WorkoutExerciseDetailDto(
            ToStableWorkoutIntId(dayExercise.Id),
            dayExercise.ExerciseId,
            name,
            activityTag,
            isLogged,
            sets)
        {
            PlannedSets = plannedSets,
            PlannedWarmUpSets = warmupSets,
            MinRepetitions = minRepetitions,
            MaxRepetitions = maxRepetitions,
            TargetRir = targetRir,
            RestSeconds = restSeconds,
            SupersetGroupId = generationExercise?.SupersetGroupId,
            IsUnilateral = isUnilateral,
            LoadStatus = generationExercise?.LoadStatus ?? string.Empty,
            PrescribedLoadKg = usesBandResistanceOnly ? null : plannedLoad,
            RequiresInitialCalibration = isCalibrationRequired && !isBodyweight && !usesBandResistanceOnly,
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
            RecommendationText = recommendation.RecommendationCode,
            RecommendationCode = recommendation.RecommendationCode,
            RecommendationLoadKg = usesBandResistanceOnly ? null : recommendation.PrescribedLoad ?? plannedLoad,
            MovementNotes = movementNotes,
            LoggedSetsText = currentLog is not null ? currentLog.CompletedSets.ToString(CultureInfo.InvariantCulture) :
                plannedSets > 0 ? plannedSets.ToString(CultureInfo.InvariantCulture) : string.Empty,
            LoggedRepsText = currentLog is not null ? currentLog.CompletedReps.ToString(CultureInfo.InvariantCulture) :
                minRepetitions > 0 ? minRepetitions.ToString(CultureInfo.InvariantCulture) : string.Empty,
            LoggedWeightText = effectiveWeight is { } displayWeight
                ? $"{displayWeight:0.#}"
                : string.Empty,
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
            UsesWeight = !usesEndurance && !usesBandResistanceOnly
        };
    }

    private static WorkoutSetDetailDto[] BuildSessionSetRows(
        int plannedWarmupSets,
        int plannedWorkingSets,
        int plannedRepetitions,
        double plannedWeightKg,
        int restSeconds,
        IReadOnlyList<ExerciseSetLogRecord>? observedSets)
    {
        var actual = observedSets ?? [];
        var actualWarmups = actual.Where(set => set.IsWarmup).OrderBy(set => set.SetNumber).ToArray();
        var actualWorking = actual.Where(set => !set.IsWarmup).OrderBy(set => set.SetNumber).ToArray();
        var warmups = actualWarmups.Length > 0
            ? actualWarmups.Select((set, index) => new WorkoutSetDetailDto(index + 1,
                (double)set.WeightKg, set.RepsCompleted, (double?)set.Rpe, true, set.RestTimeSeconds,
                set.LeftSideReps, set.RightSideReps)
                { IsObserved = true, TechniqueAcceptable = set.TechniqueAcceptable }).ToArray()
            : Enumerable.Range(1, Math.Max(0, plannedWarmupSets))
                .Select(number => new WorkoutSetDetailDto(number, plannedWeightKg, plannedRepetitions,
                    null, true, restSeconds)).ToArray();
        var working = actualWorking.Length > 0
            ? actualWorking.Select((set, index) => new WorkoutSetDetailDto(warmups.Length + index + 1,
                (double)set.WeightKg, set.RepsCompleted, (double?)set.Rpe, false, set.RestTimeSeconds,
                set.LeftSideReps, set.RightSideReps)
                { IsObserved = true, TechniqueAcceptable = set.TechniqueAcceptable }).ToArray()
            : Enumerable.Range(1, Math.Max(1, plannedWorkingSets))
                .Select(number => new WorkoutSetDetailDto(warmups.Length + number, plannedWeightKg,
                    plannedRepetitions, null, false, restSeconds)).ToArray();
        return warmups.Concat(working).ToArray();
    }

    private static StrengthProgressionAdvice BuildStrengthProgressionRecommendation(
        WorkoutDayExerciseRecord dayExercise,
        Exercise? exercise,
        WorkoutExerciseLogRecord previousLog,
        IReadOnlyList<SnapshotProgressionExposure>? observedWeeks,
        EffectiveExperienceTier tier,
        bool isUnilateral,
        int? plannedProgressionReps)
    {
        if (previousLog.WeightKg is not { } currentLoad || currentLoad <= 0m)
            return StrengthProgressionAdviceRule.StartingWeight();

        var muscle = exercise is null
            ? MajorMuscleGroup.Core
            : MapMajorMuscleGroup(exercise.PrimaryMuscle);
        var conservativeWeeks = (observedWeeks ?? []).Select(exposure => exposure with
        {
            IsUnilateral = isUnilateral || exposure.IsUnilateral
        }).ToArray();
        return SnapshotProgressionRule.Resolve(muscle, tier, dayExercise.ExerciseId,
            plannedProgressionReps ?? dayExercise.Reps, currentLoad, conservativeWeeks);
    }

    private static EffectiveExperienceTier ResolveExperienceTier(UserProfileRecord? profile) =>
        Enum.TryParse<TrainingExperienceLevel>(profile?.TrainingExperienceLevel, ignoreCase: true, out var level)
            ? level switch
            {
                TrainingExperienceLevel.Beginner => EffectiveExperienceTier.Tier1BeginnerOrNew,
                TrainingExperienceLevel.Intermediate => EffectiveExperienceTier.Tier2IntermediateOrEstablished,
                TrainingExperienceLevel.Advanced => EffectiveExperienceTier.Tier3Advanced,
                _ => EffectiveExperienceTier.Tier1BeginnerOrNew
            }
            : EffectiveExperienceTier.Tier1BeginnerOrNew;

    private static MajorMuscleGroup MapMajorMuscleGroup(MuscleGroup muscle) => muscle switch
    {
        MuscleGroup.Chest => MajorMuscleGroup.Chest,
        MuscleGroup.UpperBack or MuscleGroup.LowerBack or MuscleGroup.Lats or
            MuscleGroup.MidBack or MuscleGroup.Erectors => MajorMuscleGroup.Back,
        MuscleGroup.FrontShoulders or MuscleGroup.SideShoulders or
            MuscleGroup.RearShoulders => MajorMuscleGroup.Shoulders,
        MuscleGroup.Quads or MuscleGroup.HipFlexors or
            MuscleGroup.Adductors => MajorMuscleGroup.Quads,
        MuscleGroup.Glutes or MuscleGroup.GluteMax or MuscleGroup.GluteMed => MajorMuscleGroup.Glutes,
        MuscleGroup.Hamstrings => MajorMuscleGroup.Hamstrings,
        MuscleGroup.Biceps or MuscleGroup.Triceps or MuscleGroup.Forearms or
            MuscleGroup.Grip => MajorMuscleGroup.Arms,
        MuscleGroup.Calves => MajorMuscleGroup.Calves,
        MuscleGroup.Abs or MuscleGroup.Obliques or MuscleGroup.Core => MajorMuscleGroup.Core,
        _ => MajorMuscleGroup.Core
    };

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
                string.Empty, [])
            {
                Prescription = ActivityOccurrencePrescription.Deserialize(day.PlannedActivityPrescriptionJson),
                ActivityType = ResolveActivityType(day, tag)
            }];
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
                group.Select(e => e.WorkoutDayExerciseId).ToArray())
            {
                ActivityType = ResolveActivityType(day, tag)
            };
        }).ToArray();
    }

    private static WorkoutActivityDto ResolveActivityType(WorkoutDayRecord day, string tag)
    {
        if (day.ActivityTypeId is { } typeId && Enum.IsDefined(typeof(WorkoutActivityDto), typeId))
            return (WorkoutActivityDto)typeId;

        return tag switch
        {
            StrengthTag => WorkoutActivityDto.GeneralStrength,
            CardioTag => WorkoutActivityDto.Other,
            RecoveryTag => WorkoutActivityDto.GentleRecovery,
            _ => WorkoutActivityDto.Other
        };
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
        WorkoutActivityTagRule.UsesEnduranceMetrics(name, dayExercise.DurationSeconds);

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
        Exercise? catalogExercise) =>
        WorkoutActivityTagRule.Classify(workoutType, catalogExercise?.Name, exercise.DurationSeconds);

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

    private static bool ContainsAny(string value, params string[] terms)
    {
        // Match activity word stems (run/running), never fragments inside strength names (crunch).
        var words = value.Split(value.Where(character => !char.IsLetterOrDigit(character)).Distinct().ToArray(),
            StringSplitOptions.RemoveEmptyEntries);
        return words.Any(word => terms.Any(term => word.StartsWith(term, StringComparison.OrdinalIgnoreCase)));
    }
}
