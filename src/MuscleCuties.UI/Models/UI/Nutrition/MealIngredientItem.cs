
namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>One ingredient in the meal being composed, with the macros for its chosen portion.</summary>
public class MealIngredientItem
{
    /// <summary>Catalog identity of the ingredient.</summary>
    public int FoodItemId { get; set; }
    /// <summary>Ingredient name as shown in the list.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Weight of the ingredient, which is what the server stores.</summary>
    public float Grams { get; set; }
    /// <summary>Quantity in the displayed serving unit, when the person chose a portion instead of grams.</summary>
    public float Amount { get; set; }
    /// <summary>Unit the amount is expressed in.</summary>
    public string ServingLabel { get; set; } = "g";
    /// <summary>Calories per 100 g, used to price the portion.</summary>
    public float Calories { get; set; }
    /// <summary>Protein per 100 g.</summary>
    public float Protein { get; set; }
    /// <summary>Carbohydrate per 100 g.</summary>
    public float Carbs { get; set; }
    /// <summary>Fat per 100 g.</summary>
    public float Fats { get; set; }
    /// <summary>Provenance of the nutrition values, shown beside the ingredient.</summary>
    public string SourceSummary { get; set; } = string.Empty;

    /// <summary>The macros for the currently entered amount.</summary>
    public NutritionMacros MacrosForAmount { get; set; }

    /// <summary>The calories for the current amount.</summary>
    public float CaloriesForAmount => MacrosForAmount.Calories;
    /// <summary>The protein for the current amount.</summary>
    public float ProteinForAmount => MacrosForAmount.Protein;
    /// <summary>The carbs for the current amount.</summary>
    public float CarbsForAmount => MacrosForAmount.Carbs;
    /// <summary>The fats for the current amount.</summary>
    public float FatsForAmount => MacrosForAmount.Fats;

    /// <summary>Portion and its macros on one line, showing both the household amount and the grams when they</summary>
    /// differ.</summary>
    public string AmountSummary =>
        $"{ServingAmountText} · {MacrosForAmount.NutritionText}";

    private string ServingAmountText
    {
        get
        {
            var label = string.IsNullOrWhiteSpace(ServingLabel) ? "g" : ServingLabel;
            var amount = Amount > 0f ? Amount : Grams;
            var amountText = $"{amount:N0} {label}";

            return string.Equals(label, "g", StringComparison.OrdinalIgnoreCase)
                ? amountText
                : $"{amountText} · {DisplayFormat.Grams(Grams)}";
        }
    }
}
