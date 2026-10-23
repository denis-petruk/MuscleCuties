using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IReadinessService
{
    Task<DailyReadinessLog?> GetCheckInAsync(int userId, DateOnly date);
    Task<IReadOnlyList<DailyReadinessLog>> GetCheckInsAsync(int userId, DateOnly start, DateOnly end);
    Task<CyclePhaseBaselines?> GetPhaseBaselinesAsync(int userId);
    Task<DailyInputs?> GetDailyInputsAsync(int userId, DateOnly date);

    Task<Readiness> CalculateReadinessAsync(int userId, DateOnly date, CyclePhase phase,
        DailyInputs? todayInputs = null);

    Task SaveAsync(DailyReadinessLog log);
}
