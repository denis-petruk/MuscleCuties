using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Models.UI.Nutrition;

public class MealSuggestionItem
{
    public string ConceptName { get; set; } = string.Empty;
    public string ConceptDescription { get; set; } = string.Empty;
    public IReadOnlyList<string> SpiceBlendNames { get; set; } = [];
    public IReadOnlyList<MealSuggestionComponentItem> Components { get; set; } = [];
    public MacroNutrients Macros { get; set; }
    public float Score { get; set; }
    public MealStyle Style { get; set; }

    public string CaloriesText => $"{(int)Macros.Calories} kcal";
    public string MacrosText => $"P {Macros.Protein:N0}g  C {Macros.Carbs:N0}g  F {Macros.Fats:N0}g";
    public string SpiceBlendSummary => SpiceBlendNames.Count > 0
        ? string.Join(" | ", SpiceBlendNames)
        : string.Empty;
    public bool HasSpiceBlends => SpiceBlendNames.Count > 0;

    public string StyleLabel => Style switch
    {
        MealStyle.Bowl => "BOWL",
        MealStyle.Complex => "COMPLEX DISH",
        _ => "PLATED"
    };

    public string StyleDescription => Style switch
    {
        MealStyle.Bowl => "Mixed together in a bowl",
        MealStyle.Complex => "Cooked as one dish",
        _ => "Served separately on a plate"
    };

    public static MealSuggestionItem FromSuggestedMeal(SuggestedMeal meal)
    {
        return new MealSuggestionItem
        {
            ConceptName = meal.ConceptName,
            ConceptDescription = meal.ConceptDescription,
            SpiceBlendNames = meal.SpiceBlends.Select(s => s.Name).ToList(),
            Components = BuildComponents(meal.AllComponents),
            Macros = meal.Macros,
            Score = meal.Score,
            Style = meal.Style
        };
    }

    private static IReadOnlyList<MealSuggestionComponentItem> BuildComponents(
        IReadOnlyList<MealComponent> components)
    {
        return components
            .SelectMany(component => component.Ingredients.Select(ingredient =>
                new MealSuggestionComponentItem
                {
                    FoodItemId = ingredient.Food.Id,
                    Name = ingredient.Food.Name,
                    ComponentType = MapSlotToComponentType(component.Type),
                    Role = MapSlotToRole(component.Type),
                    Grams = ingredient.Grams,
                    IsLocked = false,
                    Calories = ingredient.Food.Calories,
                    Protein = ingredient.Food.Protein,
                    Carbs = ingredient.Food.Carbs,
                    Fats = ingredient.Food.Fats
                }))
            .ToList();
    }

    private static ComponentType MapSlotToComponentType(MealConceptSlotType slotType)
    {
        return slotType switch
        {
            MealConceptSlotType.CarbBase => ComponentType.Carb,
            MealConceptSlotType.ProteinBase => ComponentType.Protein,
            MealConceptSlotType.VitaminBase => ComponentType.Vegetable,
            MealConceptSlotType.Sauce => ComponentType.Sauce,
            _ => ComponentType.Mixed
        };
    }

    private static ComponentRole MapSlotToRole(MealConceptSlotType slotType)
    {
        return slotType switch
        {
            MealConceptSlotType.CarbBase => ComponentRole.CarbBase,
            MealConceptSlotType.ProteinBase => ComponentRole.ProteinBase,
            MealConceptSlotType.VitaminBase => ComponentRole.SideSalad,
            MealConceptSlotType.Sauce => ComponentRole.SauceOnSide,
            _ => ComponentRole.CarbBase
        };
    }
}

public class MealSuggestionComponentItem
{
    public int FoodItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ComponentType ComponentType { get; set; }
    public ComponentRole Role { get; set; }
    public float Grams { get; set; }
    public bool IsLocked { get; set; }
    public float Calories { get; set; }
    public float Protein { get; set; }
    public float Carbs { get; set; }
    public float Fats { get; set; }

    public string TypeLabel => ComponentType.ToString().ToUpperInvariant();

    public string RoleLabel => Role switch
    {
        ComponentRole.CarbBase => "BASE",
        ComponentRole.ProteinBase => "PROTEIN",
        ComponentRole.SideSalad => "SIDE SALAD",
        ComponentRole.InDishVegetable => "IN DISH",
        ComponentRole.SauceOnSide => "ON SIDE",
        ComponentRole.SaucePairedProtein => "WITH PROTEIN",
        ComponentRole.InDishSauce => "MIXED IN",
        _ => "ITEM"
    };

    public string PortionText => $"{Grams:N0}g";
    public string CaloriesText => $"{MacroNutrients.FromPer100g(Calories, Protein, Carbs, Fats, Grams).Calories:N0} kcal";
    public string DetailText => $"{Grams:N0}g · {MacroNutrients.FromPer100g(Calories, Protein, Carbs, Fats, Grams).ToNutritionText()}";
}