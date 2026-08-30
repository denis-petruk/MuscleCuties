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
        "Vitamin C" => "FoodApple24",
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
}
