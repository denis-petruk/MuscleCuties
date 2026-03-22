using MuscleCuties.Models;
using MuscleCuties.Models.Enums;

namespace MuscleCuties.Services;

public interface ICycleService
{
    Task<CyclePhase> GetCurrentPhaseAsync(int userId);
    Task<CycleLog?> GetCurrentCycleAsync(int userId);
    Task StartNewCycleAsync(int userId);
    Task EndCurrentCycleAsync(int userId);
    int CalculateCycleDay(DateTime cycleStartDate);
    CyclePhase CalculatePhase(int cycleDay, int cycleLength);
}