using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public interface ICalorieDistributionService
{
    CalorieBudget Distribute(
        float totalCalories,
        MacroNutrients? macroPreference = null,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury);
}

public sealed class CalorieDistributionService : ICalorieDistributionService
{
    public CalorieBudget Distribute(
        float totalCalories,
        MacroNutrients? macroPreference = null,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury)
    {
        return MealDistributionRule.Distribute(totalCalories, macroPreference, breakfastPreference);
    }
}
