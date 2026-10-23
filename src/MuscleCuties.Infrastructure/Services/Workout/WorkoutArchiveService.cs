using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Workout;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.Core.Services.Workout;

public class WorkoutArchiveService : IWorkoutArchiveService
{
    private readonly IAuthService _authService;
    private readonly AppDatabase _db;

    public WorkoutArchiveService(AppDatabase db, IAuthService authService)
    {
        _db = db;
        _authService = authService;
    }

    public async Task<IReadOnlyList<WorkoutSessionSummaryDto>> GetWorkoutHistoryAsync(
        int limit,
        int offset,
        CancellationToken ct = default)
    {
        var userId = await RequireCurrentUserIdAsync().ConfigureAwait(false);
        var safeLimit = Math.Clamp(limit, 1, 1000);
        var safeOffset = Math.Max(0, offset);

        var sessions = await GetPagedSessionsAsync(userId, safeOffset, safeLimit, null, null, ct).ConfigureAwait(false);

        await EnsureCurrentUserUnchangedAsync(userId).ConfigureAwait(false);
        return MapToSessionSummaryDtos(sessions);
    }

    public async Task<int> GetTotalSessionsCountAsync(CancellationToken ct = default)
    {
        var userId = await RequireCurrentUserIdAsync().ConfigureAwait(false);
        var count = await _db.WorkoutSessionLogs
            .AsNoTracking()
            .CountAsync(w => w.UserId == userId && !w.IsDeleted, ct)
            .ConfigureAwait(false);
        await EnsureCurrentUserUnchangedAsync(userId).ConfigureAwait(false);
        return count;
    }

    public async Task<IReadOnlyList<(int Year, int Month)>> GetAvailableHistoryMonthsAsync(
        CancellationToken ct = default)
    {
        var userId = await RequireCurrentUserIdAsync().ConfigureAwait(false);
        var months = await _db.WorkoutSessionLogs.AsNoTracking()
            .Where(w => w.UserId == userId && !w.IsDeleted)
            .Select(w => new { w.StartedAt.Year, w.StartedAt.Month })
            .Distinct()
            .OrderByDescending(m => m.Year)
            .ThenByDescending(m => m.Month)
            .ToListAsync(ct).ConfigureAwait(false);
        await EnsureCurrentUserUnchangedAsync(userId).ConfigureAwait(false);
        return months.Select(m => (m.Year, m.Month)).ToList();
    }

    public async Task<int> GetWorkoutHistoryCountByMonthAsync(int year, int month, CancellationToken ct = default)
    {
        var userId = await RequireCurrentUserIdAsync().ConfigureAwait(false);
        var (start, end) = GetHistoryDateRange(year, month);
        var count = await _db.WorkoutSessionLogs.AsNoTracking()
            .CountAsync(w => w.UserId == userId && !w.IsDeleted && w.StartedAt >= start && w.StartedAt < end, ct)
            .ConfigureAwait(false);
        await EnsureCurrentUserUnchangedAsync(userId).ConfigureAwait(false);
        return count;
    }

    public async Task<WorkoutSessionDetailDto> GetWorkoutSessionDetailAsync(
        Guid sessionId,
        CancellationToken ct = default)
    {
        var userId = await RequireCurrentUserIdAsync().ConfigureAwait(false);
        var session = await _db.WorkoutSessionLogs
            .AsNoTracking()
            .Include(w => w.SetLogs.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(w => w.Id == sessionId && w.UserId == userId && !w.IsDeleted, ct)
            .ConfigureAwait(false);

        if (session is null) throw new KeyNotFoundException($"Workout session with ID '{sessionId}' was not found.");

        await EnsureCurrentUserUnchangedAsync(userId).ConfigureAwait(false);

        var activeSets = session.SetLogs
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.ExerciseName)
            .ThenBy(s => s.SetNumber)
            .Select(s => new ExerciseSetLogDto(
                s.Id,
                s.WorkoutSessionLogId,
                s.ExerciseId,
                s.ExerciseName,
                s.SetNumber,
                s.WeightKg,
                s.RepsCompleted,
                s.Rpe,
                s.IsWarmup,
                s.RestTimeSeconds
            ))
            .ToList();

        return new WorkoutSessionDetailDto(
            session.Id,
            session.WorkoutName,
            session.StartedAt,
            session.CompletedAt,
            session.TotalDurationSeconds,
            Math.Round(session.TotalVolumeKg, 1),
            session.Notes,
            session.RpeRating,
            activeSets,
            GetDetailKind(session.Id, activeSets.Count),
            IsLegacySessionGuid(session.Id) ? "legacyUnknown" : "recordedWallTime"
        );
    }

