using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;

namespace MuscleCuties.Core.Services.Cycle;

public interface ICycleService
{
    Task<CyclePhase> GetCurrentPhaseAsync(int userId);
    Task<CyclePrediction> GetPredictionAsync(int userId);
    Task<CycleLog?> GetCurrentCycleAsync(int userId);
    Task<CyclePhaseLog?> GetLatestPhaseLogAsync(int userId);
    Task<IReadOnlyList<CyclePhaseLog>> GetRecentPhaseLogsAsync(int userId, int count);
    Task LogPhaseShiftAsync(int userId, CyclePhase phase, DateTime loggedAt, string? note);
    Task SetPhaseForDateAsync(int userId, CyclePhase phase, DateTime loggedAt, string? note);
    Task StartNewCycleAsync(int userId);
    Task EndCurrentCycleAsync(int userId);
}
