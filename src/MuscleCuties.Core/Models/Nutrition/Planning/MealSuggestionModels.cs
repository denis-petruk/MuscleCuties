using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.Nutrition.Planning;

// --- Calorie Distribution & Meal Planning Models ---

public sealed record CalorieBudget(
    float TotalCalories,
    float MainMealCalories,
    float SnackCalories,
    float MainMealShare,
    float SnackShare,
    MacroNutrients DailyMacros,
    IReadOnlyList<MealCalorieAllocation> Allocations)
{
    public MealCalorieAllocation? GetAllocation(MealType mealType)
        => Allocations.FirstOrDefault(a => a.MealType == mealType);
}

public sealed record MealCalorieAllocation(
    MealType MealType,
    float Calories,
    float Share,
    MacroNutrients Macros);

public sealed record FallbackMealItem(
    string Name,
    string Description,
    MealType MealType,
    MacroNutrients Macros,
    IReadOnlyList<string> IngredientNames)
{
    public float Calories => Macros.Calories;
}

public sealed record MealPlan(
    CalorieBudget Budget,
    IReadOnlyList<MealPlanEntry> Entries)
{
    public MacroNutrients TotalMacros
        => MacroNutrients.Sum(Entries.Select(e => e.Meal.Macros));

    public IReadOnlyList<MealPlanEntry> MainMeals
        => Entries.Where(e => e.Meal.MealType is not MealType.Snack).ToList();

    public IReadOnlyList<MealPlanEntry> Snacks
        => Entries.Where(e => e.Meal.MealType is MealType.Snack).ToList();
}

public sealed record MealPlanEntry(
    FallbackMealItem Meal,
    MealCalorieAllocation Target);

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
    IReadOnlySet<DietaryTag> IncompatibleDietaryTags);

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
        => AllComponents.All(c => c.MatchesDietaryPreferences(tags));
}
