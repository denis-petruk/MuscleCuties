namespace MuscleCuties.Core.Models.UI.Nutrition;

public class FoodSearchResultItem
{
    public int FoodItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public float Calories { get; set; }
    public float Protein { get; set; }
    public float Carbs { get; set; }
    public float Fats { get; set; }
    public float? ServingSize { get; set; }
    public string? ServingSizeUnit { get; set; }
    public string? ServingOptionsJson { get; set; }
    public string SourceSummary { get; set; } = string.Empty;
    public string NutritionSummary { get; set; } = string.Empty;
}
