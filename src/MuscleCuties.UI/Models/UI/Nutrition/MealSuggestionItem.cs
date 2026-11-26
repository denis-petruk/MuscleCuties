using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>A generated meal: its components, macros, style, and the cooking directions assembled from those</summary>
/// components.</summary>
public class MealSuggestionItem
{
    /// <summary>Name of the generated dish.</summary>
    public string ConceptName { get; set; } = string.Empty;
    /// <summary>One-line description of the dish.</summary>
    public string ConceptDescription { get; set; } = string.Empty;
    /// <summary>Spice blends paired with the dish, the first of which appears in the directions.</summary>
    public IReadOnlyList<SpiceBlendDetail> SpiceBlends { get; set; } = [];
    /// <summary>The foods in the dish, flat; the grouped view is derived from this list.</summary>
    public IReadOnlyList<MealSuggestionComponentItem> Components { get; set; } = [];
    /// <summary>The same components grouped under the headings the card shows.</summary>
    public IReadOnlyList<MealSuggestionComponentGroup> ComponentGroups { get; set; } = [];
    /// <summary>Totals for the whole dish.</summary>
    public NutritionMacros Macros { get; set; }
    /// <summary>Planner score, used to order or compare suggestions; not shown to the person.</summary>
    public float Score { get; set; }
    /// <summary>Shape of the dish, which selects its label, description and direction template.</summary>
    public MealStyle Style { get; set; }
    /// <summary>Glyph for the dish, chosen from its style and components.</summary>
    public string IconGlyph { get; set; } = "Food24";

    /// <summary>Outline path for the dish's glyph.</summary>
    public string IconPathData => IconGlyph switch
    {
        "BowlSalad24" => "M3 11h18c0 5-4 9-9 9s-9-4-9-9ZM7 11c0-3 2-5 5-5s5 2 5 5M12 3v3",
        "FoodCake24" => "M4 10h16v10H4zM4 14h16M8 10V7M12 10V6M16 10V7",
        "BowlChopsticks24" => "M4 13h16c-.7 4-3.8 7-8 7s-7.3-3-8-7ZM6 4l10 6M10 3l8 6",
        "FoodEgg24" => "M12 3c-3 0-7 6-7 11a7 7 0 0 0 14 0c0-5-4-11-7-11ZM12 11a3 3 0 1 0 0 6 3 3 0 0 0 0-6z",
        "FoodToast24" => "M5 9c0-4 3-6 7-6s7 2 7 6v10H5V9ZM8 13h8",
        "FoodPizza24" => "M12 3 3 20h18L12 3ZM7 16h10M11 10h.01M15 14h.01",
        "FoodChickenLeg24" =>
            "M15 5a5 5 0 0 1 0 10c-1.8 0-3.4-1-4.3-2.4L7 16.3M5.7 15a2 2 0 1 0-2.8 2.8 2 2 0 1 0 2.8 2.8L9.4 17",
        "FoodCarrot24" => "M15 8 6 21l-3-3L15 8ZM14 7c1-3 3-4 6-4 0 3-1 5-4 6M16 8c3-1 5 0 6 2-3 1-5 1-7-1",
        "FoodGrains24" =>
            "M12 21V8M12 11c-3 0-5-2-5-5 3 0 5 2 5 5ZM12 15c3 0 5-2 5-5-3 0-5 2-5 5ZM12 19c-3 0-5-2-5-5 3 0 5 2 5 5",
        "LeafThree24" => "M12 21V9M12 13c-4 0-7-3-7-7 4 0 7 3 7 7ZM12 17c4 0 7-3 7-7-4 0-7 3-7 7",
        "LeafTwo24" => "M5 20c7 0 13-5 14-15-10 1-15 7-14 15ZM5 20 16 8",
        "LeafOne24" => "M5 19c6 1 12-4 14-14C9 5 4 10 5 19ZM5 19l10-10",
        "FoodFish24" => "M3 12c3-4 7-6 12-4l4-3v6l2 1-2 1v6l-4-3c-5 2-9 0-12-4ZM9 11h.01",
        _ => "M3 2v7c0 1.1.9 2 2 2h2c1.1 0 2-.9 2-2V2M6 2v20M15 2v8h4M19 2v20"
    };

    /// <summary>Names of the paired spice blends.</summary>
    /// <summary>Calorie total, formatted.</summary>
    public string CaloriesText => DisplayFormat.Calories(Macros.Calories);
    /// <summary>Macro line for the dish.</summary>
    public string MacrosText => DisplayFormat.Macros(Macros.Protein, Macros.Carbs, Macros.Fats);

