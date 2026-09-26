using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Repositories.Common;

namespace MuscleCuties.Core.Repositories.Workout;

public class WorkoutRepository(AppDatabase db) : BaseRepository<WorkoutPlan>(db), IWorkoutRepository
{
    public async Task<WorkoutDay?> GetWorkoutDayWithExercisesAsync(int workoutDayId)
    {
        return await ReadAsync(async () =>
        {
            var day = await _db.WorkoutDays
                .AsNoTracking()
                .Include(d => d.WorkoutDayExercises)
                .FirstOrDefaultAsync(d => d.Id == workoutDayId);
            if (day is not null)
                await PopulateExercisesAsync([day]);
            return day;
        });
    }

    public async Task<List<WorkoutDay>> GetWorkoutDaysByPlanAsync(int planId)
    {
        return await ReadAsync(async () =>
        {
            var days = await _db.WorkoutDays
                .AsNoTracking()
                .Where(d => d.WorkoutPlanId == planId)
                .Include(d => d.WorkoutDayExercises)
                .OrderBy(d => d.DayOfWeek)
                .ThenBy(d => d.Id)
                .ToListAsync();
            await PopulateExercisesAsync(days);
            return days;
        });
    }

    private async Task PopulateExercisesAsync(IEnumerable<WorkoutDay> days)
    {
        var entries = days.SelectMany(day => day.WorkoutDayExercises).ToList();
        var ids = entries.Select(entry => entry.ExerciseId).Distinct().ToList();
        if (ids.Count == 0)
            return;

        var exercises = (await ReadExercisesAsync(query => query.Where(e => ids.Contains(e.Id))))
            .ToDictionary(e => e.Id);
        foreach (var entry in entries)
        {
            entry.Exercise = exercises.GetValueOrDefault(entry.ExerciseId);
            entry.Exercise?.WorkoutExercises.Add(entry);
        }
    }

    private async Task<List<Exercise>> ReadExercisesAsync(
        Func<IQueryable<Exercise>, IQueryable<Exercise>> buildQuery)
    {
        try
        {
            return await buildQuery(ExerciseReadQuery(includeInjuryFriendly: true)).ToListAsync();
        }
        catch (SqliteException exception) when (
            exception.SqliteErrorCode == 1 &&
            exception.Message.Contains("no such column:", StringComparison.OrdinalIgnoreCase) &&
            exception.Message.Contains("IsInjuryFriendly", StringComparison.OrdinalIgnoreCase))
        {
            Trace.TraceWarning(
                $"[WorkoutRepository] Exercises.IsInjuryFriendly is missing; reading legacy exercise details " +
                $"with injury suitability defaulted to false until startup migration completes: {exception}");
            return await buildQuery(ExerciseReadQuery(includeInjuryFriendly: false)).ToListAsync();
        }
    }

    private IQueryable<Exercise> ExerciseReadQuery(bool includeInjuryFriendly)
    {
        // EF can optimize away LINQ coalescing for properties marked required. Normalize legacy
        // NULLs before materialization, and omit the missing column entirely on the fallback path.
        // Only fixed SQL expressions are composed here; caller filters remain parameterized LINQ.
        var injuryFriendly = includeInjuryFriendly ? "COALESCE(e.IsInjuryFriendly, 0)" : "0";
#pragma warning disable EF1002 // injuryFriendly is a fixed SQL fragment, not user input
        return _db.Exercises.FromSqlRaw($"""
            SELECT e.Id, COALESCE(e.Code, '') AS Code, COALESCE(e.Name, 'Exercise') AS Name,
                   COALESCE(e.Description, '') AS Description, e.ImageUrl, e.VideoUrl,
                   e.TechniqueNotes, e.PrimaryMuscle, e.SecondaryMuscles,
                   COALESCE(e.JointAreas, '') AS JointAreas, {injuryFriendly} AS IsInjuryFriendly
            FROM Exercises AS e
            """).AsNoTracking();
#pragma warning restore EF1002
    }

