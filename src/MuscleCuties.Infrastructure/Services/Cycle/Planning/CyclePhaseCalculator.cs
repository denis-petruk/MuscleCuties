using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Cycle.Planning;

/// <summary>The phase rules behind the interface, so callers depend on the contract rather than the tables.</summary>
public class CyclePhaseCalculator : ICyclePhaseCalculator
{
    /// <summary>The phase a cycle day falls in.</summary>
    public CyclePhase CalculatePhase(int cycleDay, int cycleLength)
    {
        return CyclePhaseRules.CalculatePhase(cycleDay, cycleLength);
    }

    /// <summary>How much the phase moves the calorie target, as a fraction rather than a number of calories.</summary>
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