    /// <summary>True when the suggestion has spice blends listed.</summary>
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

    /// <summary>Localized name of the dish's style.</summary>
    public string StyleLabel => UiTextCatalog.Get(Style switch
    {
        MealStyle.Bowl => "Nutrition_MealStyle_Bowl",
        MealStyle.Complex => "Nutrition_MealStyle_ComplexDish",
        MealStyle.Handheld => "Nutrition_MealStyle_Handheld",
        MealStyle.Wrapped => "Nutrition_MealStyle_Wrap",
        _ => "Nutrition_MealStyle_Plated"
    });

    /// <summary>Localized explanation of what the style means.</summary>
    public string StyleDescription => UiTextCatalog.Get(Style switch
    {
        MealStyle.Bowl => "Nutrition_MealStyle_BowlDescription",
        MealStyle.Complex => "Nutrition_MealStyle_ComplexDishDescription",
        MealStyle.Handheld => "Nutrition_MealStyle_HandheldDescription",
        MealStyle.Wrapped => "Nutrition_MealStyle_WrapDescription",
        _ => "Nutrition_MealStyle_PlatedDescription"
    });

    public string DirectionsText
    {
        get
        {
            var carb = Components.FirstOrDefault(c => c.ComponentType == ComponentType.Carb)?.Name ??
                UiTextCatalog.Get("Nutrition_Directions_CarbFallback");
            var protein = Components.FirstOrDefault(c => c.ComponentType == ComponentType.Protein)?.Name ??
                UiTextCatalog.Get("Nutrition_Directions_ProteinFallback");
            var veg = Components.FirstOrDefault(c => c.ComponentType == ComponentType.Vegetable)?.Name ??
                UiTextCatalog.Get("Nutrition_Directions_VegetableFallback");
            var sauce = Components.FirstOrDefault(c => c.ComponentType == ComponentType.Sauce)?.Name;
            var spice = SpiceBlends.Count > 0 ? SpiceBlends[0].Name : null;

            var steps = Style switch
            {
                MealStyle.Bowl => BuildBowlDirections(carb, protein, veg, sauce, spice),
                MealStyle.Complex => BuildComplexDirections(carb, protein, veg, sauce, spice),
                MealStyle.Handheld => BuildHandheldDirections(carb, protein, veg, sauce, spice),
                MealStyle.Wrapped => BuildWrappedDirections(carb, protein, veg, sauce, spice),
                _ => BuildPlatedDirections(carb, protein, veg, sauce, spice)
            };

            return string.Join("\n", steps.Select((s, i) => $"{i + 1}. {s}"));
        }
    }

    /// <summary>The steps for a bowl-style meal.</summary>
    private static List<string> BuildBowlDirections(
        string carb, string protein, string veg, string? sauce, string? spice)
    {
        var steps = new List<string>
        {
            UiTextCatalog.Format("Nutrition_Directions_CookBaseBowl", carb),
            UiTextCatalog.Format("Nutrition_Directions_PrepareAndArrange", protein),
            UiTextCatalog.Format("Nutrition_Directions_AddVegetables", veg)
        };
        if (sauce is not null)
            steps.Add(UiTextCatalog.Format("Nutrition_Directions_DrizzleSauce", sauce));
        if (spice is not null)
            steps.Add(UiTextCatalog.Format("Nutrition_Directions_SeasonWith", spice));
        steps.Add(UiTextCatalog.Get("Nutrition_Directions_TossOrLayer"));
        return steps;
    }

    /// <summary>The steps for a meal built from separate parts.</summary>
    private static List<string> BuildComplexDirections(
        string carb, string protein, string veg, string? sauce, string? spice)
    {
        var steps = StartProteinSteps(protein, spice, brown: true);
        steps.Add(UiTextCatalog.Format("Nutrition_Directions_StirInVegetables", veg));
        if (sauce is not null)
            steps.Add(UiTextCatalog.Format("Nutrition_Directions_PourSauce", sauce));
        steps.Add(UiTextCatalog.Format("Nutrition_Directions_ServeOverCarb", carb));
        return steps;
    }

    /// <summary>The steps for a handheld meal.</summary>
    private static List<string> BuildHandheldDirections(
        string carb, string protein, string veg, string? sauce, string? spice)
    {
        var steps = StartProteinSteps(protein, spice, brown: false);
        steps.Add(UiTextCatalog.Format("Nutrition_Directions_ToastCarb", carb));
        steps.Add(UiTextCatalog.Format("Nutrition_Directions_LayerProtein", protein, carb));
        steps.Add(UiTextCatalog.Format("Nutrition_Directions_TopWith", veg));
        if (sauce is not null)
            steps.Add(UiTextCatalog.Format("Nutrition_Directions_FinishSpread", sauce));
        steps.Add(UiTextCatalog.Get("Nutrition_Directions_CloseAndEnjoy"));
        return steps;
    }

