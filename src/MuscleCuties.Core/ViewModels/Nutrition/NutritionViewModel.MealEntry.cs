using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    [ObservableProperty] private bool _isMealEntryModalVisible;
    [ObservableProperty] private MealType _entryMealType;

    public bool IsEntryBreakfastSelected => EntryMealType == MealType.Breakfast;
    public bool IsEntryLunchSelected => EntryMealType == MealType.Lunch;
    public bool IsEntryDinnerSelected => EntryMealType == MealType.Dinner;
    public bool IsEntrySnackSelected => EntryMealType == MealType.Snack;
    public bool HasEntryMealTypeSelected => true; // always has a default

    private void OpenMealEntry()
    {
        EntryMealType = GuessNextMealType();
        IsMealEntryModalVisible = true;
        NotifyEntryProperties();
    }

    private void CloseMealEntry()
    {
        IsMealEntryModalVisible = false;
    }

    private void SelectEntryMealType(MealType mealType)
    {
        EntryMealType = mealType;
        NotifyEntryProperties();
    }

    private void StartManualMeal()
    {
        SelectedMealType = EntryMealType;
        IsMealEntryModalVisible = false;
        ResetMealDraft();
        OpenMealEditor();
    }

    private void StartSuggestedMeal()
    {
        SuggestionMealType = EntryMealType;
        IsDayPlanMode = false;
        IsMealEntryModalVisible = false;
        IsMealTypePickerVisible = false;
        IsLoadingSuggestions = false;
        IsMealDetailVisible = false;
        SelectedMealDetail = null;
        MealSuggestions.Clear();
        _shownConceptNames.Clear();
        MealSuggestionError = string.Empty;
        SuggestionTargetText = "Built around today's targets";
        IsSuggestionModalVisible = true;
        NotifySuggestionProperties();
        _ = LoadSuggestionsAsync();
    }

    private void StartDayPlan()
    {
        IsMealEntryModalVisible = false;
        IsDayPlanMode = true;
        IsMealTypePickerVisible = false;
        IsLoadingSuggestions = false;
        IsMealDetailVisible = false;
        SelectedMealDetail = null;
        MealSuggestions.Clear();
        DayPlanMeals.Clear();
        _shownConceptNames.Clear();
        MealSuggestionError = string.Empty;
        DayPlanSummaryText = "Preparing meals around today's remaining targets";
        IsSuggestionModalVisible = true;
        NotifySuggestionProperties();
        _ = LoadDayPlanAsync();
    }

    private void NotifyEntryProperties()
    {
        OnPropertyChanged(nameof(IsEntryBreakfastSelected));
        OnPropertyChanged(nameof(IsEntryLunchSelected));
        OnPropertyChanged(nameof(IsEntryDinnerSelected));
        OnPropertyChanged(nameof(IsEntrySnackSelected));
    }
}
