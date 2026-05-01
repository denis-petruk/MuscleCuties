using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Services;

public interface ICycleService
{
    Task<CyclePhase> GetCurrentPhaseAsync(int userId);
    Task<CycleLog?> GetCurrentCycleAsync(int userId);
    Task StartNewCycleAsync(int userId);
    Task EndCurrentCycleAsync(int userId);
}