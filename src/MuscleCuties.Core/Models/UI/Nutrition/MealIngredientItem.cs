using MuscleCuties.Core.Services.Nutrition;

namespace MuscleCuties.Core.Models.UI.Nutrition;

public class MealIngredientItem
{
    public int FoodItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public float Grams { get; set; }
    public float Amount { get; set; }
    public string ServingLabel { get; set; } = "g";
    public float Calories { get; set; }
    public float Protein { get; set; }
    public float Carbs { get; set; }
    public float Fats { get; set; }
    public string SourceSummary { get; set; } = string.Empty;

    private MacroNutrients MacrosForAmount =>
        MacroNutrients.FromPer100g(Calories, Protein, Carbs, Fats, Grams);

    public float CaloriesForAmount => MacrosForAmount.Calories;
    public float ProteinForAmount => MacrosForAmount.Protein;
    public float CarbsForAmount => MacrosForAmount.Carbs;
    public float FatsForAmount => MacrosForAmount.Fats;

    public string AmountSummary =>
        $"{ServingAmountText} · {MacrosForAmount.ToNutritionText()}";

    private string ServingAmountText
    {
        get
        {
            var label = string.IsNullOrWhiteSpace(ServingLabel) ? "g" : ServingLabel;
            var amount = Amount > 0f ? Amount : Grams;
            var amountText = $"{amount:N0} {label}";

            return string.Equals(label, "g", StringComparison.OrdinalIgnoreCase)
                ? amountText
                : $"{amountText} · {Grams:N0}g";
        }
    }
}
