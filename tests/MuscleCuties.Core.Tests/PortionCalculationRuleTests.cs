using MuscleCuties.Core.Domain.Nutrition;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class PortionCalculationRuleTests
{
    private const float GramsPerHundred = 100f;
    private const float ExpectedOilCapGrams = 14f;
    private const float RelaxedMinimumCalorieRatio = 0.75f;
    private const float RelaxedMaximumCalorieRatio = 1.25f;
    private const int MacroPrecisionDigits = 3;
    private const int MaintenanceGoal = 1;
    private const int FatLossGoal = 0;
    private const float LowCalories = 300f;
    private const float MediumCalories = 600f;
    private const float HighCalories = 1200f;
    private const float ImpossibleCalories = 5000f;
    private const float LowProtein = 20f;
    private const float MediumProtein = 35f;
    private const float HighProtein = 65f;
    private const float LowCarbs = 25f;
    private const float MediumCarbs = 65f;
    private const float HighCarbs = 140f;
    private const float LowFats = 20f;
    private const float MediumFats = 35f;
    private const float HighFats = 60f;
    private const float LargeOilServingGrams = 30f;
    private const float DenseCarbCalories = 360f;
    private const float DenseCarbProtein = 10f;
    private const float DenseCarbCarbs = 75f;
    private const float DenseCarbFats = 3f;
    private const float ChickenCalories = 165f;
    private const float ChickenProtein = 31f;
    private const float ChickenFats = 3.6f;
    private const float RiceCalories = 123f;
    private const float RiceProtein = 2.7f;
    private const float RiceCarbs = 25.6f;
    private const float RiceFats = 1f;
    private const float RiceServingGrams = 50f;
    private const float BroccoliCalories = 35f;
    private const float BroccoliProtein = 2.4f;
    private const float BroccoliCarbs = 7.2f;
    private const float BroccoliFats = 0.4f;
    private const float OilCalories = 884f;
    private const float OilServingGrams = 5f;
    private const float SalmonCalories = 208f;
    private const float SalmonProtein = 20f;
    private const float SalmonFats = 13f;
    private const float SalmonMealCalories = 450f;
    private const float SalmonMealProtein = 35f;
    private const float SalmonMealCarbs = 40f;
    private const float SalmonMealFats = 10f;
    private const float HighFatMealCalories = 500f;
    private const float HighFatMealProtein = 40f;
    private const float HighFatMealCarbs = 50f;
    private const float HighFatMealFats = 30f;
    private const float VeryHighFatMealCalories = 700f;
    private const float VeryHighFatMealProtein = 50f;
    private const float VeryHighFatMealCarbs = 80f;
    private const float VeryHighFatMealFats = 40f;
    private const float ExtremeMealCalories = 1000f;
    private const float ExtremeMealProtein = 60f;
    private const float ExtremeMealCarbs = 120f;
    private const float ExtremeMealFats = 50f;

    private readonly PortionObservation _leanChicken = new(ChickenCalories, ChickenProtein, 0f, ChickenFats, GramsPerHundred);
    private readonly PortionObservation _brownRice = new(RiceCalories, RiceProtein, RiceCarbs, RiceFats, RiceServingGrams);
    private readonly PortionObservation _broccoli = new(BroccoliCalories, BroccoliProtein, BroccoliCarbs, BroccoliFats, RiceServingGrams);
    private readonly PortionObservation _oliveOil = new(OilCalories, 0f, 0f, GramsPerHundred, OilServingGrams);

    [Fact]
    public void MaxOilGramsIs14Grams()
    {
        Assert.Equal(ExpectedOilCapGrams, PortionCalculationRule.MaxOilGrams);
    }

    [Theory]
    [InlineData(HighFatMealCalories, HighFatMealProtein, HighFatMealCarbs, HighFatMealFats)]
    [InlineData(VeryHighFatMealCalories, VeryHighFatMealProtein, VeryHighFatMealCarbs, VeryHighFatMealFats)]
    [InlineData(ExtremeMealCalories, ExtremeMealProtein, ExtremeMealCarbs, ExtremeMealFats)]
    public void OilPortionNeverExceeds14GramsCapUnderAnyCircumstances(
        float calories, float protein, float carbs, float fats)
    {
        var result = PortionCalculationRule.Solve(
            targetCalories: calories,
            targetProtein: protein,
            targetCarbs: carbs,
            targetFats: fats,
            protein: _leanChicken,
            carb: _brownRice,
            veg: _broccoli,
            sauce: _oliveOil,
            userGoal: MaintenanceGoal,
            isRestDay: false,
            relaxed: true);

        Assert.NotNull(result);
        Assert.InRange(result.SauceGrams, 0f, PortionCalculationRule.MaxOilGrams);
    }

    [Theory]
    [InlineData(LowCalories, LowProtein, LowCarbs, LowFats)]
    [InlineData(MediumCalories, MediumProtein, MediumCarbs, MediumFats)]
    [InlineData(HighCalories, HighProtein, HighCarbs, HighFats)]
    public void CappedOilRebalancesWholeFoodsAndReportsActualMacros(
        float calories, float protein, float carbs, float fats)
    {
        var energyDenseCarb = new PortionObservation(DenseCarbCalories, DenseCarbProtein, DenseCarbCarbs, DenseCarbFats, 0f);
        var chicken = _leanChicken with { ServingGrams = 0f };
        var result = PortionCalculationRule.Solve(calories, protein, carbs, fats,
            chicken, energyDenseCarb, _broccoli, _oliveOil, MaintenanceGoal, false, true);

        Assert.NotNull(result);
        Assert.InRange(result.SauceGrams, 0f, PortionCalculationRule.MaxOilGrams);
        Assert.InRange(result.TotalCalories, calories * RelaxedMinimumCalorieRatio, calories * RelaxedMaximumCalorieRatio);
        Assert.Equal((chicken.Calories * result.ProteinGrams + energyDenseCarb.Calories * result.CarbGrams +
            _broccoli.Calories * result.VegGrams + _oliveOil.Calories * result.SauceGrams) / GramsPerHundred,
            result.TotalCalories, MacroPrecisionDigits);
        Assert.Equal((chicken.Protein * result.ProteinGrams + energyDenseCarb.Protein * result.CarbGrams +
            _broccoli.Protein * result.VegGrams + _oliveOil.Protein * result.SauceGrams) / GramsPerHundred,
            result.TotalProtein, MacroPrecisionDigits);
        Assert.Equal((chicken.Carbs * result.ProteinGrams + energyDenseCarb.Carbs * result.CarbGrams +
            _broccoli.Carbs * result.VegGrams + _oliveOil.Carbs * result.SauceGrams) / GramsPerHundred,
            result.TotalCarbs, MacroPrecisionDigits);
        Assert.Equal((chicken.Fats * result.ProteinGrams + energyDenseCarb.Fats * result.CarbGrams +
            _broccoli.Fats * result.VegGrams + _oliveOil.Fats * result.SauceGrams) / GramsPerHundred,
            result.TotalFats, MacroPrecisionDigits);
    }

    [Fact]
    public void LargeOilServingCannotSnapPastCap()
    {
        var largeServingOil = _oliveOil with { ServingGrams = LargeOilServingGrams };
        var result = PortionCalculationRule.Solve(MediumCalories, MediumProtein, MediumCarbs, MediumFats,
            _leanChicken, _brownRice, _broccoli, largeServingOil, MaintenanceGoal, false, true);
        Assert.NotNull(result);
        Assert.Equal(PortionCalculationRule.MaxOilGrams, result.SauceGrams);
    }

    [Fact]
    public void ImpossibleCalorieTargetReturnsNull()
    {
        var result = PortionCalculationRule.Solve(ImpossibleCalories, MediumProtein, MediumCarbs, MediumFats,
            _leanChicken, _brownRice, _broccoli, _oliveOil, MaintenanceGoal, false, true);
        Assert.Null(result);
    }

    [Fact]
    public void OilPortionIsZeroWhenFatsAreMetByWholeFoods()
    {
        var fattySalmon = new PortionObservation(SalmonCalories, SalmonProtein, 0f, SalmonFats, GramsPerHundred);
        var result = PortionCalculationRule.Solve(
            targetCalories: SalmonMealCalories,
            targetProtein: SalmonMealProtein,
            targetCarbs: SalmonMealCarbs,
            targetFats: SalmonMealFats,
            protein: fattySalmon,
            carb: _brownRice,
            veg: _broccoli,
            sauce: _oliveOil,
            userGoal: FatLossGoal,
            isRestDay: false,
            relaxed: true);

        Assert.NotNull(result);
        Assert.Equal(0f, result.SauceGrams);
    }
}
