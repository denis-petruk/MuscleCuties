using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.Nutrition.Planning;

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
