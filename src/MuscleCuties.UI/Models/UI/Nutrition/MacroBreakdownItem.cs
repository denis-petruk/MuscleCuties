using Microsoft.Maui.Graphics;


namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>One macro's slice of a meal: grams, calories, its share of the total, and its colour.</summary>
public class MacroBreakdownItem
{
    /// <summary>Macro name, which also selects its glyph.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Grams of the macro in the meal.</summary>
    public float Grams { get; set; }
    /// <summary>Calories the macro contributes, which is what the bar is scaled by.</summary>
    public float Calories { get; set; }
    /// <summary>Share of the meal's calories, as a fraction.</summary>
    public float Progress { get; set; }
    /// <summary>Colour that identifies the macro in the breakdown chart.</summary>
    public Color Color { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");

    /// <summary>Gram amount with its short unit.</summary>
    public string AmountText => DisplayFormat.GramsShort(Grams);
    /// <summary>Calorie contribution, formatted with its unit.</summary>
    public string CaloriesText => DisplayFormat.Calories(Calories);
    /// <summary>Share of the meal as a whole percentage.</summary>
    public string ProgressText => $"{Progress * 100f:N0}%";

    /// <summary>Glyph for the macro, with a generic food marker for anything unexpected.</summary>
    public string IconGlyph => Name switch
    {
        "Protein" => "Dumbbell24",
        "Carbs" => "FoodGrains24",
        "Fats" => "Drop24",
        _ => "Food24"
    };

    /// <summary>Outline path for the macro, used where the glyph font is not.</summary>
    public string IconPathData => Name switch
    {
        "Protein" => "M6 10h12M4 8v8M2 10v4M20 8v8M22 10v4",
        "Carbs" =>
            "M12 21V8M12 11c-3 0-5-2-5-5 3 0 5 2 5 5ZM12 15c3 0 5-2 5-5-3 0-5 2-5 5ZM12 19c-3 0-5-2-5-5 3 0 5 2 5 5",
        "Fats" => "M12 2c3 4 6 7 6 12a6 6 0 0 1-12 0c0-5 3-8 6-12Z",
        _ => "M3 2v7c0 1.1.9 2 2 2h2c1.1 0 2-.9 2-2V2M6 2v20M15 2v8h4M19 2v20"
    };
}
