using MuscleCuties.Core.Repositories.Common;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Workout;

namespace MuscleCuties.Core.Repositories.Workout;

public class WorkoutRepository(AppDatabase db) : BaseRepository<WorkoutPlan>(db), IWorkoutRepository
{
    public async Task<WorkoutPlan?> GetPlanWithDaysAsync(int planId) =>
        await _db.WorkoutPlans
            .AsNoTracking()
            .Include(p => p.WorkoutDays)
            .ThenInclude(d => d.WorkoutDayExercises)
            .ThenInclude(we => we.Exercise)
            .FirstOrDefaultAsync(p => p.Id == planId);

    public async Task<WorkoutDay?> GetWorkoutDayWithExercisesAsync(int workoutDayId) =>
        await _db.WorkoutDays
            .AsNoTracking()
            .Include(d => d.WorkoutDayExercises)
            .ThenInclude(we => we.Exercise)
            .FirstOrDefaultAsync(d => d.Id == workoutDayId);

    public async Task<List<WorkoutDay>> GetWorkoutDaysByPlanAsync(int planId) =>
        await _db.WorkoutDays
            .AsNoTracking()
            .Where(d => d.WorkoutPlanId == planId)
            .Include(d => d.WorkoutDayExercises)
            .ThenInclude(we => we.Exercise)
            .OrderBy(d => d.DayOfWeek)
            .ToListAsync();

    public async Task<List<Exercise>> GetExercisesByDayAsync(int workoutDayId) =>
        await _db.WorkoutDayExercises
            .AsNoTracking()
            .Where(we => we.WorkoutDayId == workoutDayId)
            .Include(we => we.Exercise)
            .Select(we => we.Exercise!)
            .ToListAsync();

    public async Task<List<Exercise>> GetAllExercisesAsync() =>
        await _db.Exercises
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ToListAsync();

    public async Task<WorkoutPlan?> GetActivePlanAsync(int userId) =>
        await _db.WorkoutPlans
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.IsActive)
            .Include(p => p.WorkoutDays)
            .FirstOrDefaultAsync();

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

    public async Task AddWorkoutLogAsync(WorkoutLog log)
    {
        await _db.WorkoutLogs.AddAsync(log);
        await _db.SaveChangesAsync();
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

        return await _db.WorkoutLogs
            .AsNoTracking()
            .Include(l => l.ExerciseLogs)
            .Where(l => l.UserId == userId &&
                        l.WorkoutDayId == workoutDayId &&
                        l.Date >= dayStart &&
                        l.Date < dayEnd)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();
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
        {
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

        return await _db.WorkoutLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.Date >= dayStart && l.Date < dayEnd)
            .ToListAsync();
    }

    public async Task<List<WorkoutLog>> GetWorkoutLogsByDateRangeAsync(
        int userId,
        DateTime startDate,
        DateTime endDate)
    {
        var rangeStart = startDate.Date;
        var rangeEnd = endDate.Date.AddDays(1);

        return await _db.WorkoutLogs
            .AsNoTracking()
            .Include(l => l.WorkoutDay)
            .Where(l => l.UserId == userId && l.Date >= rangeStart && l.Date < rangeEnd)
            .OrderByDescending(l => l.Date)
            .ThenByDescending(l => l.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<WorkoutExerciseLog>> GetExerciseLogsByExerciseIdsAsync(
        int userId,
        IReadOnlyCollection<int> exerciseIds)
    {
        if (exerciseIds.Count == 0)
            return [];

        return await _db.WorkoutExerciseLogs
            .AsNoTracking()
            .Include(l => l.WorkoutLog)
            .Where(l => l.WorkoutLog != null &&
                        l.WorkoutLog.UserId == userId &&
                        exerciseIds.Contains(l.ExerciseId))
            .OrderByDescending(l => l.WorkoutLog!.Date)
            .ThenByDescending(l => l.CreatedAt)
            .ToListAsync();
    }
}