    /// <summary>The steps for a wrapped meal.</summary>
    private static List<string> BuildWrappedDirections(
        string carb, string protein, string veg, string? sauce, string? spice)
    {
        var steps = StartProteinSteps(protein, spice, brown: false);
        steps.Add(UiTextCatalog.Format("Nutrition_Directions_WarmUntilPliable", carb));
        if (sauce is not null)
            steps.Add(UiTextCatalog.Format("Nutrition_Directions_SpreadSauce", sauce, carb));
        steps.Add(UiTextCatalog.Format("Nutrition_Directions_AddProteinAndVeg", protein, veg));
        steps.Add(UiTextCatalog.Get("Nutrition_Directions_FoldAndServe"));
        return steps;
    }

    /// <summary>The steps for a plated meal.</summary>
    private static List<string> BuildPlatedDirections(
        string carb, string protein, string veg, string? sauce, string? spice)
    {
        var steps = new List<string>
        {
            UiTextCatalog.Format("Nutrition_Directions_CookServeSide", carb),
            UiTextCatalog.Format("Nutrition_Directions_PrepareAlongside", protein)
        };
        if (spice is not null)
            steps.Add(UiTextCatalog.Format("Nutrition_Directions_SeasonBeforeAfter", protein, spice));
        steps.Add(UiTextCatalog.Format("Nutrition_Directions_ArrangeVeg", veg));
        if (sauce is not null)
            steps.Add(UiTextCatalog.Format("Nutrition_Directions_ServeSauceSide", sauce));
        return steps;
    }

    /// <summary>Opening steps shared by the wrapped and handheld templates: season, then brown or cook the</summary>
    /// protein.</summary>
    private static List<string> StartProteinSteps(string protein, string? spice, bool brown)
    {
        var steps = new List<string>();
        if (spice is not null)
            steps.Add(UiTextCatalog.Format("Nutrition_Directions_SeasonProtein", protein, spice));
        steps.Add(UiTextCatalog.Format(
            brown ? "Nutrition_Directions_BrownProtein" : "Nutrition_Directions_CookProtein",
            protein));
        return steps;
    }
}

/// <summary>A spice blend paired with a dish, and whether the planner recommends it.</summary>
public record SpiceBlendDetail(string Name, string Description, bool IsRecommended);

/// <summary>A heading, its glyph, and the components listed under it.</summary>
public record MealSuggestionComponentGroup(
    string Label,
    string IconGlyph,
    IReadOnlyList<MealSuggestionComponentItem> Items)
{
    /// <summary>Group weight and calories, summed over the components in it.</summary>
    public string TotalText => $"{DisplayFormat.GramsShort(Items.Sum(i => i.Grams))} · {DisplayFormat.Calories(Items.Sum(i => i.CaloriesForAmount))}";
}

/// <summary>One food in a suggestion, with both its per-100 g values and the values for its portion.</summary>
public class MealSuggestionComponentItem
{
    /// <summary>Catalog identity of the component.</summary>
    public int FoodItemId { get; set; }
    /// <summary>Component name as it appears in the dish.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Role the food plays in the dish (carb, protein, vegetable, sauce).</summary>
    public ComponentType ComponentType { get; set; }
    /// <summary>Finer role that decides how the food is served.</summary>
    public ComponentRole Role { get; set; }
    /// <summary>Portion weight in the dish.</summary>
    public float Grams { get; set; }
    /// <summary>True when the component is fixed to the component type's default and was not adjusted.</summary>
    public bool IsLocked { get; set; }
    /// <summary>Calories per 100 g.</summary>
    public float Calories { get; set; }
    /// <summary>Protein per 100 g.</summary>
    public float Protein { get; set; }
    /// <summary>Carbohydrate per 100 g.</summary>
    public float Carbs { get; set; }
    /// <summary>Fat per 100 g.</summary>
    public float Fats { get; set; }
    /// <summary>Calories the portion contributes, which is what the dish total is built from.</summary>
    public float CaloriesForAmount { get; set; }
    /// <summary>Preformatted macro line for the portion.</summary>
    public string NutritionForAmountText { get; set; } = string.Empty;

