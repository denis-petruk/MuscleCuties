// Change notifications and reminder wiring.
// Second half of NutritionViewModel; see its declaration for the full contract.

using System.Collections.Specialized;
using MuscleCuties.UI.Services.Dashboard;

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>Change notifications for the nutrition page's derived state, kept apart so the property sets stay</summary>
/// readable.</summary>
public partial class NutritionViewModel
{
    /// <summary>Raises the food finder properties.</summary>
    private void NotifyFoodFinderProperties()
    {
        OnPropertyChanged(nameof(IsFoodFinderVisible));
        OnPropertyChanged(nameof(IsFoodFinderCollapsed));
        OnPropertyChanged(nameof(ShowLogMealHint));
        OnPropertyChanged(nameof(IsFoodSourceTabsVisible));
        OnPropertyChanged(nameof(IsIngredientSearchVisible));
        OnPropertyChanged(nameof(IsBarcodeSourceVisible));
        OnPropertyChanged(nameof(MealEditorTitle));
        OnPropertyChanged(nameof(IngredientActionText));
    }

    /// <summary>Raises the display properties.</summary>
    private void NotifyDisplayProperties()
    {
        MacroProgressFormatter.NotifyAll(OnPropertyChanged);
        OnPropertyChanged(nameof(RemainingCalories));
        OnPropertyChanged(nameof(RemainingCaloriesText));
        NotifyMealTargetProperties();
        NotifySuggestionProperties();
    }

    /// <summary>Refreshes the macro bars when the calories change.</summary>
    partial void OnConsumedCaloriesChanged(float value)
    {
        if (_isBulkLoading) return;
        MacroProgressFormatter.NotifyCaloriesConsumed(OnPropertyChanged);
        OnPropertyChanged(nameof(RemainingCalories));
        OnPropertyChanged(nameof(RemainingCaloriesText));
    }

    /// <summary>Refreshes the macro bars when the target changes.</summary>
    partial void OnTargetCaloriesChanged(float value)
    {
        if (_isBulkLoading) return;
        MacroProgressFormatter.NotifyCaloriesGoal(OnPropertyChanged);
        OnPropertyChanged(nameof(RemainingCalories));
        OnPropertyChanged(nameof(RemainingCaloriesText));
        NotifyMealTargetProperties();
        NotifySuggestionProperties();
        RefreshMealTargetShares();
    }

    /// <summary>Refreshes the protein bar when the value changes.</summary>
    partial void OnConsumedProteinChanged(float value)
    {
        if (_isBulkLoading) return;
        MacroProgressFormatter.NotifyProtein(OnPropertyChanged);
    }

    /// <summary>Refreshes the carbs bar when the value changes.</summary>
    partial void OnConsumedCarbsChanged(float value)
    {
        if (_isBulkLoading) return;
        MacroProgressFormatter.NotifyCarbs(OnPropertyChanged);
    }

    /// <summary>Refreshes the fats bar when the value changes.</summary>
    partial void OnConsumedFatsChanged(float value)
    {
        if (_isBulkLoading) return;
        MacroProgressFormatter.NotifyFats(OnPropertyChanged);
    }

