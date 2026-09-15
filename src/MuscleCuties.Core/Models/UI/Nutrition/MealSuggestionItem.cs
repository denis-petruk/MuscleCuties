using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Models.UI.Nutrition;

public class MealSuggestionItem
{
    public string ConceptName { get; set; } = string.Empty;
    public string ConceptDescription { get; set; } = string.Empty;
    public IReadOnlyList<SpiceBlendDetail> SpiceBlends { get; set; } = [];
    public IReadOnlyList<MealSuggestionComponentItem> Components { get; set; } = [];
    public IReadOnlyList<MealSuggestionComponentGroup> ComponentGroups { get; set; } = [];
    public MacroNutrients Macros { get; set; }
    public float Score { get; set; }
    public MealStyle Style { get; set; }
    public string IconGlyph { get; set; } = "Food24";
    public string IconPathData => IconGlyph switch
    {
        "BowlSalad24" => "M3 11h18c0 5-4 9-9 9s-9-4-9-9ZM7 11c0-3 2-5 5-5s5 2 5 5M12 3v3",
        "FoodCake24" => "M4 10h16v10H4zM4 14h16M8 10V7M12 10V6M16 10V7",
        "BowlChopsticks24" => "M4 13h16c-.7 4-3.8 7-8 7s-7.3-3-8-7ZM6 4l10 6M10 3l8 6",
        "FoodEgg24" => "M12 3c-3 0-7 6-7 11a7 7 0 0 0 14 0c0-5-4-11-7-11ZM12 11a3 3 0 1 0 0 6 3 3 0 0 0 0-6z",
        "FoodToast24" => "M5 9c0-4 3-6 7-6s7 2 7 6v10H5V9ZM8 13h8",
        "FoodPizza24" => "M12 3 3 20h18L12 3ZM7 16h10M11 10h.01M15 14h.01",
        "FoodChickenLeg24" => "M15 5a5 5 0 0 1 0 10c-1.8 0-3.4-1-4.3-2.4L7 16.3M5.7 15a2 2 0 1 0-2.8 2.8 2 2 0 1 0 2.8 2.8L9.4 17",
        "FoodCarrot24" => "M15 8 6 21l-3-3L15 8ZM14 7c1-3 3-4 6-4 0 3-1 5-4 6M16 8c3-1 5 0 6 2-3 1-5 1-7-1",
        "FoodGrains24" => "M12 21V8M12 11c-3 0-5-2-5-5 3 0 5 2 5 5ZM12 15c3 0 5-2 5-5-3 0-5 2-5 5ZM12 19c-3 0-5-2-5-5 3 0 5 2 5 5",
        "LeafThree24" => "M12 21V9M12 13c-4 0-7-3-7-7 4 0 7 3 7 7ZM12 17c4 0 7-3 7-7-4 0-7 3-7 7",
        "LeafTwo24" => "M5 20c7 0 13-5 14-15-10 1-15 7-14 15ZM5 20 16 8",
        "LeafOne24" => "M5 19c6 1 12-4 14-14C9 5 4 10 5 19ZM5 19l10-10",
        "FoodFish24" => "M3 12c3-4 7-6 12-4l4-3v6l2 1-2 1v6l-4-3c-5 2-9 0-12-4ZM9 11h.01",
        _ => "M3 2v7c0 1.1.9 2 2 2h2c1.1 0 2-.9 2-2V2M6 2v20M15 2v8h4M19 2v20"
    };

    public IReadOnlyList<string> SpiceBlendNames => SpiceBlends.Select(s => s.Name).ToList();

    public string CaloriesText => $"{(int)Macros.Calories} kcal";
    public string MacrosText => $"P {Macros.Protein:N0}g  C {Macros.Carbs:N0}g  F {Macros.Fats:N0}g";
    public string SpiceBlendSummary => SpiceBlends.Count > 0
        ? string.Join(" | ", SpiceBlendNames)
        : string.Empty;
    public bool HasSpiceBlends => SpiceBlends.Count > 0;

