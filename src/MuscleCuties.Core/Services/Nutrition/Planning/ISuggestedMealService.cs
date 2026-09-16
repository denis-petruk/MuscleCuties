using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public interface ISuggestedMealService
{
    Task<IReadOnlyList<SuggestedMeal>> SuggestAsync(
        int userId,
        MealType mealType,
        CyclePhase phase,
        DateTime date,
        float consumedCalories,
        IReadOnlySet<string>? excludeConceptNames = null);
}