    public async Task<WorkoutPlan?> GetActivePlanAsync(int userId)
    {
        return await ReadAsync(() => _db.WorkoutPlans
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.IsActive)
            .Include(p => p.WorkoutDays)
            .FirstOrDefaultAsync());
    }

    public async Task<WorkoutPlan> ReplaceActivePlanAsync(WorkoutPlan plan)
    {
        var activePlans = await _db.WorkoutPlans
            .Where(p => p.UserId == plan.UserId && p.IsActive)
            .ToListAsync();

        foreach (var activePlan in activePlans)
            activePlan.IsActive = false;

        plan.IsActive = true;
        await _db.WorkoutPlans.AddAsync(plan);
        await _db.SaveChangesAsync();

        return plan;
    }

    public async Task ReplaceWorkoutLogAsync(WorkoutLog log)
    {
        var dayStart = log.Date.Date;
        var dayEnd = dayStart.AddDays(1);
        var existingLogs = await _db.WorkoutLogs
            .Include(l => l.ExerciseLogs)
            .Where(l => l.UserId == log.UserId &&
                        l.WorkoutDayId == log.WorkoutDayId &&
                        l.Date >= dayStart &&
                        l.Date < dayEnd)
            .ToListAsync();

        if (existingLogs.Count > 0)
            _db.WorkoutLogs.RemoveRange(existingLogs);

        log.Date = dayStart;
        await _db.WorkoutLogs.AddAsync(log);
        await _db.SaveChangesAsync();
    }

    public async Task<WorkoutLog?> GetWorkoutLogForDayAsync(int userId, int workoutDayId, DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        return await ReadAsync(() => _db.WorkoutLogs
            .AsNoTracking()
            .Include(l => l.ExerciseLogs)
            .Where(l => l.UserId == userId &&
                        l.WorkoutDayId == workoutDayId &&
                        l.Date >= dayStart &&
                        l.Date < dayEnd)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync());
    }

    public async Task MergeWorkoutLogAsync(WorkoutLog log)
    {
        var dayStart = log.Date.Date;
        var dayEnd = dayStart.AddDays(1);
        var existingLogs = await _db.WorkoutLogs
            .Include(l => l.ExerciseLogs)
            .Where(l => l.UserId == log.UserId &&
                        l.WorkoutDayId == log.WorkoutDayId &&
                        l.Date >= dayStart &&
                        l.Date < dayEnd)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        if (existingLogs.Count == 0)
        {
            log.Date = dayStart;
            await _db.WorkoutLogs.AddAsync(log);
            await _db.SaveChangesAsync();
            return;
        }

        var primaryLog = existingLogs[0];
        primaryLog.Date = dayStart;
        primaryLog.CompletionPercent = log.CompletionPercent;
        primaryLog.Notes = log.Notes;
        primaryLog.CreatedAt = log.CreatedAt;

        foreach (var duplicateLog in existingLogs.Skip(1))
            foreach (var duplicateExerciseLog in duplicateLog.ExerciseLogs.ToList())
            {
                var alreadyMoved = primaryLog.ExerciseLogs.Any(existing =>
                    existing.WorkoutDayExerciseId == duplicateExerciseLog.WorkoutDayExerciseId);
                if (alreadyMoved)
                    continue;

                primaryLog.ExerciseLogs.Add(new WorkoutExerciseLog
                {
                    WorkoutDayExerciseId = duplicateExerciseLog.WorkoutDayExerciseId,
                    ExerciseId = duplicateExerciseLog.ExerciseId,
                    CompletedSets = duplicateExerciseLog.CompletedSets,
                    CompletedReps = duplicateExerciseLog.CompletedReps,
                    WeightKg = duplicateExerciseLog.WeightKg,
                    CompletedDurationSeconds = duplicateExerciseLog.CompletedDurationSeconds,
                    DistanceKm = duplicateExerciseLog.DistanceKm,
                    AverageHeartRateBpm = duplicateExerciseLog.AverageHeartRateBpm,
                    PaceSecondsPerKm = duplicateExerciseLog.PaceSecondsPerKm,
                    PowerWatts = duplicateExerciseLog.PowerWatts,
                    CadenceRpm = duplicateExerciseLog.CadenceRpm,
                    EffortRating = duplicateExerciseLog.EffortRating,
                    CreatedAt = duplicateExerciseLog.CreatedAt
                });
            }

        foreach (var incomingLog in log.ExerciseLogs)
        {
            var existingExerciseLog = primaryLog.ExerciseLogs.FirstOrDefault(existing =>
                existing.WorkoutDayExerciseId == incomingLog.WorkoutDayExerciseId);
            if (existingExerciseLog is null)
            {
                primaryLog.ExerciseLogs.Add(new WorkoutExerciseLog
                {
                    WorkoutDayExerciseId = incomingLog.WorkoutDayExerciseId,
                    ExerciseId = incomingLog.ExerciseId,
                    CompletedSets = incomingLog.CompletedSets,
                    CompletedReps = incomingLog.CompletedReps,
                    WeightKg = incomingLog.WeightKg,
                    CompletedDurationSeconds = incomingLog.CompletedDurationSeconds,
                    DistanceKm = incomingLog.DistanceKm,
                    AverageHeartRateBpm = incomingLog.AverageHeartRateBpm,
                    PaceSecondsPerKm = incomingLog.PaceSecondsPerKm,
                    PowerWatts = incomingLog.PowerWatts,
                    CadenceRpm = incomingLog.CadenceRpm,
                    EffortRating = incomingLog.EffortRating,
                    CreatedAt = incomingLog.CreatedAt
                });
                continue;
            }

            existingExerciseLog.ExerciseId = incomingLog.ExerciseId;
            existingExerciseLog.CompletedSets = incomingLog.CompletedSets;
            existingExerciseLog.CompletedReps = incomingLog.CompletedReps;
            existingExerciseLog.WeightKg = incomingLog.WeightKg;
            existingExerciseLog.CompletedDurationSeconds = incomingLog.CompletedDurationSeconds;
            existingExerciseLog.DistanceKm = incomingLog.DistanceKm;
            existingExerciseLog.AverageHeartRateBpm = incomingLog.AverageHeartRateBpm;
            existingExerciseLog.PaceSecondsPerKm = incomingLog.PaceSecondsPerKm;
            existingExerciseLog.PowerWatts = incomingLog.PowerWatts;
            existingExerciseLog.CadenceRpm = incomingLog.CadenceRpm;
            existingExerciseLog.EffortRating = incomingLog.EffortRating;
            existingExerciseLog.CreatedAt = incomingLog.CreatedAt;
        }

        if (existingLogs.Count > 1)
            _db.WorkoutLogs.RemoveRange(existingLogs.Skip(1));

        await _db.SaveChangesAsync();
    }

    public async Task<List<WorkoutLog>> GetWorkoutLogsByDateAsync(int userId, DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        return await ReadAsync(() => _db.WorkoutLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.Date >= dayStart && l.Date < dayEnd)
            .ToListAsync());
    }

    public async Task<List<WorkoutLog>> GetWorkoutLogsByDateRangeAsync(
        int userId,
        DateTime startDate,
        DateTime endDate)
    {
        var rangeStart = startDate.Date;
        var rangeEnd = endDate.Date.AddDays(1);

        return await ReadAsync(() => _db.WorkoutLogs
            .AsNoTracking()
            .Include(l => l.WorkoutDay)
            .Where(l => l.UserId == userId && l.Date >= rangeStart && l.Date < rangeEnd)
            .OrderByDescending(l => l.Date)
            .ThenByDescending(l => l.CreatedAt)
            .ToListAsync());
    }

    public async Task<List<WorkoutExerciseLog>> GetExerciseLogsByExerciseIdsAsync(
        int userId,
        IReadOnlyCollection<int> exerciseIds)
    {
        if (exerciseIds.Count == 0)
            return [];

        return await ReadAsync(() => _db.WorkoutExerciseLogs
            .AsNoTracking()
            .Include(l => l.WorkoutLog)
            .Where(l => l.WorkoutLog != null &&
                        l.WorkoutLog.UserId == userId &&
                        exerciseIds.Contains(l.ExerciseId))
            .OrderByDescending(l => l.WorkoutLog!.Date)
            .ThenByDescending(l => l.CreatedAt)
            .ToListAsync());
    }
}