    public async Task UpdateWorkoutSessionNotesAsync(
        Guid sessionId,
        string notes,
        int? rpe,
        CancellationToken ct = default)
    {
        var userId = await RequireCurrentUserIdAsync().ConfigureAwait(false);
        var session = await _db.WorkoutSessionLogs
            .FirstOrDefaultAsync(w => w.Id == sessionId && w.UserId == userId && !w.IsDeleted, ct)
            .ConfigureAwait(false);

        if (session is null)
            throw new KeyNotFoundException($"Workout session with ID '{sessionId}' was not found.");

        if (rpe is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(rpe));
        try
        {
            session.Notes = notes;
            session.RpeRating = rpe;
            session.UpdatedAt = DateTime.UtcNow;
            await EnsureCurrentUserUnchangedAsync(userId).ConfigureAwait(false);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            _db.Entry(session).State = EntityState.Detached;
            throw;
        }
    }

    public async Task<Guid> SaveWorkoutSessionAsync(
        WorkoutSessionDetailDto session,
        CancellationToken ct = default)
    {
        var userId = await RequireCurrentUserIdAsync().ConfigureAwait(false);
        ValidateSession(session);
        await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var existing = await FindActiveSessionAsync(session.Id, userId, ct).ConfigureAwait(false);
            var setIds = session.Sets.Select(s => s.Id).ToList();
            await ValidateSetOwnershipAsync(session.Id, setIds, userId, ct).ConfigureAwait(false);

            var now = DateTime.UtcNow;
            var target = existing ?? new WorkoutSessionLog { Id = session.Id, UserId = userId, CreatedAt = now };
            await ReconcileSessionSnapshotAsync(target, session, existing is not null, now, ct).ConfigureAwait(false);

            if (existing is null) await _db.WorkoutSessionLogs.AddAsync(target, ct).ConfigureAwait(false);
            if (await RequireCurrentUserIdAsync().ConfigureAwait(false) != userId)
                throw new UnauthorizedAccessException("Account changed.");

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return target.Id;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            DetachFailedSessionEntries(session.Id);
            throw;
        }
    }

    public async Task<Guid> SaveLinkedWorkoutSessionAsync(
        WorkoutSessionDetailDto session,
        int workoutDayId,
        DateTime workoutDate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (workoutDayId <= 0) throw new ArgumentOutOfRangeException(nameof(workoutDayId));
        var userId = await RequireCurrentUserIdAsync().ConfigureAwait(false);
        var dayStart = workoutDate.Date;
        var dayEnd = dayStart.AddDays(1);
        var legacyId = await _db.WorkoutLogs.AsNoTracking()
            .Where(log => log.UserId == userId && log.WorkoutDayId == workoutDayId && log.Date >= dayStart &&
                          log.Date < dayEnd)
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Select(log => log.Id)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (legacyId <= 0)
            throw new KeyNotFoundException("The completed workout log was not found.");

        var linkedId = ToLegacySessionGuid(legacyId);
        if (session.Id != Guid.Empty && session.Id != linkedId)
        {
            var unlinked = await _db.WorkoutSessionLogs
                .FirstOrDefaultAsync(w => w.Id == session.Id && w.UserId == userId && !w.IsDeleted, ct)
                .ConfigureAwait(false);
            if (unlinked is not null)
            {
                unlinked.IsDeleted = true;
                unlinked.DeletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }

        var linked = session with
        {
            Id = linkedId,
            Sets = session.Sets.Select(set => set with { WorkoutSessionLogId = linkedId }).ToList()
        };
        return await SaveWorkoutSessionAsync(linked, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WorkoutSessionSummaryDto>> GetWorkoutHistoryByMonthAsync(
        int year,
        int month,
        int limit = 100,
        int offset = 0,
        CancellationToken ct = default)
    {
        var userId = await RequireCurrentUserIdAsync().ConfigureAwait(false);
        var safeLimit = Math.Clamp(limit, 1, 1000);
        var safeOffset = Math.Max(0, offset);
        var (start, end) = GetHistoryDateRange(year, month);

        var sessions = await GetPagedSessionsAsync(userId, safeOffset, safeLimit, start, end, ct).ConfigureAwait(false);

        await EnsureCurrentUserUnchangedAsync(userId).ConfigureAwait(false);
        return MapToSessionSummaryDtos(sessions);
    }

    public async Task UpdateExerciseSetAsync(
        Guid setId,
        double weightKg,
        int repsCompleted,
        CancellationToken ct = default)
    {
        var userId = await RequireCurrentUserIdAsync().ConfigureAwait(false);
        ValidateMetrics(weightKg, repsCompleted);
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var session = await _db.WorkoutSessionLogs.Include(w => w.SetLogs)
            .FirstOrDefaultAsync(w => w.UserId == userId && !w.IsDeleted &&
                                      w.SetLogs.Any(s => s.Id == setId && !s.IsDeleted), ct);
        if (session is null) throw new KeyNotFoundException("Exercise set was not found.");
        try
        {
            var set = session.SetLogs.Single(s => s.Id == setId);
            var volume = session.SetLogs.Where(s => !s.IsDeleted && !s.IsWarmup)
                .Sum(s => s.Id == setId ? weightKg * repsCompleted : s.WeightKg * s.RepsCompleted);
            if (!double.IsFinite(volume)) throw new ArgumentException("Total volume exceeds the supported range.");
            set.WeightKg = weightKg;
            set.RepsCompleted = repsCompleted;
            set.UpdatedAt = DateTime.UtcNow;
            session.TotalVolumeKg = Math.Round(volume, 1);
            session.UpdatedAt = set.UpdatedAt;
            if (await RequireCurrentUserIdAsync() != userId) throw new UnauthorizedAccessException("Account changed.");
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            foreach (var set in session.SetLogs) _db.Entry(set).State = EntityState.Detached;
            _db.Entry(session).State = EntityState.Detached;
            throw;
        }
    }

    private async Task<IReadOnlyList<WorkoutSessionLog>> GetPagedSessionsAsync(
        int userId,
        int skip,
        int take,
        DateTime? start,
        DateTime? end,
        CancellationToken ct)
    {
        var query = _db.WorkoutSessionLogs
            .AsNoTracking()
            .Where(w => w.UserId == userId && !w.IsDeleted);

        if (start.HasValue)
            query = query.Where(w => w.StartedAt >= start.Value);
        if (end.HasValue)
            query = query.Where(w => w.StartedAt < end.Value);

        return await query
            .OrderByDescending(w => w.StartedAt)
            .ThenByDescending(w => w.Id)
            .Skip(skip)
            .Take(take)
            .Include(w => w.SetLogs.Where(s => !s.IsDeleted))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    private async Task<WorkoutSessionLog?> FindActiveSessionAsync(Guid sessionId, int userId, CancellationToken ct)
    {
        var existing = await _db.WorkoutSessionLogs.Include(w => w.SetLogs)
            .FirstOrDefaultAsync(w => w.Id == sessionId && w.UserId == userId && !w.IsDeleted, ct)
            .ConfigureAwait(false);
        if (existing is null && await _db.WorkoutSessionLogs.AnyAsync(w => w.Id == sessionId, ct).ConfigureAwait(false))
            throw new KeyNotFoundException("Workout session was not found.");
        return existing;
    }

    private async Task ValidateSetOwnershipAsync(Guid sessionId, IReadOnlyList<Guid> setIds, int userId,
        CancellationToken ct)
    {
        if (await _db.ExerciseSetLogs.Include(s => s.WorkoutSessionLog)
                .AnyAsync(s => setIds.Contains(s.Id) &&
                               (s.WorkoutSessionLogId != sessionId || s.WorkoutSessionLog!.UserId != userId ||
                                s.IsDeleted), ct).ConfigureAwait(false))
            throw new KeyNotFoundException("Exercise set was not found.");
    }

    private async Task ReconcileSessionSnapshotAsync(
        WorkoutSessionLog target,
        WorkoutSessionDetailDto session,
        bool isExisting,
        DateTime now,
        CancellationToken ct)
    {
        target.WorkoutName = session.WorkoutName;
        target.StartedAt = session.StartedAt;
        target.CompletedAt = session.CompletedAt ?? (isExisting ? target.CompletedAt : null);
        target.TotalDurationSeconds = session.TotalDurationSeconds > 0 ? session.TotalDurationSeconds :
            isExisting ? target.TotalDurationSeconds : 0;
        target.Notes = session.Notes ?? (isExisting ? target.Notes : null);
        target.RpeRating = session.RpeRating ?? (isExisting ? target.RpeRating : null);
        target.UpdatedAt = now;

        var inputIds = session.Sets.Select(s => s.Id).ToHashSet();
        foreach (var removed in target.SetLogs.Where(s => !s.IsDeleted && !inputIds.Contains(s.Id)))
        {
            removed.IsDeleted = true;
            removed.DeletedAt = now;
        }

        foreach (var input in session.Sets)
        {
            var set = target.SetLogs.FirstOrDefault(s => s.Id == input.Id);
            if (set is null)
            {
                var newSet = CreateExerciseSetLog(target.Id, input, now);
                if (!isExisting) target.SetLogs.Add(newSet);
                else await _db.ExerciseSetLogs.AddAsync(newSet, ct).ConfigureAwait(false);
            }
            else
            {
                ApplySetLogUpdates(set, input, now);
            }
        }

        target.TotalVolumeKg = CalculateVolume(session.Sets);
    }

    private static void ApplySetLogUpdates(ExerciseSetLog set, ExerciseSetLogDto input, DateTime now)
    {
        set.ExerciseId = input.ExerciseId;
        set.ExerciseName = input.ExerciseName;
        set.SetNumber = input.SetNumber;
        set.WeightKg = input.WeightKg;
        set.RepsCompleted = input.RepsCompleted;
        set.Rpe = input.Rpe;
        set.IsWarmup = input.IsWarmup;
        set.RestTimeSeconds = input.RestTimeSeconds;
        set.UpdatedAt = now;
    }

    private void DetachFailedSessionEntries(Guid sessionId)
    {
        foreach (var entry in _db.ChangeTracker.Entries().Where(e =>
                     (e.Entity is WorkoutSessionLog w && w.Id == sessionId) ||
                     (e.Entity is ExerciseSetLog s && s.WorkoutSessionLogId == sessionId)).ToList())
            entry.State = EntityState.Detached;
    }

    private static void ValidateSession(WorkoutSessionDetailDto session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Id == Guid.Empty || string.IsNullOrWhiteSpace(session.WorkoutName) ||
            session.StartedAt == default || (session.CompletedAt.HasValue && session.CompletedAt < session.StartedAt) ||
            session.TotalDurationSeconds < 0 || session.RpeRating is < 1 or > 10 || session.Sets is null ||
            !double.IsFinite(session.TotalVolumeKg) || session.TotalVolumeKg < 0)
            throw new ArgumentException("A stable session ID, valid dates and nonnegative duration are required.");
        var ids = new HashSet<Guid>();
        foreach (var set in session.Sets)
        {
            if (set.Id == Guid.Empty || !ids.Add(set.Id) || set.WorkoutSessionLogId != session.Id ||
                string.IsNullOrWhiteSpace(set.ExerciseName) || set.SetNumber < 1 ||
                set.RestTimeSeconds < 0 || (set.Rpe is { } rpe && (!double.IsFinite(rpe) || rpe < 1 || rpe > 10)))
                throw new ArgumentException("Each recorded set needs a unique stable ID and valid values.");
            ValidateMetrics(set.WeightKg, set.RepsCompleted);
        }

        _ = CalculateVolume(session.Sets);
    }

    private static void ValidateMetrics(double weight, int reps)
    {
        if (!double.IsFinite(weight) || weight < 0 || reps < 0 || !double.IsFinite(weight * reps))
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight and reps must be finite and nonnegative.");
    }

    private static double CalculateVolume(IEnumerable<ExerciseSetLogDto> sets)
    {
        var total = sets.Where(s => !s.IsWarmup).Sum(s => s.WeightKg * s.RepsCompleted);
        if (!double.IsFinite(total)) throw new ArgumentException("Total volume exceeds the supported range.");
        return Math.Round(total, 1);
    }

    private static (DateTime Start, DateTime End) GetHistoryDateRange(int year, int month)
    {
        if (year is < 1 or > 9998 || month is < 0 or > 12)
            throw new ArgumentOutOfRangeException(nameof(year), "A valid year and month are required.");
        var start = new DateTime(year, month == 0 ? 1 : month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = month == 0 ? start.AddYears(1) : start.AddMonths(1);
        return (start, end);
    }

    public static Guid ToLegacySessionGuid(int legacyLogId)
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes.Clear();
        bytes[10] = 0xB0;
        BinaryPrimitives.WriteInt32BigEndian(bytes[12..], legacyLogId);
        return new Guid(bytes);
    }

    private static bool IsLegacySessionGuid(Guid id)
    {
        var bytes = id.ToByteArray();
        for (var index = 0; index < 10; index++)
            if (bytes[index] != 0)
                return false;
        return bytes[10] == 0xB0 && bytes[11] == 0 &&
               BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(12)) > 0;
    }

    private static string GetDetailKind(Guid id, int setCount)
    {
        return setCount > 0
            ? "recordedSets"
            : IsLegacySessionGuid(id)
                ? "aggregateOnly"
                : "noSetDetails";
    }

    private async Task<int> RequireCurrentUserIdAsync()
    {
        var id = await _authService.GetCurrentUserIdAsync().ConfigureAwait(false);
        if (id <= 0) throw new UnauthorizedAccessException("Sign in to access workouts.");
        return id;
    }

    private async Task EnsureCurrentUserUnchangedAsync(int expectedUserId)
    {
        if (await RequireCurrentUserIdAsync().ConfigureAwait(false) != expectedUserId)
            throw new UnauthorizedAccessException("Account changed while accessing workouts.");
    }

    private static List<WorkoutSessionSummaryDto> MapToSessionSummaryDtos(IReadOnlyList<WorkoutSessionLog> sessions)
    {
        var results = new List<WorkoutSessionSummaryDto>(sessions.Count);
        foreach (var s in sessions)
        {
            var activeSets = s.SetLogs.Where(sl => !sl.IsDeleted).ToList();
            var primaryExercises = activeSets
                .Select(sl => sl.ExerciseName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();

            results.Add(new WorkoutSessionSummaryDto(
                s.Id,
                s.WorkoutName,
                s.StartedAt,
                s.CompletedAt,
                s.TotalDurationSeconds,
                Math.Round(s.TotalVolumeKg, 1),
                s.Notes,
                s.RpeRating,
                activeSets.Count,
                primaryExercises,
                GetDetailKind(s.Id, activeSets.Count),
                IsLegacySessionGuid(s.Id) ? "legacyUnknown" : "recordedWallTime"
            ));
        }

        return results;
    }

    private static ExerciseSetLog CreateExerciseSetLog(Guid sessionId, ExerciseSetLogDto setDto, DateTime now)
    {
        return new ExerciseSetLog
        {
            Id = setDto.Id == Guid.Empty ? Guid.NewGuid() : setDto.Id,
            WorkoutSessionLogId = sessionId,
            ExerciseId = setDto.ExerciseId,
            ExerciseName = setDto.ExerciseName,
            SetNumber = setDto.SetNumber,
            WeightKg = setDto.WeightKg,
            RepsCompleted = setDto.RepsCompleted,
            Rpe = setDto.Rpe,
            IsWarmup = setDto.IsWarmup,
            RestTimeSeconds = setDto.RestTimeSeconds,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
