using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.Nutrition.Planning;

// --- Calorie Distribution & Meal Planning Models ---

public sealed record DailyMealSuggestionPlan(
    float DailyTargetCalories,
    float RemainingCalories,
    IReadOnlyList<DailyMealSuggestion> Meals);

public sealed record DailyMealSuggestion(
    MealType MealType,
    MealNutritionTarget Target,
    bool IsAlreadyLogged,
    SuggestedMeal? Suggestion);

// --- Meal Suggestion Models ---

public sealed record MealConcept(
    string Name,
    string Description,
    IReadOnlyList<MealType> MealTypes,
    IReadOnlyList<MealConceptSlot> Slots,
    IReadOnlyList<SpiceBlendOption> SpiceBlends,
    IReadOnlySet<DietaryTag> IncompatibleDietaryTags,
    MealStyle? PreferredStyle = null);

public sealed record MealConceptSlot(
    MealConceptSlotType SlotType,
    IReadOnlyList<SlotIngredientEntry> IngredientEntries,
    bool Required);

public sealed record SlotIngredientEntry(
    IReadOnlyList<string> PreferredFoodTerms,
    float PortionShare,
    bool Required);

public sealed record PortionSolution(
    MealComponent CarbComponent,
    MealComponent ProteinComponent,
    MealComponent VitaminComponent,
    MealComponent? SauceComponent,
    MacroNutrients Total);

public sealed record SpiceBlendOption(
    string Name,
    string Description);

public sealed record SuggestedMeal(
    string ConceptName,
    string ConceptDescription,
    MealComponent CarbComponent,
    MealComponent ProteinComponent,
    MealComponent VitaminComponent,
    MealComponent? SauceComponent,
    MacroNutrients Macros,
    float Score,
    MealStyle Style,
    IReadOnlyList<SpiceBlendOption> SpiceBlends)
{
    public IReadOnlyList<MealComponent> AllComponents
    {
        get
        {
            var list = new List<MealComponent>(4) { CarbComponent, ProteinComponent, VitaminComponent };
            if (SauceComponent is not null)
                list.Add(SauceComponent);
            return list;
        }
    }

    public bool MatchesDietaryPreferences(IReadOnlySet<DietaryTag> tags)
    {
        return AllComponents.All(c => c.MatchesDietaryPreferences(tags));
    }
}
