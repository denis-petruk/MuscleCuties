using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public interface INutritionPlanner
{
    NutritionPlan CreateDailyPlan(
        UserProfile profile,
        CyclePhase phase,
        DateTime date,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury);

    NutritionPlan CreateFallbackPlan(
        CyclePhase phase,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury);
}
