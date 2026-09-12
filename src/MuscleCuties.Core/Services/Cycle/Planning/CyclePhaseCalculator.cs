using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Cycle.Planning;

public class CyclePhaseCalculator : ICyclePhaseCalculator
{
    public CyclePhase CalculatePhase(int cycleDay, int cycleLength)
    {
        return CyclePhaseRules.CalculatePhase(cycleDay, cycleLength);
    }

    public float GetPhaseCalorieAdjustment(CyclePhase phase)
    {
        return phase switch
        {
            CyclePhase.Menstrual => -100f,
            CyclePhase.Ovulatory => +50f,
            CyclePhase.Luteal => +150f,
            _ => 0f
        };
    }
}