    /// <summary>Runs the search when the query changes.</summary>
    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(IsFoodSearchIdle));
        SearchFoodCommand.NotifyCanExecuteChanged();
        BrowseMoreFoodsCommand.NotifyCanExecuteChanged();
        ScheduleFoodSearch(value);
    }

    /// <summary>Refreshes the result flags when the results change.</summary>
    partial void OnFoodSearchResultsChanged(ObservableCollection<FoodSearchResultItem> value)
    {
        OnPropertyChanged(nameof(HasFoodSearchResults));
        OnPropertyChanged(nameof(ShowBrowseMoreFoods));
        BrowseMoreFoodsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Refreshes the command states when busy changes.</summary>
    partial void OnIsBusyChanged(bool value)
    {
        SearchFoodCommand.NotifyCanExecuteChanged();
        BrowseMoreFoodsCommand.NotifyCanExecuteChanged();
        AddIngredientCommand.NotifyCanExecuteChanged();
        LogMealCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSaveMeal));
        OnPropertyChanged(nameof(ShowLogMealHint));
        OnPropertyChanged(nameof(LogMealHint));
        CreateCustomFoodCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Refreshes the browse state when loading more changes.</summary>
    partial void OnIsBrowsingMoreFoodsChanged(bool value)
    {
        OnPropertyChanged(nameof(BrowseMoreFoodsButtonText));
        SearchFoodCommand.NotifyCanExecuteChanged();
        BrowseMoreFoodsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Refreshes the browse button when more results arrive.</summary>
    partial void OnHasMoreFoodResultsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowBrowseMoreFoods));
        BrowseMoreFoodsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Raises the meal flags when the meals change.</summary>
    partial void OnMealsChanged(ObservableCollection<MealItem> value)
    {
        OnPropertyChanged(nameof(HasMeals));
        OnPropertyChanged(nameof(HasNoMeals));
    }

    /// <summary>Follows the ingredient collection when it is replaced.</summary>
    partial void OnMealIngredientsChanged(
        ObservableCollection<MealIngredientItem>? oldValue,
        ObservableCollection<MealIngredientItem> newValue)
    {
        if (oldValue is not null)
            oldValue.CollectionChanged -= OnMealIngredientsCollectionChanged;
        newValue.CollectionChanged += OnMealIngredientsCollectionChanged;
        NotifyMealIngredientProperties();
    }

    /// <summary>Raises the ingredient totals when the collection changes.</summary>
    private void OnMealIngredientsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        NotifyMealIngredientProperties();
    }

    /// <summary>Shows the amount editor when a food is selected.</summary>
    partial void OnSelectedFoodResultChanged(FoodSearchResultItem? value)
    {
        UpdateServingOptions(value);
        NotifyFoodFinderProperties();
        OnPropertyChanged(nameof(IsSelectedFoodEditorVisible));
        OnPropertyChanged(nameof(SelectedFoodText));
        OnPropertyChanged(nameof(SelectedFoodSourceText));
        OnPropertyChanged(nameof(HasSelectedFoodSource));
        OnPropertyChanged(nameof(SelectedFoodAmountText));
        AddIngredientCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Recalculates the preview when the grams change.</summary>
    partial void OnFoodGramsChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedFoodAmountText));
        AddIngredientCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Recalculates the preview when the serving changes.</summary>
    partial void OnSelectedServingOptionChanged(FoodServingOptionItem? oldValue, FoodServingOptionItem? newValue)
    {
        if (!_isApplyingServingDefaults)
        {
            if (oldValue is not null && newValue is not null &&
                oldValue.Grams > 0f && newValue.Grams > 0f &&
                TryParseAmount(FoodGrams, out var currentAmount))
            {
                var grams = currentAmount * oldValue.Grams;
                var convertedAmount = grams / newValue.Grams;
                FoodGrams = FormatConvertedAmount(convertedAmount);
            }
            else
            {
                ApplyDefaultServingAmount(SelectedFoodResult, newValue);
            }
        }

        OnPropertyChanged(nameof(SelectedFoodAmountText));
        AddIngredientCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Refreshes the panel flags when the custom panel opens.</summary>
    partial void OnIsCustomFoodPanelVisibleChanged(bool value)
    {
        NotifyFoodFinderProperties();
        OnPropertyChanged(nameof(CustomFoodToggleText));
    }

    /// <summary>Refreshes the finder flags when it expands.</summary>
    partial void OnIsFoodFinderExpandedChanged(bool value)
    {
        NotifyFoodFinderProperties();
    }

    /// <summary>Refreshes the message flag when the text changes.</summary>
    partial void OnAddFoodMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasAddFoodMessage));
    }
}
