using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public sealed class DailyCheckInDataService(
    IUserRepository profiles,
    IReadinessRepository readiness) : IDailyCheckInDataService
{
    public Task<DailyReadinessLog?> GetCheckInAsync(int userId, DateOnly date) =>
        readiness.GetForAsync(userId, date);

    public async Task<CyclePhaseBaselines?> GetPhaseBaselinesAsync(int userId)
    {
        var profile = await profiles.GetProfileAsync(userId);
        return AdaptiveProfileMapper.ParseBaselines(profile?.PhaseBaselinesJson);
    }

    public Task SaveAsync(DailyReadinessLog log) => readiness.SaveInputsAsync(log);
}
