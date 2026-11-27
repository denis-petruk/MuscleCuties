// Display strings, macro formatting, and portion maths.
// Second half of NutritionViewModel; see its declaration for the full contract.

using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>Formatting and mapping for the nutrition page: serving amounts, converted weights, and the contract rows</summary>
/// turned into list items.</summary>
public partial class NutritionViewModel
{
    /// <summary>The nutrition line for a specific weight of a food, scaled from its per-serving values.</summary>
    private static string BuildNutritionForGrams(FoodSearchResultItem food, float grams)
    {
        return NutritionPresentationMapper.NutritionTextForGrams(
            food.Calories, food.Protein, food.Carbs, food.Fats, grams);
    }

    /// <summary>A catalog food as a search row, carrying its serving options so the amount picker has something to offer.</summary>
    private static FoodSearchResultItem CreateFoodSearchResultItem(FoodCatalogItem food)
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

    /// <summary>A user's own food as a search row.</summary>
    private static FoodSearchResultItem CreateFoodSearchResultItem(CustomFoodResult food)
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

    /// <summary>The search result row for a catalog food.</summary>
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

    /// <summary>Chooses a search result, offers its serving options and fills in a default amount; ignored while the page is</summary>
    /// busy.</summary>
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

    /// <summary>Replaces the serving choices for the chosen food, clearing them when no food is chosen.</summary>
    private void UpdateServingOptions(FoodSearchResultItem? food)
    {
        if (food is null)
        {
            ServingOptions = [];
            SelectedServingOption = null;
            FoodGrams = string.Empty;
            return;
        }

        var options = FoodServingOptionHelper.BuildServingOptionItems(
            food, _foodPortionCatalog, _recipePortionUseCase);
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

    /// <summary>Fills the amount from the chosen serving without treating it as the user's own edit.</summary>
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

    /// <summary>The amount a serving implies: one unit for countable servings, the option's weight for weighed ones, and a</summary>
    /// hundred for a plain hundred-gram measure.</summary>
    private string GetDefaultServingAmountText(FoodSearchResultItem? food, FoodServingOptionItem? option)
    {
        if (food is null || option is null)
            return string.Empty;

        if (IsSingleUnitServing(option))
            return "1";

        if (IsStandardAmountUnit(option) &&
            food.ServingSize is > 0f &&
            _recipePortionUseCase.ConvertMassToGrams(food.ServingSize.Value, food.ServingSizeUnit) is
            { Succeeded: true, Grams: { } servingGrams })
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

    /// <summary>True for a serving counted in pieces rather than weighed — a serving, a container, or any non-standard</summary>
    /// portion that is not a known measure.</summary>
    private static bool IsSingleUnitServing(FoodServingOptionItem option)
    {
        return string.Equals(option.Label, "serving", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(option.Label, "container", StringComparison.OrdinalIgnoreCase) ||
               (!string.Equals(option.Source, "Standard", StringComparison.OrdinalIgnoreCase) &&
                !IsStandardOptionLabel(option.Label));
    }

    /// <summary>A standard option that also carries a weight, so the amount can be converted rather than guessed.</summary>
    private static bool IsStandardAmountUnit(FoodServingOptionItem option)
    {
        return option.Grams > 0f &&
               string.Equals(option.Source, "Standard", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The measure labels the app treats as amounts it understands.</summary>
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

    /// <summary>Whether a value carries calories worth showing.</summary>
    private static bool HasCalories(float calories)
    {
        return calories > 0f;
    }

    /// <summary>Reads a typed amount in the user's culture, refusing text that is not a positive number.</summary>
    private static bool TryParseAmount(string value, out float amount)
    {
        amount = 0f;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out amount) &&
            float.IsFinite(amount) && amount > 0f)
            return true;

        if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out amount) &&
            float.IsFinite(amount) && amount > 0f)
            return true;

        var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var alternateSeparator = decimalSeparator == "." ? "," : ".";
        if (!trimmed.Contains(decimalSeparator, StringComparison.Ordinal) &&
            trimmed.Contains(alternateSeparator, StringComparison.Ordinal))
        {
            var normalized = trimmed.Replace(alternateSeparator, decimalSeparator, StringComparison.Ordinal);
            if (float.TryParse(normalized, NumberStyles.Float, CultureInfo.CurrentCulture, out amount) &&
                float.IsFinite(amount) && amount > 0f)
                return true;
        }

        amount = 0f;
        return false;
    }

    /// <summary>A converted weight with four decimals, dropping the decimals when the value is effectively whole.</summary>
    private static string FormatConvertedAmount(float amount)
    {
        var rounded = MathF.Round(amount);
        return MathF.Abs(amount - rounded) < 0.0001f
            ? DisplayFormat.WholeNumber(rounded)
            : DisplayFormat.Decimal4(amount);
    }

    /// <summary>An amount with its unit, as the serving chips read.</summary>
    private static string FormatServingAmount(float amount, string label)
    {
        return $"{FormatAmountInput(amount)} {label}";
    }

    /// <summary>A weight for the amount field, with two decimals only when the value is not whole.</summary>
    private static string FormatAmountInput(float amount)
    {
        var rounded = MathF.Round(amount);
        return MathF.Abs(amount - rounded) < 0.01f
            ? DisplayFormat.WholeNumber(rounded)
            : DisplayFormat.Decimal2(amount);
    }

    /// <summary>A logged meal as a list item, keeping both its history and log identities.</summary>
    private static MealItem MapMealDtoToItem(NutritionMealSummary dto)
    {
        return new MealItem
        {
            HistoryId = dto.HistoryId,
            LoggedMealId = dto.LoggedMealId,
            Time = dto.Time,
            MealType = dto.MealType,
            Name = dto.Name,
            IngredientsText = string.Equals(dto.IngredientSummary, dto.Name, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : dto.IngredientSummary,
            CaloriesText = dto.CaloriesText,
            MacrosText = dto.MacrosText,
            FiberText = dto.FiberText,
            NutrientSummaryText = dto.NutrientSummaryText,
            Macros = NutritionPresentationMapper.FromTotals(dto.Calories, dto.Protein, dto.Carbs, dto.Fats),
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

    private const float PercentScale = 100f;

    /// <summary>Recomputes each meal's share of the daily target after the target or the meals change.</summary>
    private void RefreshMealTargetShares()
    {
        foreach (var meal in Meals)
        {
            meal.TargetShareText = TargetCalories > 0
                ? MuscleCuties.UI.Services.Localization.UiTextCatalog.Format(
                    "Nutrition_MealCard_TargetShareFormat",
                    Math.Round(meal.Macros.Calories / TargetCalories * PercentScale))
                : string.Empty;
        }
    }

    /// <summary>A micronutrient row with its amounts already formatted for display.</summary>
    private static DailyMicronutrientItem MapMicroDtoToItem(DailyMicronutrient dto)
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
