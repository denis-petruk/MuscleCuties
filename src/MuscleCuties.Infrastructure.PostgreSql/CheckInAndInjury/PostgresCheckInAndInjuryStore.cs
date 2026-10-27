using System.Data;
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

            var isNewInjury = row is null;
            var wasAlreadyCleared = row is not null && string.Equals(row.Status, ClearedStatus, StringComparison.OrdinalIgnoreCase);
            if (isClearing && row is null)
                throw new InvalidOperationException("The injury must exist before it can be cleared.");
            var originalClearanceDate = wasAlreadyCleared ? row!.ClearedDate :
                isClearing ? await GetAccountLocalDateAsync(request.UserId, cancellationToken).ConfigureAwait(false) : null;

            var episode = await dbContext.WorkoutInjuryEpisodes.SingleOrDefaultAsync(item => item.UserId == request.UserId &&
                item.InjuryLogId == request.Id && item.ClearedOn == null, cancellationToken).ConfigureAwait(false);
            if (!isClearing && (isNewInjury || wasAlreadyCleared))
            {
                if (episode is not null) throw new InvalidOperationException("The previous injury episode is still active.");
                episode = new WorkoutInjuryEpisodeRecord
                {
                    Id = Guid.CreateVersion7(), UserId = request.UserId, InjuryLogId = request.Id,
                    SiteFlag = request.SiteFlag, StartedOn = request.Since
                };
                dbContext.WorkoutInjuryEpisodes.Add(episode);
            }
            else if ((episode is not null && episode.SiteFlag != request.SiteFlag) ||
                     (isClearing && row is not null && row.SiteFlag != request.SiteFlag))
                throw new InvalidOperationException("Record a separate injury for a different site.");
            if (isClearing && !wasAlreadyCleared && episode is not null)
            {
                var calendar = await dbContext.WorkoutAccountPlanningStates.AsNoTracking().SingleAsync(
                    state => state.UserId == request.UserId, cancellationToken).ConfigureAwait(false);
                episode.ClearedOn = originalClearanceDate;
                episode.ClearanceTimeZoneId = calendar.TimeZoneId;
                episode.ClearanceTimeZoneVersion = calendar.TimeZoneVersion;
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
                await SuppressFutureStrengthForInjuryAsync(request.UserId, request.Id, episode?.Id, cancellationToken).ConfigureAwait(false);

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

    private async Task SuppressFutureStrengthForInjuryAsync(Guid userId, Guid injuryId, Guid? episodeId,
        CancellationToken cancellationToken)
    {
        var activePlanId = await dbContext.WorkoutPlans.AsNoTracking()
            .Where(plan => plan.UserId == userId && plan.IsActive).Select(plan => (Guid?)plan.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (activePlanId is null) return;
        var zoneId = await dbContext.WorkoutAccountPlanningStates.AsNoTracking()
            .Where(state => state.UserId == userId).Select(state => state.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (zoneId is null) throw new InvalidOperationException("Establish the account timezone before recording this injury.");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId); }
        catch (TimeZoneNotFoundException) { throw new InvalidOperationException("Establish the account timezone before recording this injury."); }
        catch (InvalidTimeZoneException) { throw new InvalidOperationException("Establish the account timezone before recording this injury."); }
        var today = WorkoutAccountCalendarRule.LocalDate(timeProvider.GetUtcNow(), zone);
        var days = await dbContext.WorkoutDays.Where(day => day.UserId == userId && day.WorkoutPlanId == activePlanId &&
                day.Date >= today && day.WorkoutType == nameof(WorkoutType.Strength) &&
                !dbContext.WorkoutCompletionDayCredits.Any(credit => credit.UserId == userId &&
                    credit.SourceWorkoutLogId != null && dbContext.WorkoutLogs.Any(log =>
                        log.UserId == userId && log.Id == credit.SourceWorkoutLogId && log.WorkoutDayId == day.Id)) &&
                !dbContext.WorkoutSessionLogs.Any(session => session.UserId == userId &&
                    session.PlannedWorkoutDayId == day.Id && session.CompletedAt != null))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (episodeId is { } observedEpisodeId && days.Count > 0)
        {
            var dayIds = days.Select(day => day.Id).ToArray();
            var prescriptions = await dbContext.WorkoutDayExercises.AsNoTracking()
                .Where(exercise => exercise.UserId == userId && dayIds.Contains(exercise.WorkoutDayId))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var alreadyCaptured = await dbContext.WorkoutInjuryPrescriptions.AsNoTracking()
                .Where(item => item.UserId == userId && item.EpisodeId == observedEpisodeId)
                .Select(item => item.OriginalWorkoutDayExerciseId).ToListAsync(cancellationToken).ConfigureAwait(false);
            var known = alreadyCaptured.ToHashSet();
            foreach (var original in prescriptions.Where(item => !known.Contains(item.Id)))
                dbContext.WorkoutInjuryPrescriptions.Add(new WorkoutInjuryPrescriptionRecord
                {
                    Id = Guid.CreateVersion7(), UserId = userId, EpisodeId = observedEpisodeId,
                    WorkoutDayId = original.WorkoutDayId, OriginalWorkoutDayExerciseId = original.Id,
                    OriginalExerciseId = original.ExerciseId, OriginalPlanningSlotId = original.PlanningSlotId,
                    OriginalOriginWorkoutDayExerciseId = original.OriginWorkoutDayExerciseId,
                    OriginalSets = original.Sets, OriginalOptionalSets = original.OptionalSets,
                    OriginalReps = original.Reps, OriginalDurationSeconds = original.DurationSeconds,
                    OriginalIsOptional = original.IsOptional, OriginalIsRemovedByReadiness = original.IsRemovedByReadiness
                });
        }
        foreach (var day in days)
        {
            day.IsRemovedBySafety = true;
            day.SafetyInjuryId ??= injuryId;
        }
    }

    private async Task<DateOnly> GetAccountLocalDateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var zoneId = await dbContext.WorkoutAccountPlanningStates.AsNoTracking()
            .Where(state => state.UserId == userId).Select(state => state.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (zoneId is null) throw new InvalidOperationException("Establish the account timezone before clearing this injury.");
        try { return WorkoutAccountCalendarRule.LocalDate(timeProvider.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(zoneId)); }
        catch (TimeZoneNotFoundException) { throw new InvalidOperationException("Establish the account timezone before clearing this injury."); }
        catch (InvalidTimeZoneException) { throw new InvalidOperationException("Establish the account timezone before clearing this injury."); }
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
