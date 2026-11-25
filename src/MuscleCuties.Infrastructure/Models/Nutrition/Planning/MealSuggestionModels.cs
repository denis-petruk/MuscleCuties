using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.Nutrition.Planning;

// --- Meal Suggestion Models ---

/// <summary>A meal idea the planner can build a suggestion from, with the slots it needs filled.</summary>
public sealed record MealConcept(
    string Name,
    string Description,
    IReadOnlyList<MealType> MealTypes,
    IReadOnlyList<MealConceptSlot> Slots,
    IReadOnlyList<SpiceBlendOption> SpiceBlends,
    IReadOnlySet<DietaryTag> IncompatibleDietaryTags,
    MealStyle? PreferredStyle = null);

/// <summary>One slot of a meal idea: what kind of food goes there and how much of the meal's energy it carries.</summary>
public sealed record MealConceptSlot(
    MealConceptSlotType SlotType,
    IReadOnlyList<SlotIngredientEntry> IngredientEntries,
    bool Required);

/// <summary>An ingredient chosen for a slot, with the weight it was scaled to.</summary>
public sealed record SlotIngredientEntry(
    IReadOnlyList<string> PreferredFoodTerms,
    float PortionShare,
    bool Required);


/// <summary>A spice blend that can season a dish without changing its macros.</summary>
public sealed record SpiceBlendOption(
    string Name,
    string Description);
