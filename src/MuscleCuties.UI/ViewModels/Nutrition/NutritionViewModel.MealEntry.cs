using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    [ObservableProperty] private MealType _entryMealType;
    [ObservableProperty] private TimeSpan _entryMealTime;
    [ObservableProperty] private bool _isMealEntryModalVisible;

    public bool IsEntryBreakfastSelected => EntryMealType == MealType.Breakfast;
    public bool IsEntryLunchSelected => EntryMealType == MealType.Lunch;
    public bool IsEntryDinnerSelected => EntryMealType == MealType.Dinner;
    public bool IsEntrySnackSelected => EntryMealType == MealType.Snack;

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in Nutrition XAML / view model contract.")]
    public bool HasEntryMealTypeSelected => true;

    private void OpenMealEntry()
    {
        EntryMealType = GuessNextMealType();
        EntryMealTime = DateTime.Now.TimeOfDay;
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
        var chosenTime = EntryMealTime;
        IsMealEntryModalVisible = false;
        ResetMealDraft();
        OpenMealEditor();
        SelectedMealType = EntryMealType;
        SelectedMealTime = chosenTime;
        IsMealChoiceLocked = true;
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
