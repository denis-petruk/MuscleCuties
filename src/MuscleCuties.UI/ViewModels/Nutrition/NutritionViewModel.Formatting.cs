using System.Collections.ObjectModel;
using System.Globalization;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private static string BuildNutritionForGrams(FoodSearchResultItem food, float grams)
    {
        return MacroNutrients
            .FromPer100g(food.Calories, food.Protein, food.Carbs, food.Fats, grams)
            .ToNutritionText();
    }

    private static FoodSearchResultItem CreateFoodSearchResultItem(FoodCatalogItemDto food)
    {
        return new FoodSearchResultItem
        {
            FoodItemId = food.Id,
            Name = food.Name,
            Calories = food.Calories ?? 0f,
            Protein = food.Protein ?? 0f,
            Carbs = food.Carbs ?? 0f,
            Fats = food.Fats ?? 0f,
            ServingSize = food.ServingSize,
            ServingSizeUnit = food.ServingSizeUnit,
            ServingOptionsJson = food.ServingOptionsJson,
            SourceSummary = food.SourceSummary,
            NutritionSummary = food.NutritionSummary
        };
    }

    private static FoodSearchResultItem CreateFoodSearchResultItem(CustomFoodResultDto food)
    {
        return new FoodSearchResultItem
        {
            FoodItemId = food.FoodItemId,
            Name = food.Name,
            Calories = food.Calories,
            Protein = food.Protein,
            Carbs = food.Carbs,
            Fats = food.Fats,
            ServingSize = food.ServingSize,
            ServingSizeUnit = food.ServingSizeUnit,
            ServingOptionsJson = food.ServingOptionsJson,
            SourceSummary = food.SourceSummary,
            NutritionSummary = food.NutritionSummary
        };
    }

    private static FoodSearchResultItem CreateFoodSearchResultItem(
        int foodId, string name, float calories, float protein, float carbs, float fats, string sourceSummary)
    {
        return new FoodSearchResultItem
        {
            FoodItemId = foodId,
            Name = name,
            Calories = calories,
            Protein = protein,
            Carbs = carbs,
            Fats = fats,
            SourceSummary = sourceSummary
        };
    }

    private void SelectFoodResult(FoodSearchResultItem? food)
    {
        if (food is null || IsBusy)
            return;

        SelectedFoodResult = food;
        UpdateServingOptions(food);
        NotifyFoodFinderProperties();
        OnPropertyChanged(nameof(SelectedFoodText));
        OnPropertyChanged(nameof(SelectedFoodSourceText));
        OnPropertyChanged(nameof(HasSelectedFoodSource));
        OnPropertyChanged(nameof(SelectedFoodAmountText));
        AddIngredientCommand.NotifyCanExecuteChanged();
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

        var options = FoodServingOptionHelper.BuildServingOptionItems(food);
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
            FoodServingOptionHelper.TryConvertToGrams(food.ServingSize.Value, food.ServingSizeUnit,
                out var servingGrams))
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

    private static bool IsSingleUnitServing(FoodServingOptionItem option)
    {
        return string.Equals(option.Label, "serving", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(option.Label, "container", StringComparison.OrdinalIgnoreCase) ||
               (!string.Equals(option.Source, "Standard", StringComparison.OrdinalIgnoreCase) &&
                !IsStandardOptionLabel(option.Label));
    }

    private static bool IsStandardAmountUnit(FoodServingOptionItem option)
    {
        return option.Grams > 0f &&
               string.Equals(option.Source, "Standard", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStandardOptionLabel(string label)
    {
        return string.Equals(label, "g", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(label, "oz", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(label, "lb", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(label, "ml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(label, "cup", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(label, "fl oz", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(label, "tbsp", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(label, "tsp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCalories(float calories)
    {
        return calories > 0f;
    }

    private static bool TryParseAmount(string value, out float amount)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out amount) &&
            !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out amount))
            amount = 0f;

        return amount > 0f;
    }

    private static string FormatServingAmount(float amount, string label)
    {
        return $"{FormatAmountInput(amount)} {label}";
    }

    private static string FormatAmountInput(float amount)
    {
        var rounded = MathF.Round(amount);
        return MathF.Abs(amount - rounded) < 0.01f
            ? rounded.ToString("N0", CultureInfo.CurrentCulture)
            : amount.ToString("0.##", CultureInfo.CurrentCulture);
    }

    private static MealItem MapMealDtoToItem(MealSummaryDto dto)
    {
        return new MealItem
        {
            HistoryId = dto.HistoryId,
            LoggedMealId = dto.LoggedMealId,
            Time = dto.Time,
            MealType = dto.MealType,
            Name = dto.Name,
            CaloriesText = dto.CaloriesText,
            MacrosText = dto.MacrosText,
            FiberText = dto.FiberText,
            NutrientSummaryText = dto.NutrientSummaryText,
            Macros = new MacroNutrients(dto.Calories, dto.Protein, dto.Carbs, dto.Fats),
            MacroItems = new ObservableCollection<MacroBreakdownItem>(dto.MacroItems.Select(m => new MacroBreakdownItem
            {
                Name = m.Name,
                Grams = m.Grams,
                Calories = m.Calories,
                Progress = m.Progress
            })),
            Micronutrients =
                new ObservableCollection<DailyMicronutrientItem>(dto.Micronutrients.Select(MapMicroDtoToItem))
        };
    }

    private static DailyMicronutrientItem MapMicroDtoToItem(DailyMicronutrientDto dto)
    {
        return new DailyMicronutrientItem
        {
            Group = dto.Group,
            Name = dto.Name,
            Unit = dto.Unit,
            Amount = dto.Amount,
            Goal = dto.Goal
        };
    }
}
