using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.CheckInAndInjury;

public sealed class PostgresCheckInAndInjuryStore(PostgresDbContext dbContext, TimeProvider timeProvider)
    : ICheckInAndInjuryStore
{
    private const int SleepWindowLookbackDays = 2;
    private const int StepsWindowLookbackDays = 6;
    private const string ClearedStatus = "Cleared";

    public async Task<IReadOnlyList<DailyCheckInRecord>> LoadCheckInsAsync(
        Guid userId,
        DateOnly from,
        DateOnly through,
        CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            var rows = await dbContext.DailyReadinessLogs.AsNoTracking()
                .Where(row => row.UserId == userId && row.Date >= from && row.Date <= through)
                .OrderBy(row => row.Date)
                .ThenBy(row => row.Id)
                .Select(row => MapCheckIn(row))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return (IReadOnlyList<DailyCheckInRecord>)rows;
        }).ConfigureAwait(false);

    public async Task<DailyCheckInRecord> SaveCheckInAsync(
        SaveDailyCheckInRequest request,
        CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, request.UserId, cancellationToken)
                .ConfigureAwait(false);
            await LockRecordIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

            var row = await dbContext.DailyReadinessLogs.SingleOrDefaultAsync(
                    item => item.UserId == request.UserId && item.Id == request.Id,
                    cancellationToken)
                .ConfigureAwait(false);
            var sameDateRow = await dbContext.DailyReadinessLogs.FirstOrDefaultAsync(
                    item => item.UserId == request.UserId && item.Date == request.Date && item.Id != request.Id,
                    cancellationToken)
                .ConfigureAwait(false);
            if (sameDateRow is not null)
                throw new CheckInAndInjuryDateConflictException();

            var recentLogs = await dbContext.DailyReadinessLogs.AsNoTracking()
                .Where(item => item.UserId == request.UserId &&
                               item.Id != request.Id &&
                               item.Date >= request.Date.AddDays(-StepsWindowLookbackDays) &&
                               item.Date < request.Date)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var sleepSamples = recentLogs
                .Where(item => item.Date >= request.Date.AddDays(-SleepWindowLookbackDays))
                .Select(item => item.SleepHours)
                .Append(request.SleepHours)
                .ToArray();
            var computedSleep3dAvg = CheckInCoverageRule.AverageSleep(sleepSamples);

            var stepSamples = recentLogs
                .Select(item => item.StepsYesterday)
                .Append(request.StepsYesterday)
                .ToArray();
            var computedSteps7dAvg = CheckInCoverageRule.AverageSteps(stepSamples);

            var profile = await dbContext.UserProfiles.AsNoTracking()
                .SingleOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken)
                .ConfigureAwait(false);
            var reproductiveContext = ReproductiveContextCodec.Parse(profile?.ReproductiveContextJson);
            var requestedPhase = TryParsePhase(request.Phase ?? profile?.CurrentCyclePhase);
            var effectivePhase = reproductiveContext.AllowsCyclePhaseTrainingAdjustments
                ? requestedPhase
                : CyclePhase.Unknown;

            var sleep3dAvg = request.Sleep3dAvg ?? computedSleep3dAvg;
            var steps7dAvg = request.Steps7dAvg ?? computedSteps7dAvg;
            var dailyInputs = new DailyInputs(
                request.Date,
                (double)(request.SleepHours ?? 0m),
                (double)(sleep3dAvg ?? 0m),
                request.StepsYesterday ?? 0,
                steps7dAvg ?? 0,
                request.Energy ?? 1,
                request.Pain ?? 3,
                request.Bloating ?? false,
                (double?)request.WeightKg,
                SleepQuality: request.SleepQuality is { } sq ? (SleepQuality)sq : null,
                Soreness: request.Soreness is { } s ? (SorenessLevel)s : null,
                PreviousDayLoad: request.PreviousDayLoad is { } load
                    ? new WorkoutLoad(WorkoutLoadKind.AggregateOnly, (double)load)
                    : null);
            var readiness = ReadinessRule.ComputeForCheckInCoverage(
                dailyInputs,
                request.SleepHours.HasValue,
                request.Energy.HasValue,
                request.Pain.HasValue,
                effectivePhase,
                WorkoutPlanningConfig.CreateDefault());

            row ??= new DailyReadinessLogRecord
            {
                Id = request.Id,
                UserId = request.UserId,
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime
            };
            row.Date = request.Date;
            row.SleepHours = request.SleepHours;
            row.Sleep3dAvg = sleep3dAvg;
            row.StepsYesterday = request.StepsYesterday;
            row.Steps7dAvg = steps7dAvg;
            row.Energy = request.Energy;
            row.Pain = request.Pain;
            row.Bloating = request.Bloating;
            row.WeightKg = request.WeightKg;
            row.SleepQuality = request.SleepQuality;
            row.Soreness = request.Soreness;
            row.PlanningSleepQuality = request.PlanningSleepQuality;
            row.PlanningSoreness = request.PlanningSoreness;
            row.PlanningStress = request.PlanningStress;
            row.PlanningTimeZoneId = request.PlanningTimeZoneId;
            row.PlanningRecordedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            row.PlanningRevision = checked(row.PlanningRevision + 1);
            row.PreviousDayLoad = request.PreviousDayLoad;
            row.ReadinessScore = readiness.Score;
            row.ReadinessTier = readiness.Tier;
            row.Phase = effectivePhase.ToString();

            if (dbContext.Entry(row).State == EntityState.Detached)
                dbContext.DailyReadinessLogs.Add(row);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException exception) when (IsUniqueIdCollision(exception))
            {
                throw new CheckInAndInjuryIdConflictException();
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return MapCheckIn(row);
        }).ConfigureAwait(false);

    public async Task<bool> DeleteCheckInAsync(
        Guid userId,
        Guid checkInId,
        CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            var row = await dbContext.DailyReadinessLogs.SingleOrDefaultAsync(
                    item => item.UserId == userId && item.Id == checkInId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            dbContext.DailyReadinessLogs.Remove(row);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);

    public async Task<IReadOnlyList<InjuryObservationRecord>> LoadInjuriesAsync(
        Guid userId,
        bool activeOnly,
        CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            var query = dbContext.WorkoutInjuryLogs.AsNoTracking().Where(row => row.UserId == userId);
            if (activeOnly)
                query = query.Where(row => row.Status.ToLower() != ClearedStatus.ToLower());

            var rows = await query
                .OrderByDescending(row => row.Date)
                .ThenBy(row => row.Id)
                .Select(row => MapInjury(row))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return (IReadOnlyList<InjuryObservationRecord>)rows;
        }).ConfigureAwait(false);

    public async Task<InjuryObservationRecord> SaveInjuryAsync(
        SaveInjuryObservationRequest request,
        CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, request.UserId, cancellationToken)
                .ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(dbContext, request.UserId, cancellationToken)
                .ConfigureAwait(false);
            var isClearing = string.Equals(request.Status, ClearedStatus, StringComparison.OrdinalIgnoreCase);
            if (!isClearing && await dbContext.WorkoutSessionLogs.AnyAsync(
                    session => session.UserId == request.UserId && !session.IsDeleted && session.CompletedAt == null,
                    cancellationToken).ConfigureAwait(false))
                throw new ActiveWorkoutPreventsInjuryException();
            await LockRecordIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

            var row = await dbContext.WorkoutInjuryLogs.SingleOrDefaultAsync(
                    item => item.UserId == request.UserId && item.Id == request.Id,
                    cancellationToken)
                .ConfigureAwait(false);

            var wasAlreadyCleared = row is not null && string.Equals(row.Status, ClearedStatus, StringComparison.OrdinalIgnoreCase);
            DateOnly? originalClearanceDate = isClearing
                ? wasAlreadyCleared ? row!.ClearedDate : await AccountLocalDateAsync(request.UserId, cancellationToken).ConfigureAwait(false)
                : null;

            var openEpisodes = await dbContext.WorkoutInjuryEpisodes.Where(episode =>
                    episode.UserId == request.UserId && episode.SourceInjuryId == request.Id &&
                    episode.ClearedDate == null && !episode.SourceDeleted)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            if (isClearing)
            {
                foreach (var episode in openEpisodes)
                    episode.ClearedDate = originalClearanceDate;
            }
            else
            {
                // A site change cannot retroactively clear the previous episode.
                foreach (var episode in openEpisodes.Where(episode => episode.SiteFlag != request.SiteFlag))
                    episode.SourceDeleted = true;
                if (!openEpisodes.Any(episode => episode.SiteFlag == request.SiteFlag))
                    dbContext.WorkoutInjuryEpisodes.Add(new WorkoutInjuryEpisodeRecord
                    {
                        Id = Guid.NewGuid(), UserId = request.UserId, SourceInjuryId = request.Id,
                        SiteFlag = request.SiteFlag, RecordedAtUtc = timeProvider.GetUtcNow().UtcDateTime
                    });
            }

            row ??= new WorkoutInjuryLogRecord
            {
                Id = request.Id,
                UserId = request.UserId,
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime
            };
            row.SiteFlag = request.SiteFlag;
            row.Status = request.Status;
            row.Since = request.Since;
            row.Pain = request.Pain;
            row.Date = request.Date;
            row.ClearedDate = isClearing ? originalClearanceDate : null;

            if (dbContext.Entry(row).State == EntityState.Detached)
                dbContext.WorkoutInjuryLogs.Add(row);

            if (!isClearing)
                await SuppressFutureStrengthForInjuryAsync(request.UserId, request.Id, cancellationToken).ConfigureAwait(false);

            await MarkSafetyRegenerationPendingAsync(request.UserId, cancellationToken).ConfigureAwait(false);
            await InvalidatePlanningPreviewsAsync(request.UserId, cancellationToken).ConfigureAwait(false);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException exception) when (IsUniqueIdCollision(exception))
            {
                throw new CheckInAndInjuryIdConflictException();
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return MapInjury(row);
        }).ConfigureAwait(false);

    public async Task<bool> DeleteInjuryAsync(
        Guid userId,
        Guid injuryId,
        CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            var row = await dbContext.WorkoutInjuryLogs.SingleOrDefaultAsync(
                    item => item.UserId == userId && item.Id == injuryId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            var episodes = await dbContext.WorkoutInjuryEpisodes.Where(episode => episode.UserId == userId &&
                    episode.SourceInjuryId == injuryId && !episode.SourceDeleted)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var episode in episodes) episode.SourceDeleted = true;
            dbContext.WorkoutInjuryLogs.Remove(row);
            await MarkSafetyRegenerationPendingAsync(userId, cancellationToken).ConfigureAwait(false);
            await InvalidatePlanningPreviewsAsync(userId, cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);

    private async Task InvalidatePlanningPreviewsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var proposals = await dbContext.WorkoutPlanProposalsV02
            .Where(item => item.UserId == userId && item.Status == "Proposed" && item.LeaseId != null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var proposal in proposals)
        {
            proposal.Status = "Expired";
            proposal.DecidedAtUtc = now;
            proposal.LeaseExpiresAtUtc = now;
            proposal.ConcurrencyVersion++;
        }
    }

    private async Task MarkSafetyRegenerationPendingAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await dbContext.WorkoutPlans.AnyAsync(plan => plan.UserId == userId && plan.IsActive, cancellationToken)
                .ConfigureAwait(false))
            return;
        var state = await dbContext.WorkoutAccountPlanningStates.SingleOrDefaultAsync(item => item.UserId == userId,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Establish the account timezone before changing an injury.");
        state.SafetyRegenerationPending = true;
    }

    private async Task SuppressFutureStrengthForInjuryAsync(Guid userId, Guid injuryId, CancellationToken cancellationToken)
    {
        var activePlanId = await dbContext.WorkoutPlans.AsNoTracking()
            .Where(plan => plan.UserId == userId && plan.IsActive).Select(plan => (Guid?)plan.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (activePlanId is null) return;
        var today = await AccountLocalDateAsync(userId, cancellationToken).ConfigureAwait(false);
        var days = await dbContext.WorkoutDays.Where(day => day.UserId == userId && day.WorkoutPlanId == activePlanId &&
                day.Date >= today && day.WorkoutType == nameof(WorkoutType.Strength) &&
                !dbContext.WorkoutCompletionDayCredits.Any(credit => credit.UserId == userId &&
                    credit.SourceWorkoutLogId != null && dbContext.WorkoutLogs.Any(log =>
                        log.UserId == userId && log.Id == credit.SourceWorkoutLogId && log.WorkoutDayId == day.Id)) &&
                !dbContext.WorkoutSessionLogs.Any(session => session.UserId == userId &&
                    session.PlannedWorkoutDayId == day.Id && session.CompletedAt != null))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var day in days)
        {
            day.IsRemovedBySafety = true;
            day.SafetyInjuryId ??= injuryId;
        }
        await LinkKnownAffectedPrescriptionsAsync(userId, activePlanId.Value, days, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task LinkKnownAffectedPrescriptionsAsync(Guid userId, Guid planId,
        IReadOnlyList<WorkoutDayRecord> suppressedDays, CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.WorkoutPlanRevisionsV02.AsNoTracking()
            .Where(revision => revision.UserId == userId && revision.PlanId == planId)
            .OrderBy(revision => revision.RevisionNumber).Select(revision => revision.PlanJson)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var originals = ReadUnambiguousOriginals(snapshot);
        if (originals.Count == 0) return;
        var dayIds = suppressedDays.Select(day => day.Id).ToArray();
        var protectedDayIds = (await dbContext.WorkoutLogs.AsNoTracking()
            .Where(log => log.UserId == userId && dayIds.Contains(log.WorkoutDayId))
            .Select(log => log.WorkoutDayId).ToArrayAsync(cancellationToken).ConfigureAwait(false))
            .Concat(await dbContext.WorkoutSessionLogs.AsNoTracking()
                .Where(session => session.UserId == userId && session.PlannedWorkoutDayId != null &&
                    dayIds.Contains(session.PlannedWorkoutDayId.Value))
                .Select(session => session.PlannedWorkoutDayId!.Value).ToArrayAsync(cancellationToken).ConfigureAwait(false))
            .ToHashSet();
        var daysById = suppressedDays.Where(day => !protectedDayIds.Contains(day.Id)).ToDictionary(day => day.Id);
        var unloggedDayIds = daysById.Keys.ToArray();
        var exercises = await dbContext.WorkoutDayExercises.Where(exercise => exercise.UserId == userId &&
                unloggedDayIds.Contains(exercise.WorkoutDayId) &&
                !dbContext.WorkoutExerciseLogs.Any(log => log.UserId == userId && log.WorkoutDayExerciseId == exercise.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (exercises.Count == 0) return;
        var definitionIds = exercises.Select(exercise => exercise.InjuryOriginalExerciseId ?? exercise.ExerciseId).Distinct().ToArray();
        // The caller already holds the account lock; retain the catalog lock until commit.
        await PlanningCatalogFingerprint.LockCatalogAsync(dbContext, cancellationToken).ConfigureAwait(false);
        var definitions = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
            .Where(definition => definitionIds.Contains(definition.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var reviewed = definitions.Where(definition => WorkoutExerciseV020CatalogMapper.Map(definition).MetadataGaps.Count == 0)
            .ToDictionary(definition => definition.Id);
        var episodes = await dbContext.WorkoutInjuryEpisodes.Where(episode => episode.UserId == userId &&
                episode.ClearedDate == null && !episode.SourceDeleted)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        episodes.AddRange(dbContext.ChangeTracker.Entries<WorkoutInjuryEpisodeRecord>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.UserId == userId)
            .Select(entry => entry.Entity));
        episodes = episodes.Where(episode => !episode.SourceDeleted && episode.ClearedDate is null)
            .DistinctBy(episode => episode.Id).ToList();
        var ambiguousSources = episodes.GroupBy(episode => episode.SourceInjuryId)
            .Where(group => group.Count() != 1).Select(group => group.Key).ToHashSet();
        var exerciseIds = exercises.Select(exercise => exercise.Id).ToArray();
        var existingLinks = await dbContext.WorkoutInjuryPrescriptionLinks.AsNoTracking()
            .Where(link => link.UserId == userId && exerciseIds.Contains(link.WorkoutDayExerciseId))
            .Select(link => new { link.WorkoutDayExerciseId, link.InjuryEpisodeId }).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var exercise in exercises)
        {
            var originalId = exercise.InjuryOriginalExerciseId ?? exercise.ExerciseId;
            var day = daysById[exercise.WorkoutDayId];
            if (!originals.Contains(((DayOfWeek)day.DayOfWeek, originalId)) ||
                exercises.Count(other => other.WorkoutDayId == day.Id &&
                    (other.InjuryOriginalExerciseId ?? other.ExerciseId) == originalId) != 1 ||
                !reviewed.TryGetValue(originalId, out var definition)) continue;
            foreach (var episode in episodes.Where(episode => !ambiguousSources.Contains(episode.SourceInjuryId) &&
                (definition.Contraindications & (InjuryFlag)episode.SiteFlag) != InjuryFlag.None))
            {
                if (existingLinks.Any(link => link.WorkoutDayExerciseId == exercise.Id && link.InjuryEpisodeId == episode.Id)) continue;
                exercise.InjuryOriginalExerciseId = originalId;
                dbContext.WorkoutInjuryPrescriptionLinks.Add(new WorkoutInjuryPrescriptionLinkRecord
                {
                    Id = Guid.NewGuid(), UserId = userId, WorkoutDayExerciseId = exercise.Id,
                    InjuryEpisodeId = episode.Id, OriginalExerciseId = originalId
                });
            }
        }
    }

    private static HashSet<(DayOfWeek Weekday, int ExerciseId)> ReadUnambiguousOriginals(string? json)
    {
        var result = new HashSet<(DayOfWeek, int)>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("proposal", out var proposal) ||
                !proposal.TryGetProperty("sessions", out var sessions) || sessions.ValueKind != JsonValueKind.Array ||
                !document.RootElement.TryGetProperty("weekdayAssignments", out var assignments) ||
                assignments.ValueKind != JsonValueKind.Array) return result;
            var assignmentList = new List<(int Session, DayOfWeek Weekday)>();
            foreach (var assignment in assignments.EnumerateArray())
            {
                if (!assignment.TryGetProperty("sessionIndex", out var index) || index.ValueKind != JsonValueKind.Number || !index.TryGetInt32(out var session) ||
                    !assignment.TryGetProperty("weekday", out var weekday) || weekday.ValueKind != JsonValueKind.Number || !weekday.TryGetInt32(out var day) ||
                    !Enum.IsDefined(typeof(DayOfWeek), day)) return [];
                assignmentList.Add((session, (DayOfWeek)day));
            }
            foreach (var session in sessions.EnumerateArray())
            {
                if (session.ValueKind != JsonValueKind.Object || !session.TryGetProperty("sessionIndex", out var index) ||
                    index.ValueKind != JsonValueKind.Number || !index.TryGetInt32(out _) ||
                    !session.TryGetProperty("slots", out var slots) || slots.ValueKind != JsonValueKind.Array) return [];
                foreach (var slot in slots.EnumerateArray())
                    if (slot.ValueKind != JsonValueKind.Object || !slot.TryGetProperty("exerciseId", out var id) ||
                        (id.ValueKind != JsonValueKind.Null &&
                         (id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out _)))) return [];
            }
            foreach (var assignment in assignmentList)
            {
                if (assignmentList.Count(item => item.Session == assignment.Session || item.Weekday == assignment.Weekday) != 1) continue;
                var matchingSessions = sessions.EnumerateArray().Where(session =>
                    session.ValueKind == JsonValueKind.Object && session.TryGetProperty("sessionIndex", out var index) && index.ValueKind == JsonValueKind.Number && index.TryGetInt32(out var value) &&
                    value == assignment.Session).ToArray();
                if (matchingSessions.Length != 1 || !matchingSessions[0].TryGetProperty("slots", out var slots) ||
                    slots.ValueKind != JsonValueKind.Array) continue;
                var ids = new List<int>();
                foreach (var slot in slots.EnumerateArray())
                    if (slot.ValueKind == JsonValueKind.Object && slot.TryGetProperty("exerciseId", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out var value)) ids.Add(value);
                foreach (var id in ids.Where(id => ids.Count(candidate => candidate == id) == 1))
                    result.Add((assignment.Weekday, id));
            }
        }
        catch (JsonException) { return []; }
        catch (InvalidOperationException) { return []; }
        return result;
    }

    private async Task<DateOnly> AccountLocalDateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var zoneId = await dbContext.WorkoutAccountPlanningStates.AsNoTracking()
            .Where(state => state.UserId == userId).Select(state => state.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(zoneId))
            throw new InvalidOperationException("Establish the account timezone before changing an injury.");
        try
        {
            return WorkoutAccountCalendarRule.LocalDate(timeProvider.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(zoneId));
        }
        catch (TimeZoneNotFoundException)
        {
            throw new InvalidOperationException("Establish the account timezone before changing an injury.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new InvalidOperationException("Establish the account timezone before changing an injury.");
        }
    }

    private static DailyCheckInRecord MapCheckIn(DailyReadinessLogRecord row) =>
        new(
            row.Id,
            row.Date,
            row.SleepHours,
            row.Sleep3dAvg,
            row.StepsYesterday,
            row.Steps7dAvg,
            row.Energy,
            row.Pain,
            row.Bloating,
            row.WeightKg,
            row.SleepQuality,
            row.Soreness,
            row.PreviousDayLoad,
            row.ReadinessScore,
            row.ReadinessTier.ToString(),
            row.Phase,
            row.PlanningSleepQuality,
            row.PlanningSoreness,
            row.PlanningStress,
            row.PlanningTimeZoneId,
            row.PlanningRecordedAtUtc is { } recorded ? new DateTimeOffset(DateTime.SpecifyKind(recorded, DateTimeKind.Utc)) : null,
            row.PlanningRevision);

    private static InjuryObservationRecord MapInjury(WorkoutInjuryLogRecord row) =>
        new(
            row.Id,
            row.SiteFlag,
            row.Status,
            row.Since,
            row.Pain,
            row.Date,
            !string.Equals(row.Status, ClearedStatus, StringComparison.OrdinalIgnoreCase));

    private static CyclePhase TryParsePhase(string? phase) =>
        Enum.TryParse<CyclePhase>(phase, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : CyclePhase.Unknown;

    private Task<int> LockRecordIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString("D")}, 0))",
            cancellationToken);

    private static bool IsUniqueIdCollision(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
