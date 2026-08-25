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
}
