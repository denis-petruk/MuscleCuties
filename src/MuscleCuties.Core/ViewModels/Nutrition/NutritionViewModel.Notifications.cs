using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MuscleCuties.Core.Models.UI.Nutrition;

namespace MuscleCuties.Core.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private void NotifyFoodFinderProperties()
    {
        OnPropertyChanged(nameof(IsFoodFinderVisible));
        OnPropertyChanged(nameof(IsFoodFinderCollapsed));
    }

    private void NotifyDisplayProperties()
    {
        OnPropertyChanged(nameof(CaloriesProgress));
        OnPropertyChanged(nameof(CaloriesConsumed));
        OnPropertyChanged(nameof(CaloriesGoal));
        OnPropertyChanged(nameof(ProteinText));
        OnPropertyChanged(nameof(ProteinProgress));
        OnPropertyChanged(nameof(CarbsText));
        OnPropertyChanged(nameof(CarbsProgress));
        OnPropertyChanged(nameof(FatsText));
        OnPropertyChanged(nameof(FatsProgress));
    }

    partial void OnConsumedCaloriesChanged(float value)
    {
        OnPropertyChanged(nameof(CaloriesProgress));
        OnPropertyChanged(nameof(CaloriesConsumed));
    }

    partial void OnTargetCaloriesChanged(float value)
    {
        OnPropertyChanged(nameof(CaloriesProgress));
        OnPropertyChanged(nameof(CaloriesGoal));
    }

    partial void OnConsumedProteinChanged(float value)
    {
        OnPropertyChanged(nameof(ProteinText));
        OnPropertyChanged(nameof(ProteinProgress));
    }

    partial void OnConsumedCarbsChanged(float value)
    {
        OnPropertyChanged(nameof(CarbsText));
        OnPropertyChanged(nameof(CarbsProgress));
    }

    partial void OnConsumedFatsChanged(float value)
    {
        OnPropertyChanged(nameof(FatsText));
        OnPropertyChanged(nameof(FatsProgress));
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
