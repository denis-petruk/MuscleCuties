using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.UI.Nutrition;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    [ObservableProperty] private bool _isSuggestionModalVisible;
    [ObservableProperty] private bool _isLoadingSuggestions;
    [ObservableProperty] private bool _isMealTypePickerVisible;
    [ObservableProperty] private MealType _suggestionMealType = MealType.Lunch;
    [ObservableProperty] private ObservableCollection<MealSuggestionItem> _mealSuggestions = new();
    [ObservableProperty] private string _suggestionTargetText = string.Empty;

    private readonly HashSet<string> _shownConceptNames = new();

    public bool HasMealSuggestions => MealSuggestions.Count > 0;
    public bool HasNoMealSuggestions => !IsLoadingSuggestions && !IsMealTypePickerVisible && MealSuggestions.Count == 0;
    public bool ShowSuggestionResults => !IsMealTypePickerVisible;

    private void OpenSuggestMeal()
    {
        SuggestionMealType = GuessNextMealType();
        IsMealTypePickerVisible = true;
        IsSuggestionModalVisible = true;
        MealSuggestions.Clear();
        _shownConceptNames.Clear();
        SuggestionTargetText = string.Empty;
        NotifySuggestionProperties();
    }

    private void SelectSuggestionMealType(MealType mealType)
    {
        SuggestionMealType = mealType;
        IsMealTypePickerVisible = false;
        NotifySuggestionProperties();
        _ = LoadSuggestionsAsync();
    }

    private void RefreshSuggestions()
    {
        foreach (var item in MealSuggestions)
            _shownConceptNames.Add(item.ConceptName);

        _ = LoadSuggestionsAsync(_shownConceptNames);
    }

    private void CloseSuggestionModal()
    {
        IsSuggestionModalVisible = false;
        IsMealTypePickerVisible = false;
        MealSuggestions.Clear();
        _shownConceptNames.Clear();
        NotifySuggestionProperties();
    }

    private async Task LoadSuggestionsAsync(IReadOnlySet<string>? excludeConceptNames = null)
    {
        IsLoadingSuggestions = true;
        MealSuggestions.Clear();
        NotifySuggestionProperties();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var nutritionService = scope.ServiceProvider.GetRequiredService<INutritionService>();

            var breakfastShare = IsSweetBreakfast ? 0.20f : 0.25f;
            var lunchShare = IsSweetBreakfast ? 0.32f : 0.35f;
            var dinnerShare = IsSweetBreakfast ? 0.28f : 0.27f;
            var snackShare = 1f - breakfastShare - lunchShare - dinnerShare;

            SuggestionTargetText = SuggestionMealType switch
            {
                MealType.Breakfast => $"Breakfast · {(int)(TargetCalories * breakfastShare)} kcal target",
                MealType.Lunch => $"Lunch · {(int)(TargetCalories * lunchShare)} kcal target",
                MealType.Dinner => $"Dinner · {(int)(TargetCalories * dinnerShare)} kcal target",
                MealType.Snack => $"Snack · {(int)(TargetCalories * snackShare)} kcal target",
                _ => "Meal suggestion"
            };

            var mealType = SuggestionMealType;
            var breakfastPreference = BreakfastPreference;
            var phase = CurrentPhase;
            var date = DateTime.Today;
            var exclude = excludeConceptNames;
            var suggestions = await DataLoadScheduler.RunAsync(async () =>
            {
                var userId = await authService.GetCurrentUserIdAsync();
                var meals = await nutritionService.GetSuggestedMealsAsync(
                    userId, mealType, breakfastPreference, phase, date, exclude);
                return meals.Select(MealSuggestionItem.FromSuggestedMeal).ToList();
            });

            foreach (var item in suggestions)
                _shownConceptNames.Add(item.ConceptName);

            MealSuggestions = new ObservableCollection<MealSuggestionItem>(suggestions);
        }
        catch
        {
        }
        finally
        {
            IsLoadingSuggestions = false;
            NotifySuggestionProperties();
        }
    }

    private void AcceptSuggestion(MealSuggestionItem? suggestion)
    {
        if (suggestion is null || suggestion.Components.Count == 0)
            return;

        MealIngredients = new ObservableCollection<MealIngredientItem>(suggestion.Components
            .Select(component => new MealIngredientItem
            {
                FoodItemId = component.FoodItemId,
                Name = component.Name,
                Grams = component.Grams,
                Amount = component.Grams,
                ServingLabel = "g",
                Calories = component.Calories,
                Protein = component.Protein,
                Carbs = component.Carbs,
                Fats = component.Fats
            }));

        SelectedMealType = SuggestionMealType;
        SelectedMealTime = DateTime.Now.TimeOfDay;
        IsAddFoodPanelVisible = true;
        IsFoodFinderExpanded = false;
        SelectedFoodResult = null;
        SearchQuery = string.Empty;
        FoodSearchResults = [];
        ResetFoodSearchPaging();
        AddFoodMessage = "Suggestion applied. Adjust or log it.";
        NotifyMealIngredientProperties();

        CloseSuggestionModal();
    }

    private MealType GuessNextMealType()
    {
        var hour = DateTime.Now.Hour;
        return hour switch
        {
            < 10 => MealType.Breakfast,
            < 14 => MealType.Lunch,
            < 17 => MealType.Snack,
            _ => MealType.Dinner
        };
    }

    private void NotifySuggestionProperties()
    {
        OnPropertyChanged(nameof(HasMealSuggestions));
        OnPropertyChanged(nameof(HasNoMealSuggestions));
        OnPropertyChanged(nameof(ShowSuggestionResults));
    }
}
