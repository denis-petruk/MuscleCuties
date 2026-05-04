using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;

namespace MuscleCuties.Core.Services;

public class WorkoutService : IWorkoutService
{
    private readonly IWorkoutRepository _workoutRepo;
    private readonly ICycleService _cycleService;

    public WorkoutService(IWorkoutRepository workoutRepo, ICycleService cycleService)
    {
        _workoutRepo = workoutRepo;
        _cycleService = cycleService;
    }

    public async Task GenerateUserPlansAsync(int userId, UserGoal goal, int daysPerWeek)
    {
        var existing = await _workoutRepo.GetAllUserPlansAsync(userId);
        foreach (var old in existing)
            await _workoutRepo.DeleteAsync(old);

        var currentPhase = await _cycleService.GetCurrentPhaseAsync(userId);

        foreach (var phase in Enum.GetValues<CyclePhase>())
        {
            var dayTypes = GetPhaseWorkoutDayTypes(phase, goal, daysPerWeek);
            var plan = new WorkoutPlan
            {
                UserId = userId,
                Name = $"{phase} Plan",
                IsActive = phase == currentPhase,
                CyclePhaseTarget = phase,
                CreatedAt = DateTime.UtcNow
            };

            var dowSlots = GetDayOfWeekSlots(daysPerWeek);
            for (int i = 0; i < dayTypes.Length; i++)
            {
                var type = dayTypes[i];
                var exercises = await GetExercisesForType(type);
                var recoveryType = type == WorkoutType.Recovery
                    ? ClassifyRecovery(phase, goal)
                    : RecoveryType.None;
                var day = new WorkoutDay
                {
                    DayOfWeek = dowSlots[i],
                    Name = BuildDayName(phase, type, i),
                    WorkoutType = type,
                    RecoveryType = recoveryType,
                    DurationMinutes = type == WorkoutType.Recovery ? 20 : 45
                };
                foreach (var (ex, sets, reps, durSec) in exercises)
                    day.WorkoutDayExercises.Add(new WorkoutDayExercise
                    {
                        Exercise = ex,
                        Sets = sets,
                        Reps = reps,
                        DurationSeconds = durSec
                    });
                plan.WorkoutDays.Add(day);
            }

            await _workoutRepo.AddAsync(plan);
        }
    }

    public async Task<WorkoutDay?> GetTodaysWorkoutAsync(int userId)
    {
        var plan = await _workoutRepo.GetActivePlanAsync(userId);
        if (plan?.WorkoutDays is null || !plan.WorkoutDays.Any())
            return null;

        var todayDow = (int)DateTime.Today.DayOfWeek;
        return plan.WorkoutDays.FirstOrDefault(d => d.DayOfWeek == todayDow);
    }

    public async Task SyncActivePlanToPhaseAsync(int userId, CyclePhase currentPhase)
    {
        await _workoutRepo.DeactivateAllUserPlansAsync(userId);
        var plan = await _workoutRepo.GetPlanByPhaseAsync(userId, currentPhase);
        if (plan is not null)
        {
            plan.IsActive = true;
            await _workoutRepo.UpdateAsync(plan);
        }
    }


    private static WorkoutType[] GetPhaseWorkoutDayTypes(CyclePhase phase, UserGoal goal, int daysPerWeek)
    {
        var count = Math.Clamp(daysPerWeek, 2, 5);

        if (phase == CyclePhase.Menstrual)
            return Enumerable.Repeat(WorkoutType.Recovery, count).ToArray();

        List<WorkoutType> pool = (phase, goal) switch
        {
            (CyclePhase.Follicular, UserGoal.FatLoss)   => new() { WorkoutType.Cardio, WorkoutType.Strength, WorkoutType.Cardio, WorkoutType.Strength, WorkoutType.Cardio },
            (CyclePhase.Follicular, UserGoal.Strength)  => new() { WorkoutType.Strength, WorkoutType.Strength, WorkoutType.Strength, WorkoutType.Cardio, WorkoutType.Strength },
            (CyclePhase.Follicular, _)                  => new() { WorkoutType.Strength, WorkoutType.Cardio, WorkoutType.Strength, WorkoutType.Cardio, WorkoutType.Strength },
            (CyclePhase.Ovulatory, UserGoal.FatLoss)    => new() { WorkoutType.Cardio, WorkoutType.Strength, WorkoutType.Cardio, WorkoutType.Cardio, WorkoutType.Strength },
            (CyclePhase.Ovulatory, UserGoal.Strength)   => new() { WorkoutType.Strength, WorkoutType.Cardio, WorkoutType.Strength, WorkoutType.Strength, WorkoutType.Cardio },
            (CyclePhase.Ovulatory, _)                   => new() { WorkoutType.Strength, WorkoutType.Cardio, WorkoutType.Strength, WorkoutType.Cardio, WorkoutType.Strength },
            (CyclePhase.Luteal, UserGoal.FatLoss)       => new() { WorkoutType.Cardio, WorkoutType.Recovery, WorkoutType.Cardio, WorkoutType.Recovery, WorkoutType.Cardio },
            (CyclePhase.Luteal, UserGoal.Strength)      => new() { WorkoutType.Strength, WorkoutType.Recovery, WorkoutType.Cardio, WorkoutType.Recovery, WorkoutType.Strength },
            (CyclePhase.Luteal, _)                      => new() { WorkoutType.Cardio, WorkoutType.Recovery, WorkoutType.Cardio, WorkoutType.Recovery, WorkoutType.Recovery },
            _                                           => new() { WorkoutType.Strength, WorkoutType.Cardio, WorkoutType.Recovery, WorkoutType.Strength, WorkoutType.Cardio }
        };

        return pool.Take(count).ToArray();
    }

