using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.UI.Models.UI.Nutrition;

public class MealSuggestionItem
{
    public string ConceptName { get; set; } = string.Empty;
    public string ConceptDescription { get; set; } = string.Empty;
    public IReadOnlyList<SpiceBlendDetail> SpiceBlends { get; set; } = [];
    public IReadOnlyList<MealSuggestionComponentItem> Components { get; set; } = [];
    public IReadOnlyList<MealSuggestionComponentGroup> ComponentGroups { get; set; } = [];
    public NutritionMacros Macros { get; set; }
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

    public IReadOnlyList<string> SpiceBlendNames => SpiceBlends.Select(s => s.Name).ToList();

    public string CaloriesText => DisplayFormat.Calories(Macros.Calories);
    public string MacrosText => DisplayFormat.Macros(Macros.Protein, Macros.Carbs, Macros.Fats);

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

    public string StyleLabel => UiTextCatalog.Get(Style switch
    {
        MealStyle.Bowl => "Nutrition_MealStyle_Bowl",
        MealStyle.Complex => "Nutrition_MealStyle_ComplexDish",
        MealStyle.Handheld => "Nutrition_MealStyle_Handheld",
        MealStyle.Wrapped => "Nutrition_MealStyle_Wrap",
        _ => "Nutrition_MealStyle_Plated"
    });

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

public record SpiceBlendDetail(string Name, string Description, bool IsRecommended);

public record MealSuggestionComponentGroup(
    string Label,
    string IconGlyph,
    IReadOnlyList<MealSuggestionComponentItem> Items)
{
    public string TotalText => $"{DisplayFormat.GramsShort(Items.Sum(i => i.Grams))} · {DisplayFormat.Calories(Items.Sum(i => i.CaloriesForAmount))}";
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
    public float CaloriesForAmount { get; set; }
    public string NutritionForAmountText { get; set; } = string.Empty;

    public string TypeLabel => ComponentType.ToString();

    public string RoleLabel => UiTextCatalog.Get(Role switch
    {
        ComponentRole.CarbBase => "Nutrition_ComponentRole_Base",
        ComponentRole.ProteinBase => "Nutrition_ComponentRole_Protein",
        ComponentRole.SideSalad => "Nutrition_ComponentRole_SideSalad",
        ComponentRole.InDishVegetable => "Nutrition_ComponentRole_InDish",
        ComponentRole.SauceOnSide => "Nutrition_ComponentRole_OnSide",
        ComponentRole.SaucePairedProtein => "Nutrition_ComponentRole_WithProtein",
        ComponentRole.InDishSauce => "Nutrition_ComponentRole_MixedIn",
        _ => "Nutrition_ComponentRole_Item"
    });

    public string PortionText => DisplayFormat.GramsShort(Grams);

    public string CaloriesText => DisplayFormat.Calories(CaloriesForAmount);

    public string DetailText => $"{DisplayFormat.GramsShort(Grams)} · {NutritionForAmountText}";
}

public sealed record DayMealPlanItem(
    MealType MealType,
    NutritionMealTarget Target,
    float DailyCaloriesTarget,
    bool IsAlreadyLogged,
    bool IsRefreshing,
    MealSuggestionItem? Suggestion,
    string ErrorMessage = "")
{
    public string MealTypeLabel => UiTextCatalog.Get(MealType switch
    {
        MealType.Breakfast => "Nutrition_MealType_Breakfast",
        MealType.Lunch => "Nutrition_MealType_Lunch",
        MealType.Dinner => "Nutrition_MealType_Dinner",
        MealType.Snack => "Nutrition_MealType_Snack",
        _ => "Nutrition_MealType_Snack"
    });

    public string MealTypeIconPathData => MealType switch
    {
        MealType.Breakfast => "M17 18a5 5 0 0 0 0-10H5a5 5 0 0 0 0 10zM2 21h20M12 3v2",
        MealType.Lunch =>
            "M12 2a8.5 8.5 0 0 1 8.5 8.5c0 3.7-2.4 6.8-5.7 8H9.2c-3.3-1.2-5.7-4.3-5.7-8A8.5 8.5 0 0 1 12 2zM12 2v4M4.93 10h14.14",
        MealType.Dinner => "M3 16h18M5 14a7 7 0 0 1 14 0M12 7V5M2 19h20",
        MealType.Snack => "M12 2a10 10 0 0 0-6.88 2.77A4 4 0 0 1 8 8a4 4 0 0 1-3.54 3.97A10 10 0 1 0 12 2z",
        _ => "M12 2a10 10 0 0 0 0 20 10 10 0 0 0 0-20z"
    };

    public string TargetSummaryText =>
        UiTextCatalog.Format("Nutrition_DayPlan_TargetSummaryFormat",
            DisplayFormat.Calories(Target.Calories), TargetSharePercent);

    public int TargetSharePercent => !float.IsFinite(DailyCaloriesTarget) || DailyCaloriesTarget <= 0f ||
                                     !float.IsFinite(Target.Calories)
        ? 0
        : Math.Clamp((int)MathF.Round(Target.Calories / DailyCaloriesTarget * 100f), 0, 100);

    public string MealTitle => Suggestion?.ConceptName ?? UiTextCatalog.Get(
        IsAlreadyLogged
            ? "Nutrition_DayPlan_AlreadyLogged"
            : Target.Calories < 50f
                ? "Nutrition_DayPlan_DailyTargetReached"
                : "Nutrition_DayPlan_NoSuitableMeal");

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

    public string MacrosText => Suggestion?.MacrosText ?? string.Empty;
    public bool HasSuggestion => Suggestion is not null;
    public bool CanUseSuggestion => !IsAlreadyLogged && Suggestion is not null;
    public bool CanRefresh => !IsAlreadyLogged && !IsRefreshing && Target.Calories >= 50f;
}
