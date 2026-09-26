using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public interface ISuggestedMealService
{
    Task<IReadOnlyList<SuggestedMeal>> SuggestAsync(
        int userId,
        MealType mealType,
        BreakfastPreference breakfastPreference,
        CyclePhase phase,
        DateTime date,
        float consumedCalories,
        IReadOnlySet<string>? excludeConceptNames = null,
        MealNutritionTarget? targetOverride = null);

    Task<IReadOnlyDictionary<MealType, IReadOnlyList<SuggestedMeal>>> SuggestPlanAsync(
        int userId,
        IReadOnlyList<MealNutritionTarget> targets,
        BreakfastPreference breakfastPreference,
        CyclePhase phase,
        DateTime date,
        UserProfile? profile,
        IReadOnlySet<string>? excludeConceptNames = null);
}