    private static int[] GetDayOfWeekSlots(int daysPerWeek) => daysPerWeek switch
    {
        2 => new[] { 1, 4 },
        3 => new[] { 1, 3, 5 },
        4 => new[] { 1, 2, 4, 6 },
        5 => new[] { 1, 2, 3, 4, 5 },
        _ => new[] { 1, 3, 5 }
    };

    private static string BuildDayName(CyclePhase phase, WorkoutType type, int dayIndex) =>
        $"{phase} {type} Day {dayIndex + 1}";

    private static RecoveryType ClassifyRecovery(CyclePhase phase, UserGoal goal) =>
        phase switch
        {
            CyclePhase.Menstrual  => RecoveryType.PassiveRecovery,
            CyclePhase.Follicular => RecoveryType.ActiveRecovery,
            CyclePhase.Ovulatory  => RecoveryType.ActiveRecovery,
            CyclePhase.Luteal     => goal == UserGoal.Strength
                ? RecoveryType.PassiveRecovery
                : RecoveryType.ActiveRecovery,
            _ => RecoveryType.ActiveRecovery
        };

    private async Task<List<(Exercise ex, int sets, int reps, int? durSec)>> GetExercisesForType(WorkoutType type) =>
        type switch
        {
            WorkoutType.Strength => await GetStrengthExercises(),
            WorkoutType.Cardio   => await GetCardioExercises(),
            _                    => await GetRecoveryExercises()
        };

    private async Task<List<(Exercise, int, int, int?)>> GetStrengthExercises()
    {
        var codes = new[] { "GOBLET_SQUAT", "DB_ROMANIAN_DEADLIFT", "BARBELL_HIP_THRUST", "INCLINE_PUSH_UP_BENCH", "CHEST_SUPPORTED_DB_ROW", "PLANK" };
        var map = (await _workoutRepo.GetExercisesByCodesAsync(codes)).ToDictionary(e => e.Code);

        var list = new List<(Exercise ex, int sets, int reps, int? durSec)>();
        if (map.TryGetValue("GOBLET_SQUAT",           out var squat)) list.Add((squat, 3, 12, null));
        if (map.TryGetValue("DB_ROMANIAN_DEADLIFT",   out var rdl))   list.Add((rdl,   3, 10, null));
        if (map.TryGetValue("BARBELL_HIP_THRUST",     out var hip))   list.Add((hip,   3, 15, null));
        if (map.TryGetValue("INCLINE_PUSH_UP_BENCH",  out var push))  list.Add((push,  3, 10, null));
        if (map.TryGetValue("CHEST_SUPPORTED_DB_ROW", out var row))   list.Add((row,   3, 12, null));
        if (map.TryGetValue("PLANK",                  out var plank)) list.Add((plank, 3,  0, 45));
        return list;
    }

    private async Task<List<(Exercise, int, int, int?)>> GetCardioExercises()
    {
        var codes = new[] { "ELLIPTICAL_TRAINER", "STAIR_CLIMBER", "ROWING_MACHINE", "STATIONARY_BIKE", "TREADMILL_INCLINE_WALK" };
        var map = (await _workoutRepo.GetExercisesByCodesAsync(codes)).ToDictionary(e => e.Code);

        var list = new List<(Exercise ex, int sets, int reps, int? durSec)>();
        if (map.TryGetValue("ELLIPTICAL_TRAINER",    out var elliptical)) list.Add((elliptical, 3, 0,  300));
        if (map.TryGetValue("STAIR_CLIMBER",         out var stairs))     list.Add((stairs,     3, 0,  300));
        if (map.TryGetValue("ROWING_MACHINE",        out var row))        list.Add((row,        3, 0,  300));
        if (map.TryGetValue("STATIONARY_BIKE",       out var bike))       list.Add((bike,       3, 0,  300));
        if (map.TryGetValue("TREADMILL_INCLINE_WALK",out var walk))       list.Add((walk,       1, 0, 1200));
        return list;
    }

    private async Task<List<(Exercise, int, int, int?)>> GetRecoveryExercises()
    {
        var codes = new[] { "CHILDS_POSE", "CAT_COW", "PIGEON_POSE", "SUPINE_SPINAL_TWIST", "LEGS_UP_WALL" };
        var map = (await _workoutRepo.GetExercisesByCodesAsync(codes)).ToDictionary(e => e.Code);

        var list = new List<(Exercise ex, int sets, int reps, int? durSec)>();
        if (map.TryGetValue("CHILDS_POSE",          out var cp))  list.Add((cp,  1, 0,  60));
        if (map.TryGetValue("CAT_COW",              out var cc))  list.Add((cc,  1, 0,  60));
        if (map.TryGetValue("PIGEON_POSE",          out var pp))  list.Add((pp,  1, 0,  90));
        if (map.TryGetValue("SUPINE_SPINAL_TWIST",  out var st))  list.Add((st,  1, 0,  60));
        if (map.TryGetValue("LEGS_UP_WALL",         out var luw)) list.Add((luw, 1, 0, 120));
        return list;
    }
}
