using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Cycle.Planning;

public interface ICyclePhaseCalculator
{
    CyclePhase CalculatePhase(int cycleDay, int cycleLength);
    float GetPhaseCalorieAdjustment(CyclePhase phase);
}
