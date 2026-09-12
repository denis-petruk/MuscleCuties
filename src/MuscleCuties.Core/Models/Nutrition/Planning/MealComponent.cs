using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.Nutrition.Planning;

public sealed class MealComponent
{
    public MealConceptSlotType Type { get; }
    public IReadOnlyList<Ingredient> Ingredients { get; }

    public MealComponent(MealConceptSlotType type, IReadOnlyList<Ingredient> ingredients)
    {
        Type = type;
        Ingredients = ingredients;
    }

    public MacroNutrients Macros => MacroNutrients.Sum(Ingredients.Select(i => i.Macros));

    public float TotalCalories => Ingredients.Sum(i => i.Calories);
    public float TotalProtein => Ingredients.Sum(i => i.Protein);
    public float TotalCarbs => Ingredients.Sum(i => i.Carbs);
    public float TotalFats => Ingredients.Sum(i => i.Fats);
    public float TotalGrams => Ingredients.Sum(i => i.Grams);

    public bool IsVegetarian => Ingredients.All(i => i.IsVegetarian);
    public bool IsVegan => Ingredients.All(i => i.IsVegan);
    public bool IsGlutenFree => Ingredients.All(i => i.IsGlutenFree);
    public bool IsLactoseFree => Ingredients.All(i => i.IsLactoseFree);

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