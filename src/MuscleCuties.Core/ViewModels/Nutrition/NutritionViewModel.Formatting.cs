using System.Collections.ObjectModel;
using System.Globalization;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.UI.Nutrition;
using MuscleCuties.Core.Services.Nutrition;

namespace MuscleCuties.Core.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private static string BuildNutritionSummary(FoodItem food)
    {
        if (!HasCalories(food.Calories))
            return "Nutrition values unavailable";

        var option = FoodServingOptions.BuildOptionsForFood(food).FirstOrDefault();
        if (option is null)
            return BuildNutritionForGrams(food, 100f);

        var previewAmount = GetServingPreviewAmount(option.Label);
        return $"{FormatServingAmount(previewAmount, option.Label)}: {BuildNutritionForGrams(food, option.Grams * previewAmount)}";
    }

    private static string BuildServingNutritionPreview(FoodSearchResultItem food, FoodServingOptionItem option)
    {
        var previewAmount = GetServingPreviewAmount(option.Label);
        return $"{FormatServingAmount(previewAmount, option.Label)}: {BuildNutritionForGrams(food, option.Grams * previewAmount)}";
    }

    private static string BuildNutritionForGrams(FoodSearchResultItem food, float grams)
        => MacroNutrients
            .FromPer100g(food.Calories, food.Protein, food.Carbs, food.Fats, grams)
            .ToNutritionText();

    private static string BuildNutritionForGrams(FoodItem food, float grams)
        => MacroNutrients.FromFood(food, grams).ToNutritionText();

    private static FoodSearchResultItem CreateFoodSearchResultItem(FoodItem food) =>
        new()
        {
            FoodItemId = food.Id,
            Name = food.Name,
            Calories = food.Calories,
            Protein = food.Protein,
            Carbs = food.Carbs,
            Fats = food.Fats,
            ServingSize = food.ServingSize,
            ServingSizeUnit = food.ServingSizeUnit,
            ServingOptionsJson = food.ServingOptionsJson,
            SourceSummary = BuildSourceSummary(food),
            NutritionSummary = BuildNutritionSummary(food)
        };

    private static List<FoodServingOptionItem> BuildServingOptionItems(FoodSearchResultItem food)
    {
        var foodItem = new FoodItem
        {
            Name = food.Name,
            Calories = food.Calories,
            Protein = food.Protein,
            Carbs = food.Carbs,
            Fats = food.Fats,
            ServingSize = food.ServingSize,
            ServingSizeUnit = food.ServingSizeUnit,
            ServingOptionsJson = food.ServingOptionsJson
        };

        return FoodServingOptions
            .BuildOptionsForFood(foodItem)
            .Select(option => new FoodServingOptionItem
            {
                Label = option.Label,
                Unit = option.Unit,
                Grams = option.Grams,
                Source = option.Source
            })
            .ToList();
    }

    private void UpdateServingOptions(FoodSearchResultItem? food)
    {
        if (food is null)
        {
            ServingOptions = [];
            SelectedServingOption = null;
            FoodGrams = string.Empty;
            return;
        }

        var options = BuildServingOptionItems(food);
        _isApplyingServingDefaults = true;
        try
        {
            ServingOptions = new ObservableCollection<FoodServingOptionItem>(options);
            SelectedServingOption = ServingOptions.FirstOrDefault();
            FoodGrams = GetDefaultServingAmountText(food, SelectedServingOption);
        }
        finally
        {
            _isApplyingServingDefaults = false;
        }
    }

    private void ApplyDefaultServingAmount(FoodSearchResultItem? food, FoodServingOptionItem? option)
    {
        _isApplyingServingDefaults = true;
        try
        {
            FoodGrams = GetDefaultServingAmountText(food, option);
        }
        finally
        {
            _isApplyingServingDefaults = false;
        }
    }

    private static string GetDefaultServingAmountText(FoodSearchResultItem? food, FoodServingOptionItem? option)
    {
        if (food is null || option is null)
            return string.Empty;

        if (IsSingleUnitServing(option))
            return "1";

        if (IsStandardAmountUnit(option) &&
            food.ServingSize is > 0f &&
            FoodServingOptions.TryConvertToGrams(food.ServingSize.Value, food.ServingSizeUnit, out var servingGrams))
        {
            var amount = servingGrams / option.Grams;
            if (amount is > 0f and < 1000f)
                return FormatAmountInput(amount);
        }

        return string.Equals(option.Label, "g", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(option.Label, "ml", StringComparison.OrdinalIgnoreCase)
            ? "100"
            : "1";
    }

    private static bool IsSingleUnitServing(FoodServingOptionItem option) =>
        string.Equals(option.Label, "serving", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(option.Label, "container", StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(option.Source, "Standard", StringComparison.OrdinalIgnoreCase) &&
        !IsStandardOptionLabel(option.Label);

    private static bool IsStandardAmountUnit(FoodServingOptionItem option) =>
        option.Grams > 0f &&
        string.Equals(option.Source, "Standard", StringComparison.OrdinalIgnoreCase);

    private static bool IsStandardOptionLabel(string label) =>
        string.Equals(label, "g", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(label, "oz", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(label, "lb", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(label, "ml", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(label, "cup", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(label, "fl oz", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(label, "tbsp", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(label, "tsp", StringComparison.OrdinalIgnoreCase);

    private static string BuildSourceSummary(FoodItem food)
    {
        var source = string.IsNullOrWhiteSpace(food.DataType)
            ? food.FdcId.HasValue ? "USDA" : "Local"
            : food.DataType;
        var parts = new List<string> { source };

        var brand = FirstPresent(food.BrandName, food.BrandOwner);
        if (!string.IsNullOrWhiteSpace(brand))
            parts.Add(brand);

        if (!string.IsNullOrWhiteSpace(food.GtinUpc))
            parts.Add($"UPC {food.GtinUpc}");

        if (food.ServingSize is > 0f)
        {
            var unit = string.IsNullOrWhiteSpace(food.ServingSizeUnit)
                ? "serving"
                : food.ServingSizeUnit;
            parts.Add($"Serving {food.ServingSize.Value:N0} {unit}");
        }

        return string.Join(" · ", parts);
    }

    private static string? FirstPresent(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static bool HasCalories(float calories) =>
        calories > 0f;

    private static bool TryParseAmount(string value, out float amount)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out amount) &&
            !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out amount))
        {
            amount = 0f;
        }

        return amount > 0f;
    }

    private static string FormatServingAmount(float amount, string label) =>
        $"{FormatAmountInput(amount)} {label}";

    private static string FormatAmountInput(float amount)
    {
        var rounded = MathF.Round(amount);
        return MathF.Abs(amount - rounded) < 0.01f
            ? rounded.ToString("N0", CultureInfo.CurrentCulture)
            : amount.ToString("0.##", CultureInfo.CurrentCulture);
    }

    private static float GetServingPreviewAmount(string label) =>
        string.Equals(label, "g", StringComparison.OrdinalIgnoreCase) ? 100f : 1f;
}
