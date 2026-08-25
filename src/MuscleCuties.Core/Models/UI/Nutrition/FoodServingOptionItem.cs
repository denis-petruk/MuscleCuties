namespace MuscleCuties.Core.Models.UI.Nutrition;

public class FoodServingOptionItem
{
    public string Label { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public float Grams { get; set; }
    public string Source { get; set; } = string.Empty;

    public string DisplayText => Label;

    public override string ToString() => DisplayText;
}
