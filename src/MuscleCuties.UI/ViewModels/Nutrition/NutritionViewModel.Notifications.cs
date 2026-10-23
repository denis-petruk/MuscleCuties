using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Dashboard;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private void NotifyFoodFinderProperties()
    {
        OnPropertyChanged(nameof(IsFoodFinderVisible));
        OnPropertyChanged(nameof(IsFoodFinderCollapsed));
        OnPropertyChanged(nameof(IsIngredientSearchVisible));
        OnPropertyChanged(nameof(MealEditorTitle));
        OnPropertyChanged(nameof(IngredientActionText));
    }

    private void NotifyDisplayProperties()
    {
        MacroProgressFormatter.NotifyAll(OnPropertyChanged);
        NotifyMealTargetProperties();
        NotifySuggestionProperties();
    }

    partial void OnConsumedCaloriesChanged(float value)
    {
        if (_isBulkLoading) return;
        MacroProgressFormatter.NotifyCaloriesConsumed(OnPropertyChanged);
    }

    partial void OnTargetCaloriesChanged(float value)
    {
        if (_isBulkLoading) return;
        MacroProgressFormatter.NotifyCaloriesGoal(OnPropertyChanged);
        NotifyMealTargetProperties();
        NotifySuggestionProperties();
    }

    partial void OnConsumedProteinChanged(float value)
    {
        if (_isBulkLoading) return;
        MacroProgressFormatter.NotifyProtein(OnPropertyChanged);
    }

    partial void OnConsumedCarbsChanged(float value)
    {
        if (_isBulkLoading) return;
        MacroProgressFormatter.NotifyCarbs(OnPropertyChanged);
    }

    partial void OnConsumedFatsChanged(float value)
    {
        if (_isBulkLoading) return;
        MacroProgressFormatter.NotifyFats(OnPropertyChanged);
    }

    partial void OnSearchQueryChanged(string value)
    {
        SearchFoodCommand.NotifyCanExecuteChanged();
        BrowseMoreFoodsCommand.NotifyCanExecuteChanged();
    }

    partial void OnFoodSearchResultsChanged(ObservableCollection<FoodSearchResultItem> value)
    {
        OnPropertyChanged(nameof(HasFoodSearchResults));
        OnPropertyChanged(nameof(ShowBrowseMoreFoods));
        BrowseMoreFoodsCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        SearchFoodCommand.NotifyCanExecuteChanged();
        BrowseMoreFoodsCommand.NotifyCanExecuteChanged();
        AddIngredientCommand.NotifyCanExecuteChanged();
        LogMealCommand.NotifyCanExecuteChanged();
        CreateCustomFoodCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBrowsingMoreFoodsChanged(bool value)
    {
        OnPropertyChanged(nameof(BrowseMoreFoodsButtonText));
        SearchFoodCommand.NotifyCanExecuteChanged();
        BrowseMoreFoodsCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasMoreFoodResultsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowBrowseMoreFoods));
        BrowseMoreFoodsCommand.NotifyCanExecuteChanged();
    }

    partial void OnMealsChanged(ObservableCollection<MealItem> value)
    {
        OnPropertyChanged(nameof(HasMeals));
        OnPropertyChanged(nameof(HasNoMeals));
    }

    partial void OnMealIngredientsChanged(
        ObservableCollection<MealIngredientItem>? oldValue,
        ObservableCollection<MealIngredientItem> newValue)
    {
        if (oldValue is not null)
            oldValue.CollectionChanged -= OnMealIngredientsCollectionChanged;
        newValue.CollectionChanged += OnMealIngredientsCollectionChanged;
        NotifyMealIngredientProperties();
    }

    private void OnMealIngredientsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        NotifyMealIngredientProperties();
    }

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

    partial void OnFoodGramsChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedFoodAmountText));
        AddIngredientCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedServingOptionChanged(FoodServingOptionItem? value)
    {
        if (!_isApplyingServingDefaults)
            ApplyDefaultServingAmount(SelectedFoodResult, value);

        OnPropertyChanged(nameof(SelectedFoodAmountText));
        AddIngredientCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCustomFoodPanelVisibleChanged(bool value)
    {
        NotifyFoodFinderProperties();
        OnPropertyChanged(nameof(CustomFoodToggleText));
    }

    partial void OnIsFoodFinderExpandedChanged(bool value)
    {
        NotifyFoodFinderProperties();
    }

    partial void OnAddFoodMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasAddFoodMessage));
    }
}