    /// <summary>Calories the portion contributes, formatted.</summary>
    public string CaloriesText => DisplayFormat.Calories(CaloriesForAmount);

    /// <summary>Portion and its macros on one line.</summary>
    public string DetailText => $"{DisplayFormat.GramsShort(Grams)} · {NutritionForAmountText}";
}

/// <summary>One meal slot in the generated day plan, including why it has no suggestion when it has none.</summary>
public sealed record DayMealPlanItem(
    MealType MealType,
    NutritionMealTarget Target,
    float DailyCaloriesTarget,
    bool IsAlreadyLogged,
    bool IsRefreshing,
    MealSuggestionItem? Suggestion,
    string ErrorMessage = "")
{
    /// <summary>Localized meal name.</summary>
    public string MealTypeLabel => UiTextCatalog.Get(MealType switch
    {
        MealType.Breakfast => "Nutrition_MealType_Breakfast",
        MealType.Lunch => "Nutrition_MealType_Lunch",
        MealType.Dinner => "Nutrition_MealType_Dinner",
        MealType.Snack => "Nutrition_MealType_Snack",
        _ => "Nutrition_MealType_Snack"
    });

    /// <summary>Outline glyph for the meal type.</summary>
    public string MealTypeIconPathData => MealType switch
    {
        MealType.Breakfast => "M17 18a5 5 0 0 0 0-10H5a5 5 0 0 0 0 10zM2 21h20M12 3v2",
        MealType.Lunch =>
            "M12 2a8.5 8.5 0 0 1 8.5 8.5c0 3.7-2.4 6.8-5.7 8H9.2c-3.3-1.2-5.7-4.3-5.7-8A8.5 8.5 0 0 1 12 2zM12 2v4M4.93 10h14.14",
        MealType.Dinner => "M3 16h18M5 14a7 7 0 0 1 14 0M12 7V5M2 19h20",
        MealType.Snack => "M12 2a10 10 0 0 0-6.88 2.77A4 4 0 0 1 8 8a4 4 0 0 1-3.54 3.97A10 10 0 1 0 12 2z",
        _ => "M12 2a10 10 0 0 0 0 20 10 10 0 0 0 0-20z"
    };

    /// <summary>The slot's calorie target and its share of the day, for the slot header.</summary>
    public string TargetSummaryText =>
        UiTextCatalog.Format("Nutrition_DayPlan_TargetSummaryFormat",
            DisplayFormat.Calories(Target.Calories), TargetSharePercent);

    /// <summary>Share of the daily target this slot represents, clamped to 0..100 and zero when no target is set.</summary>
    public int TargetSharePercent => !float.IsFinite(DailyCaloriesTarget) || DailyCaloriesTarget <= 0f ||
                                     !float.IsFinite(Target.Calories)
        ? 0
        : Math.Clamp((int)MathF.Round(Target.Calories / DailyCaloriesTarget * 100f), 0, 100);

    /// <summary>Slot title: the suggested dish, or the reason there is none (already logged, target reached,</summary>
    /// nothing suitable).</summary>
    public string MealTitle => Suggestion?.ConceptName ?? UiTextCatalog.Get(
        IsAlreadyLogged
            ? "Nutrition_DayPlan_AlreadyLogged"
            : Target.Calories < 50f
                ? "Nutrition_DayPlan_DailyTargetReached"
                : "Nutrition_DayPlan_NoSuitableMeal");

    /// <summary>Explains the slot's state, preferring the reason over an empty summary.</summary>
    public string MealSummaryText => IsAlreadyLogged
        ? UiTextCatalog.Get("Nutrition_DayPlan_AlreadyLoggedSummary")
        : IsRefreshing
            ? UiTextCatalog.Get("Nutrition_DayPlan_FindingAnotherMeal")
            : Target.Calories < 50f
                ? UiTextCatalog.Get("Nutrition_DayPlan_NoCaloriesRemain")
                : Suggestion?.ComponentSummary ??
                  (string.IsNullOrWhiteSpace(ErrorMessage)
                      ? UiTextCatalog.Get("Nutrition_DayPlan_NoMealMatched")
                      : ErrorMessage);

    /// <summary>The suggested dish's macro line; empty when there is no suggestion.</summary>
    public string MacrosText => Suggestion?.MacrosText ?? string.Empty;
    /// <summary>True when a suggestion is attached.</summary>
    public bool HasSuggestion => Suggestion is not null;
    /// <summary>Another suggestion is worth asking for only while calories remain and nothing is already logged.</summary>
    public bool CanRefresh => !IsAlreadyLogged && !IsRefreshing && Target.Calories >= 50f;
}
