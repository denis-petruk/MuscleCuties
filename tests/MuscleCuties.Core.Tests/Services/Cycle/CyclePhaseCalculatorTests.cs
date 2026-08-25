using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Quiz;

namespace MuscleCuties.Core.Tests.Services.Cycle;

public class CyclePhaseCalculatorTests
{
    private readonly CyclePhaseCalculator _calc = new();

    [Theory]
    [InlineData(1, 28, CyclePhase.Menstrual)]
    [InlineData(5, 28, CyclePhase.Menstrual)]
    [InlineData(6, 28, CyclePhase.Follicular)]
    [InlineData(12, 28, CyclePhase.Follicular)]
    [InlineData(13, 28, CyclePhase.Ovulatory)]  // ovulationDay=14, day 12..16
    [InlineData(16, 28, CyclePhase.Ovulatory)]
    [InlineData(17, 28, CyclePhase.Luteal)]
    [InlineData(28, 28, CyclePhase.Luteal)]
    public void CalculatePhase_StandardCycle_ReturnsCorrectPhase(int day, int length, CyclePhase expected)
    {
        var result = _calc.CalculatePhase(day, length);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(CyclePhase.Menstrual, -100f)]
    [InlineData(CyclePhase.Follicular, 0f)]
    [InlineData(CyclePhase.Ovulatory, 50f)]
    [InlineData(CyclePhase.Luteal, 150f)]
    public void GetPhaseCalorieAdjustment_EachPhase_ReturnsExpectedDelta(CyclePhase phase, float expected)
    {
        var result = _calc.GetPhaseCalorieAdjustment(phase);
        Assert.Equal(expected, result);
    }
}
