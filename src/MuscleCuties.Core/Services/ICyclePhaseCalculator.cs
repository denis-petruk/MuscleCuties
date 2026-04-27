using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Services;

public interface ICyclePhaseCalculator
{
    CyclePhase CalculatePhase(int cycleDay, int cycleLength);
    float GetPhaseCalorieAdjustment(CyclePhase phase);
}
