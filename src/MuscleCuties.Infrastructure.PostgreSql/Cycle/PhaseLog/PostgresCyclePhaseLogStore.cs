using System.Data;
using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.UseCases.Features.Cycle.PhaseLog;
using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.Infrastructure.PostgreSql.Cycle.PhaseLog;

/// <summary>
/// Writes a manual phase edit and its profile/cycle alignment atomically. LoggedAt is UTC midnight
/// for LocalDate; LocalDate is the authoritative user-calendar value and never passes through a timezone.
/// </summary>
public sealed class PostgresCyclePhaseLogStore(PostgresDbContext db, TimeProvider timeProvider) : ICyclePhaseLogStore
{
    private const int MinimumSupportedYear = 1970;
    private const int MaximumSupportedYear = 2100;
    private const int DefaultCycleLength = 28;
    private const int UnknownCycleLength = 0;
    private const long OwnerEditLockSalt = 741092;
    private const int MaximumNoteLength = 1000;

    /// <summary>Saves one day's phase log, aligning the cycle it belongs to.</summary>
    public async Task<SaveCyclePhaseLogOutput> UpsertAsync(
        SaveCyclePhaseLogInput input,
        CancellationToken cancellationToken)
    {
        if (input is null)
            return Invalid();

        MuscleCuties.Core.Models.Enums.Cycle.CyclePhase phase;
        MuscleCuties.Core.Models.Enums.Cycle.FlowIntensity? flowIntensity;
        try
        {
            phase = CycleContractMapper.ToCore(input.Phase);
            flowIntensity = input.FlowIntensity is { } requestedFlow
                ? CycleContractMapper.ToCore(requestedFlow)
                : null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return Invalid();
        }

        if (!Guid.TryParse(input.OwnerId, out var ownerId) || ownerId == Guid.Empty ||
            !IsSupportedDate(input.LocalDate) || !IsKnownPhase(phase) ||
            (flowIntensity is { } flow && !Enum.IsDefined(flow)))
            return Invalid();

        var normalizedNote = NormalizeNote(input.Note);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(db, ownerId, cancellationToken).ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(db, ownerId, cancellationToken).ConfigureAwait(false);
            await LockOwnerEditAsync(ownerId, cancellationToken).ConfigureAwait(false);

            try
            {
                var profile = await db.UserProfiles.SingleOrDefaultAsync(row => row.UserId == ownerId, cancellationToken)
                    .ConfigureAwait(false);
                var cycles = await db.CycleLogs.Where(row => row.UserId == ownerId)
                    .OrderBy(row => row.StartDate).ThenBy(row => row.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
                var logs = await db.CyclePhaseLogs.Where(row => row.UserId == ownerId)
                    .OrderBy(row => row.LocalDate).ThenBy(row => row.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

                var cycle = AlignCycle(ownerId, phase, input.LocalDate, cycles, profile);
                if (cycle is null)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Invalid();
                }
                if (profile is not null)
                {
                    var phaseValue = logs.LastOrDefault(row => row.LocalDate > input.LocalDate)?.Phase
                                     ?? phase.ToString();
                    if (profile.CurrentCyclePhase != phaseValue)
                    {
                        profile.CurrentCyclePhase = phaseValue;
                        profile.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
                    }
                }

                var row = logs.SingleOrDefault(item => item.LocalDate == input.LocalDate);
                if (row is null)
                {
                    row = new CyclePhaseLogRecord
                    {
                        Id = Guid.NewGuid(),
                        UserId = ownerId,
                        LocalDate = input.LocalDate,
                        LoggedAt = UtcMidnight(input.LocalDate),
                        CreatedAt = timeProvider.GetUtcNow().UtcDateTime
                    };
                    db.CyclePhaseLogs.Add(row);
                }

                row.CycleLogId = cycle.Id;
                row.Phase = phase.ToString();
                if (flowIntensity.HasValue)
                    row.FlowIntensity = flowIntensity.Value.ToString();
                row.Note = normalizedNote;
                row.CreatedAt = timeProvider.GetUtcNow().UtcDateTime;
                await PostgresCycleReadinessReconciler.MarkPendingAsync(db, ownerId, cancellationToken).ConfigureAwait(false);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                await PostgresCycleReadinessReconciler.TryReconcileAfterWriteAsync(db, ownerId,
                    timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                return new SaveCyclePhaseLogOutput(SaveCyclePhaseLogOutcomeKind.Saved);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception) || exception.InnerException is PostgresException)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new SaveCyclePhaseLogOutput(SaveCyclePhaseLogOutcomeKind.Conflict);
            }
        }).ConfigureAwait(false);
    }

    /// <summary>Finds the cycle a phase log belongs to, creating it when needed.</summary>
    private CycleLogRecord? AlignCycle(
        Guid ownerId,
        CyclePhase phase,
        DateOnly localDate,
        List<CycleLogRecord> cycles,
        UserProfileRecord? profile)
    {
        if (phase == CyclePhase.Menstrual)
        {
            var existingStart = cycles.FirstOrDefault(row => row.StartDate == localDate);
            if (existingStart is not null)
                return existingStart;

            var previousCycle = cycles.Where(row => row.StartDate < localDate)
                .OrderByDescending(row => row.StartDate).FirstOrDefault();
            var nextCycle = cycles.Where(row => row.StartDate > localDate)
                .OrderBy(row => row.StartDate).FirstOrDefault();
            if (previousCycle is not null && (previousCycle.EndDate is null || previousCycle.EndDate.Value > localDate))
            {
                previousCycle.EndDate = localDate;
                previousCycle.CycleLength = localDate.DayNumber - previousCycle.StartDate.DayNumber;
            }

            var created = NewCycle(ownerId, localDate, nextCycle?.StartDate);
            db.CycleLogs.Add(created);
            return created;
        }

        var containingCycle = cycles.Where(row => row.StartDate <= localDate && (row.EndDate is null || localDate < row.EndDate.Value))
            .OrderByDescending(row => row.StartDate).FirstOrDefault();
        if (containingCycle is not null)
            return containingCycle;

        var latestCycleLength = cycles.OrderByDescending(row => row.StartDate).Select(row => row.CycleLength).FirstOrDefault();
        var predictedLength = CyclePhaseRule.NormalizeCycleLength(
            profile?.CycleLength is > 0 ? profile.CycleLength : latestCycleLength is > 0 ? latestCycleLength : DefaultCycleLength);
        var inferredStart = CyclePhaseRule.GetImpliedCycleStart(phase, localDate, predictedLength);
        if (!IsSupportedDate(inferredStart))
            return null;
        var next = cycles.Where(row => row.StartDate > localDate).OrderBy(row => row.StartDate).FirstOrDefault();
        var inferredCycle = NewCycle(ownerId, inferredStart, next?.StartDate);
        db.CycleLogs.Add(inferredCycle);
        return inferredCycle;
    }

    /// <summary>The cycle row a first phase log needs, created with the default length and no end.</summary>
    private CycleLogRecord NewCycle(Guid ownerId, DateOnly start, DateOnly? nextStart) => new()
    {
        Id = Guid.NewGuid(),
        UserId = ownerId,
        StartDate = start,
        EndDate = nextStart,
        CycleLength = nextStart is { } next ? next.DayNumber - start.DayNumber : UnknownCycleLength,
        CreatedAt = timeProvider.GetUtcNow().UtcDateTime
    };

    /// <summary>Takes the owner's edit lock so phase writes stay ordered.</summary>
    private Task<int> LockOwnerEditAsync(Guid ownerId, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({ownerId.ToString("D")}, {OwnerEditLockSalt}))",
            cancellationToken);

    /// <summary>True when the date is inside the supported range.</summary>
    private static bool IsSupportedDate(DateOnly value) => value.Year >= MinimumSupportedYear && value.Year <= MaximumSupportedYear;
    /// <summary>True when the value names a real phase.</summary>
    private static bool IsKnownPhase(CyclePhase value) => Enum.IsDefined(value) && value != CyclePhase.Unknown;
    /// <summary>UTC midnight of a local date.</summary>
    private static DateTime UtcMidnight(DateOnly date) => DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    /// <summary>The result that reports invalid input.</summary>
    private static SaveCyclePhaseLogOutput Invalid() => new(SaveCyclePhaseLogOutcomeKind.InvalidInput);

    /// <summary>A note trimmed and reduced to null when empty, so the column has one representation of "no note".</summary>
    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
            return null;
        var trimmed = note.Trim();
        return trimmed.Length <= MaximumNoteLength ? trimmed : trimmed[..MaximumNoteLength];
    }

    /// <summary>True when the write failed on a unique constraint.</summary>
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
