using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Cycle.Planning;

/// <summary>The phase calculation as the rest of the app sees it.</summary>
public interface ICyclePhaseCalculator
{
    CyclePhase CalculatePhase(int cycleDay, int cycleLength);
    float GetPhaseCalorieAdjustment(CyclePhase phase);
}
