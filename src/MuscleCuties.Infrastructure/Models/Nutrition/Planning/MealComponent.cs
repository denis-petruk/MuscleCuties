using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.Nutrition.Planning;

/// <summary>One part of a planned meal — a slot's ingredients — with the totals and dietary verdicts the planner filters</summary>
/// on.</summary>
public sealed class MealComponent
{
    /// <summary>Takes the slot it fills and the ingredients chosen for it.</summary>
    public MealComponent(MealConceptSlotType type, IReadOnlyList<Ingredient> ingredients)
    {
        Type = type;
        Ingredients = ingredients;
    }

    /// <summary>Slot of the meal the component fills.</summary>
    public MealConceptSlotType Type { get; }
    /// <summary>Ingredients the component uses.</summary>
    public IReadOnlyList<Ingredient> Ingredients { get; }

    /// <summary>The component's macros, summed over its ingredients.</summary>
    public MacroNutrients Macros => MacroNutrients.Sum(Ingredients.Select(i => i.Macros));

    /// <summary>The component's energy at the weights chosen.</summary>
    public float TotalCalories => Ingredients.Sum(i => i.Calories);
    /// <summary>The component's protein.</summary>
    public float TotalProtein => Ingredients.Sum(i => i.Protein);
    /// <summary>The component's carbohydrate.</summary>
    public float TotalCarbs => Ingredients.Sum(i => i.Carbs);
    /// <summary>The component's fat.</summary>
    public float TotalFats => Ingredients.Sum(i => i.Fats);
    /// <summary>The total weight, which the plan uses to keep portions believable.</summary>
    public float TotalGrams => Ingredients.Sum(i => i.Grams);

    /// <summary>Whether every ingredient reads as vegetarian, since one exception makes the whole component not.</summary>
    public bool IsVegetarian => Ingredients.All(i => i.IsVegetarian);
    /// <summary>The same all-ingredients rule for vegan.</summary>
    public bool IsVegan => Ingredients.All(i => i.IsVegan);
    /// <summary>The same all-ingredients rule for gluten free.</summary>
    public bool IsGlutenFree => Ingredients.All(i => i.IsGlutenFree);
    /// <summary>The same all-ingredients rule for lactose free.</summary>
    public bool IsLactoseFree => Ingredients.All(i => i.IsLactoseFree);

    /// <summary>Whether the component satisfies every requested tag, which is what admits it to the meal.</summary>
    public bool MatchesDietaryPreferences(IReadOnlySet<DietaryTag> tags)
    {
        if (tags.Count == 0)
            return true;

        foreach (var tag in tags)
        {
            var passes = tag switch
            {
                DietaryTag.Vegetarian => IsVegetarian,
                DietaryTag.Vegan => IsVegan,
                DietaryTag.GlutenFree => IsGlutenFree,
                DietaryTag.LactoseFree => IsLactoseFree,
                _ => true
            };

            if (!passes)
                return false;
        }

        return true;
    }
}
