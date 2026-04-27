using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Services;

public class CyclePhaseCalculator : ICyclePhaseCalculator
{
    public CyclePhase CalculatePhase(int cycleDay, int cycleLength)
    {
        if (cycleDay <= 5) return CyclePhase.Menstrual;

        var ovulationDay = cycleLength - 14;
        if (cycleDay <= ovulationDay - 2) return CyclePhase.Follicular;
        if (cycleDay <= ovulationDay + 2) return CyclePhase.Ovulatory;

        return CyclePhase.Luteal;
    }

    public float GetPhaseCalorieAdjustment(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual => -100f,
        CyclePhase.Ovulatory => +50f,
        CyclePhase.Luteal => +150f,
        _ => 0f
    };
}
