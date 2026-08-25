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
}
