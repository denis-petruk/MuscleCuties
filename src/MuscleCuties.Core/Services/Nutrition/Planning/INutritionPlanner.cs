using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public interface INutritionPlanner
{
    NutritionPlan CreateDailyPlan(UserProfile profile, CyclePhase phase, DateTime date);
    NutritionPlan CreateFallbackPlan(CyclePhase phase);
}