    public string ComponentSummary
    {
        get
        {
            var names = Components
                .GroupBy(c => c.ComponentType)
                .Select(g => g.First().Name)
                .ToList();
            return names.Count > 0 ? string.Join(" + ", names) : string.Empty;
        }
    }

    public string StyleLabel => Style switch
    {
        MealStyle.Bowl => "Bowl",
        MealStyle.Complex => "Complex dish",
        _ => "Plated"
    };

    public string StyleDescription => Style switch
    {
        MealStyle.Bowl => "Mixed together in a bowl",
        MealStyle.Complex => "Cooked as one dish",
        _ => "Served separately on a plate"
    };

    public string DirectionsText
    {
        get
        {
            var carb = Components.FirstOrDefault(c => c.ComponentType == ComponentType.Carb)?.Name ?? "carb base";
            var protein = Components.FirstOrDefault(c => c.ComponentType == ComponentType.Protein)?.Name ?? "protein";
            var veg = Components.FirstOrDefault(c => c.ComponentType == ComponentType.Vegetable)?.Name ?? "vegetables";
            var sauce = Components.FirstOrDefault(c => c.ComponentType == ComponentType.Sauce)?.Name;
            var spice = SpiceBlends.Count > 0 ? SpiceBlends[0].Name : null;

            var steps = Style switch
            {
                MealStyle.Bowl => BuildBowlDirections(carb, protein, veg, sauce, spice),
                MealStyle.Complex => BuildComplexDirections(carb, protein, veg, sauce, spice),
                _ => BuildPlatedDirections(carb, protein, veg, sauce, spice)
            };

            return string.Join("\n", steps.Select((s, i) => $"{i + 1}. {s}"));
        }
    }

    public static MealSuggestionItem FromSuggestedMeal(SuggestedMeal meal)
    {
        var components = BuildComponents(meal.AllComponents);
        return new MealSuggestionItem
        {
            ConceptName = meal.ConceptName,
            ConceptDescription = meal.ConceptDescription,
            SpiceBlends = meal.SpiceBlends.Select((s, i) =>
                new SpiceBlendDetail(s.Name, s.Description, i == 0)).ToList(),
            Components = components,
            ComponentGroups = BuildComponentGroups(components),
            Macros = meal.Macros,
            Score = meal.Score,
            Style = meal.Style,
            IconGlyph = ResolveIconGlyph(meal.ConceptName)
        };
    }

    private static string ResolveIconGlyph(string conceptName)
    {
        return conceptName switch
        {
            "Berry Oat Bowl" => "BowlSalad24",
            "Yogurt Parfait" => "FoodCake24",
            "PB Banana Oats" => "BowlChopsticks24",
            "Shakshuka" => "FoodEgg24",
            "Avocado Toast" => "FoodToast24",
            "Breakfast Taco" => "FoodPizza24",
            "Breakfast Wrap" => "FoodChickenLeg24",
            "Breakfast Burger" => "Food24",
            "Egg and Potato Plate" => "FoodCarrot24",
            "Loaded Grain Bowl" => "FoodGrains24",
            "Protein Wrap" => "FoodToast24",
            "Bean Power Plate" => "LeafThree24",
            "Taco Bowl" => "BowlSalad24",
            "Loaded Potato" => "FoodCarrot24",
            "Quinoa Salad Plate" => "LeafTwo24",
            "Protein Sweet Potato Plate" => "FoodChickenLeg24",
            "Bean Stew Bowl" => "BowlChopsticks24",
            "Rice Protein Bowl" => "FoodGrains24",
            "Stuffed Potato" => "Food24",
            "Lentil Power Bowl" => "LeafOne24",
            "Mini Sandwich" => "FoodToast24",
            "Protein Snack Plate" => "FoodFish24",
            "Yogurt Bowl" => "FoodCake24",
            _ => "BowlSalad24"
        };
    }

