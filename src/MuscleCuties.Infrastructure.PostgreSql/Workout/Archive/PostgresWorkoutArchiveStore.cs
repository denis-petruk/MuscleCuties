using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Archive;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Archive;

public sealed class PostgresWorkoutArchiveStore(PostgresDbContext dbContext) : IServerWorkoutArchiveStore
{
    private const int PrimaryExerciseSummaryCount = 4;
    private const double EpleyFormulaDivisor = 30.0;
    private const int MinValidYear = 1;
    private const int MaxValidYear = 9998;
    private const int MinValidMonth = 0;
    private const int MaxValidMonth = 12;
    private const string RecordedSetsDetailKind = "recordedSets";
    private const string NoSetDetailsDetailKind = "noSetDetails";
    private const string RecordedWallTimePrecision = "recordedWallTime";

    public async Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryAsync(
        Guid userId,
        int limit,
        int offset,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var sessions = await dbContext.WorkoutSessionLogs.AsNoTracking()
                .Where(w => w.UserId == userId && !w.IsDeleted)
                .OrderByDescending(w => w.StartedAt)
                .ThenByDescending(w => w.Id)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var summaries = await BuildSummariesAsync(dbContext, userId, sessions, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return summaries;
        }).ConfigureAwait(false);

    public async Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryByMonthAsync(
        Guid userId,
        int year,
        int month,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var (start, end) = GetHistoryDateRange(year, month);
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var sessions = await dbContext.WorkoutSessionLogs.AsNoTracking()
                .Where(w => w.UserId == userId && !w.IsDeleted && w.StartedAt >= start && w.StartedAt < end)
                .OrderByDescending(w => w.StartedAt)
                .ThenByDescending(w => w.Id)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var summaries = await BuildSummariesAsync(dbContext, userId, sessions, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return summaries;
        }).ConfigureAwait(false);

    public async Task<WorkoutArchiveDetailDto?> GetWorkoutSessionDetailAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var session = await dbContext.WorkoutSessionLogs.AsNoTracking()
                .SingleOrDefaultAsync(w => w.UserId == userId && w.Id == sessionId && !w.IsDeleted, cancellationToken)
                .ConfigureAwait(false);

            if (session is null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }

            var sets = await dbContext.ExerciseSetLogs.AsNoTracking()
                .Where(s => s.UserId == userId && s.WorkoutSessionLogId == sessionId && !s.IsDeleted)
                .OrderBy(s => s.ExerciseName)
                .ThenBy(s => s.SetNumber)
                .ThenBy(s => s.CreatedAt)
                .Select(s => new WorkoutExerciseSetDto(
                    s.Id,
                    s.WorkoutSessionLogId,
                    s.ExerciseId,
                    s.ExerciseName,
                    s.SetNumber,
                    (double)s.WeightKg,
                    s.RepsCompleted,
                    s.Rpe.HasValue ? (double)s.Rpe.Value : null,
                    s.IsWarmup,
                    s.RestTimeSeconds))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            var detailKind = sets.Count > 0 ? RecordedSetsDetailKind : NoSetDetailsDetailKind;
            return new WorkoutArchiveDetailDto(
                session.Id,
                session.WorkoutName,
                DateTime.SpecifyKind(session.StartedAt, DateTimeKind.Utc),
                session.CompletedAt.HasValue ? DateTime.SpecifyKind(session.CompletedAt.Value, DateTimeKind.Utc) : null,
                session.TotalDurationSeconds,
                (double)Math.Round(session.TotalVolumeKg, 1),
                session.Notes,
                session.RpeRating,
                sets,
                detailKind,
                RecordedWallTimePrecision);
        }).ConfigureAwait(false);

    public async Task<bool> UpdateWorkoutSessionNotesAsync(
        Guid userId,
        Guid sessionId,
        string notes,
        int? rpe,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var session = await dbContext.WorkoutSessionLogs
                .SingleOrDefaultAsync(w => w.UserId == userId && w.Id == sessionId && !w.IsDeleted, cancellationToken)
                .ConfigureAwait(false);

            if (session is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            session.Notes = notes;
            session.RpeRating = rpe;
            session.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);

    public async Task<bool> UpdateExerciseSetAsync(
        Guid userId,
        Guid setId,
        double weightKg,
        int repsCompleted,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var set = await dbContext.ExerciseSetLogs
                .SingleOrDefaultAsync(s => s.UserId == userId && s.Id == setId && !s.IsDeleted, cancellationToken)
                .ConfigureAwait(false);

            if (set is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            var nowUtc = DateTime.UtcNow;
            set.WeightKg = (decimal)weightKg;
            set.RepsCompleted = repsCompleted;
            set.UpdatedAt = nowUtc;

            var session = await dbContext.WorkoutSessionLogs
                .SingleOrDefaultAsync(w => w.UserId == userId && w.Id == set.WorkoutSessionLogId && !w.IsDeleted, cancellationToken)
                .ConfigureAwait(false);

            if (session is not null)
            {
                var workingSets = await dbContext.ExerciseSetLogs
                    .Where(s => s.UserId == userId && s.WorkoutSessionLogId == session.Id && !s.IsDeleted && !s.IsWarmup)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                decimal totalVol = 0m;
                foreach (var s in workingSets)
                {
                    var w = s.Id == setId ? (decimal)weightKg : s.WeightKg;
                    var r = s.Id == setId ? repsCompleted : s.RepsCompleted;
                    totalVol += w * r;
                }

                session.TotalVolumeKg = Math.Round(totalVol, 1);
                session.UpdatedAt = nowUtc;
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);

    public async Task<int> GetTotalSessionsCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var count = await dbContext.WorkoutSessionLogs
                .CountAsync(w => w.UserId == userId && !w.IsDeleted, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return count;
        }).ConfigureAwait(false);

    public async Task<IReadOnlyList<HistoryMonthOptionDto>> GetAvailableHistoryMonthsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var dates = await dbContext.WorkoutSessionLogs.AsNoTracking()
                .Where(w => w.UserId == userId && !w.IsDeleted)
                .Select(w => w.StartedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return dates
                .Select(d => new { d.Year, d.Month })
                .Distinct()
                .OrderByDescending(d => d.Year)
                .ThenByDescending(d => d.Month)
                .Select(d => new HistoryMonthOptionDto(d.Year, d.Month))
                .ToList();
        }).ConfigureAwait(false);

    public async Task<int> GetWorkoutHistoryCountByMonthAsync(
        Guid userId,
        int year,
        int month,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var (start, end) = GetHistoryDateRange(year, month);
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var count = await dbContext.WorkoutSessionLogs
                .CountAsync(w => w.UserId == userId && !w.IsDeleted && w.StartedAt >= start && w.StartedAt < end, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return count;
        }).ConfigureAwait(false);

    public async Task<IReadOnlyList<ExercisePersonalRecordDto>> GetPersonalRecordsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync<IReadOnlyList<ExercisePersonalRecordDto>>(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var query = from s in dbContext.ExerciseSetLogs.AsNoTracking()
                        join w in dbContext.WorkoutSessionLogs.AsNoTracking()
                            on new { s.UserId, WorkoutSessionLogId = s.WorkoutSessionLogId }
                            equals new { w.UserId, WorkoutSessionLogId = w.Id }
                        where s.UserId == userId && !s.IsDeleted && !s.IsWarmup && !w.IsDeleted
                        select new
                        {
                            s.Id,
                            s.WorkoutSessionLogId,
                            s.ExerciseId,
                            s.ExerciseName,
                            s.WeightKg,
                            s.RepsCompleted,
                            s.CreatedAt
                        };

            var allSets = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            if (allSets.Count == 0)
                return Array.Empty<ExercisePersonalRecordDto>();

            var grouped = allSets
                .Where(s => !string.IsNullOrWhiteSpace(s.ExerciseName))
                .GroupBy(s => s.ExerciseName.Trim(), StringComparer.OrdinalIgnoreCase);

            var records = new List<ExercisePersonalRecordDto>();

            foreach (var group in grouped)
            {
                var exerciseName = group.First().ExerciseName.Trim();
                var exerciseId = group.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.ExerciseId))?.ExerciseId;

                var maxWeight = group.Max(s => (double)s.WeightKg);
                var bestWeightSet = group
                    .Where(s => (double)s.WeightKg == maxWeight)
                    .OrderByDescending(s => s.RepsCompleted)
                    .ThenByDescending(s => s.CreatedAt)
                    .First();

                var maxVolume = group.Max(s => (double)s.WeightKg * s.RepsCompleted);

                var maxEstimated1Rm = group.Max(s =>
                    s.RepsCompleted <= 1
                        ? (double)s.WeightKg
                        : (double)s.WeightKg * (1.0 + s.RepsCompleted / EpleyFormulaDivisor));

                records.Add(new ExercisePersonalRecordDto(
                    exerciseId,
                    exerciseName,
                    Math.Round(maxWeight, 2),
                    bestWeightSet.RepsCompleted,
                    Math.Round(maxVolume, 2),
                    Math.Round(maxEstimated1Rm, 2),
                    DateTime.SpecifyKind(bestWeightSet.CreatedAt, DateTimeKind.Utc),
                    bestWeightSet.WorkoutSessionLogId,
                    bestWeightSet.Id));
            }

            return records.OrderBy(r => r.ExerciseName, StringComparer.OrdinalIgnoreCase).ToList();
        }).ConfigureAwait(false);

    private static async Task<IReadOnlyList<WorkoutArchiveSummaryDto>> BuildSummariesAsync(
        PostgresDbContext dbContext,
        Guid userId,
        IReadOnlyList<WorkoutSessionLogRecord> sessions,
        CancellationToken cancellationToken)
    {
        if (sessions.Count == 0)
            return Array.Empty<WorkoutArchiveSummaryDto>();

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var sets = await dbContext.ExerciseSetLogs.AsNoTracking()
            .Where(s => s.UserId == userId && sessionIds.Contains(s.WorkoutSessionLogId) && !s.IsDeleted)
            .OrderBy(s => s.SetNumber)
            .ThenBy(s => s.CreatedAt)
            .Select(s => new { s.WorkoutSessionLogId, s.ExerciseName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var setsBySession = sets.GroupBy(s => s.WorkoutSessionLogId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<WorkoutArchiveSummaryDto>(sessions.Count);
        foreach (var session in sessions)
        {
            var sessionSets = setsBySession.TryGetValue(session.Id, out var sl) ? sl : [];
            var primaryExercises = sessionSets
                .Select(s => s.ExerciseName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(PrimaryExerciseSummaryCount)
                .ToList();

            var detailKind = sessionSets.Count > 0 ? RecordedSetsDetailKind : NoSetDetailsDetailKind;
            result.Add(new WorkoutArchiveSummaryDto(
                session.Id,
                session.WorkoutName,
                DateTime.SpecifyKind(session.StartedAt, DateTimeKind.Utc),
                session.CompletedAt.HasValue ? DateTime.SpecifyKind(session.CompletedAt.Value, DateTimeKind.Utc) : null,
                session.TotalDurationSeconds,
                (double)Math.Round(session.TotalVolumeKg, 1),
                session.Notes,
                session.RpeRating,
                sessionSets.Count,
                primaryExercises,
                detailKind,
                RecordedWallTimePrecision));
        }

        return result;
    }

    private static (DateTime Start, DateTime End) GetHistoryDateRange(int year, int month)
    {
        if (year is < MinValidYear or > MaxValidYear || month is < MinValidMonth or > MaxValidMonth)
            throw new ArgumentOutOfRangeException(nameof(year), "A valid year and month are required.");
        var start = new DateTime(year, month == 0 ? 1 : month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = month == 0 ? start.AddYears(1) : start.AddMonths(1);
        return (start, end);
    }
}
