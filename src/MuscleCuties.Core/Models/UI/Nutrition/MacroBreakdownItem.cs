using Microsoft.Maui.Graphics;

namespace MuscleCuties.Core.Models.UI.Nutrition;

public class MacroBreakdownItem
{
    public string Name { get; set; } = string.Empty;
    public float Grams { get; set; }
    public float Calories { get; set; }
    public float Progress { get; set; }
    public Color Color { get; set; } = Colors.Transparent;

    public string AmountText => $"{Grams:N1}g";
    public string CaloriesText => $"{Calories:N0} kcal";
    public string ProgressText => $"{Progress * 100f:N0}%";

    public string IconGlyph => Name switch
    {
        "Protein" => "Dumbbell24",
        "Carbs" => "FoodGrains24",
        "Fats" => "Drop24",
        _ => "Food24"
    };

    public string IconPathData => Name switch
    {
        "Protein" => "M6 10h12M4 8v8M2 10v4M20 8v8M22 10v4",
        "Carbs" => "M12 21V8M12 11c-3 0-5-2-5-5 3 0 5 2 5 5ZM12 15c3 0 5-2 5-5-3 0-5 2-5 5ZM12 19c-3 0-5-2-5-5 3 0 5 2 5 5",
        "Fats" => "M12 2c3 4 6 7 6 12a6 6 0 0 1-12 0c0-5 3-8 6-12Z",
        _ => "M3 2v7c0 1.1.9 2 2 2h2c1.1 0 2-.9 2-2V2M6 2v20M15 2v8h4M19 2v20"
    };
}