    private static IReadOnlyList<MealSuggestionComponentGroup> BuildComponentGroups(
        IReadOnlyList<MealSuggestionComponentItem> components)
    {
        var groups = new List<MealSuggestionComponentGroup>();
        var ordered = new[]
        {
            (ComponentType.Carb, "Carb base", "Food24"),
            (ComponentType.Protein, "Protein", "FoodEgg24"),
            (ComponentType.Vegetable, "Salad / Vegetables", "LeafThree24"),
            (ComponentType.Sauce, "Sauce", "Drop24")
        };

        foreach (var (type, label, icon) in ordered)
        {
            var items = components.Where(c => c.ComponentType == type).ToList();
            if (items.Count > 0)
                groups.Add(new MealSuggestionComponentGroup(label, icon, items));
        }

        return groups;
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

    private static List<string> BuildBowlDirections(
        string carb, string protein, string veg, string? sauce, string? spice)
    {
        var steps = new List<string>
        {
            $"Cook {carb} and place in a bowl as the base.",
            $"Prepare {protein} and arrange on top.",
            $"Add {veg} fresh or lightly tossed."
        };
        if (sauce is not null)
            steps.Add($"Drizzle {sauce} over everything.");
        if (spice is not null)
            steps.Add($"Season with {spice} to finish.");
        steps.Add("Toss gently or eat layered.");
        return steps;
    }

    private static List<string> BuildPlatedDirections(
        string carb, string protein, string veg, string? sauce, string? spice)
    {
        var steps = new List<string>
        {
            $"Cook {carb} and serve on one side of the plate.",
            $"Prepare {protein} and place alongside."
        };
        if (spice is not null)
            steps.Add($"Season {protein} with {spice} before or after cooking.");
        steps.Add($"Arrange {veg} on the side as a fresh salad or steamed.");
        if (sauce is not null)
            steps.Add($"Serve {sauce} on the side or drizzle lightly.");
        return steps;
    }

    private static List<string> BuildComplexDirections(
        string carb, string protein, string veg, string? sauce, string? spice)
    {
        var steps = new List<string>();
        if (spice is not null)
            steps.Add($"Season {protein} with {spice}.");
        steps.Add($"Cook {protein} in a pan until done.");
        steps.Add($"Add {veg} and cook together briefly.");
        if (sauce is not null)
            steps.Add($"Stir in {sauce} and let it coat evenly.");
        steps.Add($"Serve over {carb}.");
        return steps;
    }
}

public record SpiceBlendDetail(string Name, string Description, bool IsRecommended);

public record MealSuggestionComponentGroup(
    string Label,
    string IconGlyph,
    IReadOnlyList<MealSuggestionComponentItem> Items)
{
    public string TotalText
    {
        get
        {
            var totalGrams = Items.Sum(i => i.Grams);
            var macros = MacroNutrients.Sum(Items.Select(i =>
                MacroNutrients.FromPer100g(i.Calories, i.Protein, i.Carbs, i.Fats, i.Grams)));
            return $"{totalGrams:N0}g · {macros.Calories:N0} kcal";
        }
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

    public string TypeLabel => ComponentType.ToString();

    public string RoleLabel => Role switch
    {
        ComponentRole.CarbBase => "Base",
        ComponentRole.ProteinBase => "Protein",
        ComponentRole.SideSalad => "Side salad",
        ComponentRole.InDishVegetable => "In dish",
        ComponentRole.SauceOnSide => "On side",
        ComponentRole.SaucePairedProtein => "With protein",
        ComponentRole.InDishSauce => "Mixed in",
        _ => "Item"
    };

    public string PortionText => $"{Grams:N0}g";
    public string CaloriesText => $"{MacroNutrients.FromPer100g(Calories, Protein, Carbs, Fats, Grams).Calories:N0} kcal";
    public string DetailText => $"{Grams:N0}g · {MacroNutrients.FromPer100g(Calories, Protein, Carbs, Fats, Grams).ToNutritionText()}";
}
