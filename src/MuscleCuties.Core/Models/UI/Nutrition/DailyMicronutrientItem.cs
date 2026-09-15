namespace MuscleCuties.Core.Models.UI.Nutrition;

public class DailyMicronutrientItem
{
    public string Group { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public float Amount { get; set; }
    public float Goal { get; set; }

    public float Progress => Goal <= 0f ? 0f : Math.Clamp(Amount / Goal, 0f, 1f);
    public bool IsGoalHit => Goal > 0f && Amount >= Goal;
    public string AmountText => $"{Amount:N1}{Unit}";
    public string GoalText => $"/ {Goal:N1}{Unit}";
    public string ProgressText => Goal <= 0f ? "No target" : $"{Progress * 100f:N0}%";

    public string IconGlyph => Name switch
    {
        "Fiber" => "LeafThree24",
        "Vitamin A" => "FoodCarrot24",
        "Vitamin C" => "SparkleCircle24",
        "Vitamin D" => "Fire24",
        "Vitamin B6" or "Vitamin B12" or "Folate" => "Molecule24",
        "Calcium" => "ShieldCheckmark24",
        "Iron" => "Drop24",
        "Magnesium" => "BatteryCharge24",
        "Zinc" => "SparkleCircle24",
        "Potassium" => "HeartCircle24",
        _ when string.Equals(Group, "Vitamins", StringComparison.OrdinalIgnoreCase) => "Molecule24",
        _ when string.Equals(Group, "Minerals", StringComparison.OrdinalIgnoreCase) => "Beaker24",
        _ => "Food24"
    };

    public string IconPathData => Name switch
    {
        "Fiber" => "M12 21V9M12 13c-4 0-7-3-7-7 4 0 7 3 7 7ZM12 17c4 0 7-3 7-7-4 0-7 3-7 7",
        "Vitamin A" => "M15 8 6 21l-3-3L15 8ZM14 7c1-3 3-4 6-4 0 3-1 5-4 6M16 8c3-1 5 0 6 2-3 1-5 1-7-1",
        "Vitamin C" => "M12 3v3M12 18v3M3 12h3M18 12h3M5.6 5.6l2.1 2.1M16.3 16.3l2.1 2.1M18.4 5.6l-2.1 2.1M7.7 16.3l-2.1 2.1M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z",
        "Vitamin D" => "M13 2s1 4-2 7c-2 2-3 4-3 6a5 5 0 0 0 10 0c0-3-2-5-5-7 0 2-1 3-2 4",
        "Vitamin B6" or "Vitamin B12" or "Folate" => "M7 7a3 3 0 1 0 0-6 3 3 0 0 0 0 6ZM17 13a3 3 0 1 0 0-6 3 3 0 0 0 0 6ZM7 23a3 3 0 1 0 0-6 3 3 0 0 0 0 6ZM9 5l6 4M9 19l6-4M7 7v10",
        "Calcium" => "M12 3 20 6v6c0 5-3.4 8-8 10-4.6-2-8-5-8-10V6l8-3ZM8 12l3 3 5-6",
        "Iron" => "M12 2c3 4 6 7 6 12a6 6 0 0 1-12 0c0-5 3-8 6-12Z",
        "Magnesium" => "M13 2 5 14h6l-1 8 8-12h-6l-2-10Z",
        "Zinc" => "M12 3v3M12 18v3M3 12h3M18 12h3M6 6l2 2M16 16l2 2M18 6l-2 2M8 16l-2 2",
        "Potassium" => "M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1-1.1a5.5 5.5 0 0 0-7.8 7.8L12 21l8.8-8.6a5.5 5.5 0 0 0 0-7.8Z",
        _ => "M4 19h16M6 16h12l-1-9H7l-1 9ZM9 7V4h6v3"
    };
}
