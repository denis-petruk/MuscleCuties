using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IDailyCheckInDataService
{
    Task<DailyReadinessLog?> GetCheckInAsync(int userId, DateOnly date);
    Task<CyclePhaseBaselines?> GetPhaseBaselinesAsync(int userId);
    Task SaveAsync(DailyReadinessLog log);
}
