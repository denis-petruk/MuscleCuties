using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public sealed class DailyCheckInDataService(
    IUserRepository profiles,
    IReadinessRepository readiness,
    IReadinessEngine? engine = null) : IDailyCheckInDataService, IReadinessService
{
    private readonly IReadinessEngine _engine = engine ?? new DailyReadinessEngine(WorkoutPlanningConfig.CreateDefault());

    public Task<DailyReadinessLog?> GetCheckInAsync(int userId, DateOnly date)
    {
        return readiness.GetForAsync(userId, date);
    }

    public Task<IReadOnlyList<DailyReadinessLog>> GetCheckInsAsync(int userId, DateOnly start, DateOnly end)
    {
        return readiness.GetRangeAsync(userId, start, end);
    }

    public async Task<CyclePhaseBaselines?> GetPhaseBaselinesAsync(int userId)
    {
        var profile = await profiles.GetProfileAsync(userId);
        return AdaptiveProfileMapper.ParseBaselines(profile?.PhaseBaselinesJson);
    }

    public Task SaveAsync(DailyReadinessLog log)
    {
        return readiness.SaveInputsAsync(log);
    }

    public Task<DailyInputs?> GetDailyInputsAsync(int userId, DateOnly date)
    {
        return readiness.GetInputsForAsync(userId, date);
    }

    public async Task<Readiness> CalculateReadinessAsync(
        int userId,
        DateOnly date,
        CyclePhase phase,
        DailyInputs? todayInputs = null)
    {
        var historyStart = DateOnly.FromDayNumber(
            Math.Max(DateOnly.MinValue.DayNumber, date.DayNumber - 14));
        var history = await readiness.GetRangeAsync(userId, historyStart, date);
        var baselines = await GetPhaseBaselinesAsync(userId);
        return _engine.ComputeFromHistory(history, date, phase, baselines, todayInputs);
    }
}
