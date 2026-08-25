using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Quiz;

namespace MuscleCuties.Core.Tests.Services.Nutrition;

public class CalorieCalculatorTests
{
    private readonly CalorieCalculator _calc = new();

    [Fact]
    public void CalculateBmr_KnownInputs_ReturnsCorrectValue()
    {
        // 10*60 + 6.25*165 - 5*25 - 161 = 600 + 1031.25 - 125 - 161 = 1345.25
        var result = _calc.CalculateBmr(60f, 165f, 25);

        Assert.Equal(1345.25f, result, precision: 1);
    }

    [Theory]
    [InlineData(0, 1.2f)]
    [InlineData(2, 1.375f)]
    [InlineData(3, 1.55f)]
    [InlineData(5, 1.725f)]
    public void ApplyActivityMultiplier_DifferentDays_ReturnsCorrectMultiplier(int days, float multiplier)
    {
        var bmr = 1400f;

        var result = _calc.ApplyActivityMultiplier(bmr, days);

        Assert.Equal(bmr * multiplier, result, precision: 1);
    }

    [Theory]
    [InlineData(0, 0, 2000f - 300f)]   // FatLoss Steady
    [InlineData(0, 1, 2000f - 500f)]   // FatLoss Aggressive
    [InlineData(2, 0, 2000f + 300f)]   // Strength Steady
    [InlineData(1, 0, 2000f)]          // MuscleTone - no delta
    [InlineData(3, 0, 2000f)]          // MaintainHealth - no delta
    public void AdjustForGoal_VariousGoals_ReturnsExpected(int goal, int pace, float expected)
    {
        var result = _calc.AdjustForGoal(2000f, goal, pace);

        Assert.Equal(expected, result, precision: 0);
    }

    [Theory]
    [InlineData(0, 2000f - 100f)]  // Menstrual
    [InlineData(1, 2000f)]         // Follicular - no delta
    [InlineData(2, 2000f + 50f)]   // Ovulatory
    [InlineData(3, 2000f + 150f)]  // Luteal
    public void AdjustForPhase_VariousPhases_ReturnsExpected(int phase, float expected)
    {
        var result = _calc.AdjustForPhase(2000f, phase);

        Assert.Equal(expected, result, precision: 0);
    }

    [Fact]
    public void Clamp_BelowMin_ReturnsMin()
    {
        var result = _calc.Clamp(900f);
        Assert.Equal(1200f, result);
    }

    [Fact]
    public void Clamp_AboveMax_ReturnsMax()
    {
        var result = _calc.Clamp(5000f);
        Assert.Equal(4000f, result);
    }

    [Fact]
    public void Clamp_WithinRange_ReturnsValue()
    {
        var result = _calc.Clamp(2000f);
        Assert.Equal(2000f, result);
    }

    [Fact]
    public void CalculateMacros_TypicalValues_AllPositive()
    {
        var (protein, carbs, fats) = _calc.CalculateMacros(2000f, 65f);

        Assert.True(protein > 0);
        Assert.True(carbs > 0);
        Assert.True(fats > 0);
    }

    [Fact]
    public void CalculateMacros_ProteinIs1Point8PerKg()
    {
        var (protein, _, _) = _calc.CalculateMacros(2000f, 60f);

        Assert.Equal(60f * 1.8f, protein, precision: 1);
    }
}
