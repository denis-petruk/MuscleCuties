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
    [ObservableProperty] private bool _isDayPlanMode;
    [ObservableProperty] private bool _isMealTypePickerVisible;
    [ObservableProperty] private bool _isMealDetailVisible;
    [ObservableProperty] private MealSuggestionItem? _selectedMealDetail;
    [ObservableProperty] private MealType _suggestionMealType = MealType.Lunch;
    [ObservableProperty] private ObservableCollection<MealSuggestionItem> _mealSuggestions = new();
    [ObservableProperty] private ObservableCollection<DayMealPlanItem> _dayPlanMeals = new();
    [ObservableProperty] private string _mealSuggestionError = string.Empty;
    [ObservableProperty] private string _suggestionTargetText = string.Empty;
    [ObservableProperty] private string _dayPlanSummaryText = string.Empty;

    private readonly HashSet<string> _shownConceptNames = new();
    private int _suggestionLoadVersion;
    private static readonly string[] SuggestionIconPalette =
    [
        "BowlSalad24",
        "FoodEgg24",
        "FoodToast24",
        "FoodGrains24",
        "FoodChickenLeg24",
        "FoodCarrot24",
        "BowlChopsticks24",
        "FoodCake24",
        "FoodPizza24",
        "FoodFish24",
        "LeafThree24",
        "Food24"
    ];

    public bool HasMealSuggestions => MealSuggestions.Count > 0;
    public bool HasDayPlanMeals => DayPlanMeals.Count > 0;
    public bool HasMealSuggestionError => !string.IsNullOrWhiteSpace(MealSuggestionError);
    public bool HasNoMealSuggestions =>
        !IsDayPlanMode &&
        !IsLoadingSuggestions &&
        !IsMealTypePickerVisible &&
        !HasMealSuggestionError &&
        MealSuggestions.Count == 0;
    public bool HasNoDayPlanMeals =>
        IsDayPlanMode &&
        !IsLoadingSuggestions &&
        !HasMealSuggestionError &&
        DayPlanMeals.Count == 0;
    public bool ShowDayPlanMeals =>
        IsDayPlanMode && HasDayPlanMeals && !IsLoadingSuggestions && !HasMealSuggestionError;
    public bool ShowSuggestionResults => !IsMealTypePickerVisible;
    public string SuggestionPageTitle => IsDayPlanMode ? "Today's meal plan" : "Meal ideas";
    public string LoadingSuggestionText => IsDayPlanMode ? "Building today's meal plan" : "Finding a good fit";

    public string SuggestionMealTypeCaloriesText => GetMealTypeCaloriesText(SuggestionMealType);

    public string BreakfastCaloriesHint => GetMealTypeCaloriesText(MealType.Breakfast);
    public string LunchCaloriesHint => GetMealTypeCaloriesText(MealType.Lunch);
    public string DinnerCaloriesHint => GetMealTypeCaloriesText(MealType.Dinner);
    public string SnackCaloriesHint => GetMealTypeCaloriesText(MealType.Snack);

    public bool IsBreakfastGuessed => SuggestionMealType == MealType.Breakfast && IsMealTypePickerVisible;
    public bool IsLunchGuessed => SuggestionMealType == MealType.Lunch && IsMealTypePickerVisible;
    public bool IsDinnerGuessed => SuggestionMealType == MealType.Dinner && IsMealTypePickerVisible;
    public bool IsSnackGuessed => SuggestionMealType == MealType.Snack && IsMealTypePickerVisible;

    public string DetailMacrosText => SelectedMealDetail?.MacrosText ?? string.Empty;
    public string DetailCaloriesText => SelectedMealDetail?.CaloriesText ?? string.Empty;
    public string DetailDirectionsText => SelectedMealDetail?.DirectionsText ?? string.Empty;
    public bool DetailHasSpiceBlends => SelectedMealDetail?.HasSpiceBlends ?? false;

    public float RemainingCalories => Math.Max(0, TargetCalories - ConsumedCalories);
    public string RemainingCaloriesText => $"{(int)RemainingCalories} kcal remaining today";
    public string DailyProgressText =>
        $"{(int)ConsumedCalories} / {(int)TargetCalories} kcal consumed";
    public string DailyMacroRemainingText
    {
        get
        {
            var rp = Math.Max(0, TargetProtein - ConsumedProtein);
            var rc = Math.Max(0, TargetCarbs - ConsumedCarbs);
            var rf = Math.Max(0, TargetFats - ConsumedFats);
            return $"Remaining: P {rp:N0}g  C {rc:N0}g  F {rf:N0}g";
        }
    }

    public float MealCaloriesFillPercent
    {
        get
        {
            if (TargetCalories <= 0 || SelectedMealDetail is null)
                return 0;
            return Math.Clamp(SelectedMealDetail.Macros.Calories / TargetCalories, 0f, 1f);
        }
    }

    public float ConsumedCaloriesFillPercent =>
        TargetCalories <= 0 ? 0 : Math.Clamp(ConsumedCalories / TargetCalories, 0f, 1f);

    public string MealFillPercentText
    {
        get
        {
            if (TargetCalories <= 0 || SelectedMealDetail is null)
                return string.Empty;
            var pct = SelectedMealDetail.Macros.Calories / TargetCalories * 100f;
            return $"This meal is {pct:N0}% of your daily target";
        }
    }

    private void OpenSuggestMeal()
    {
        _suggestionLoadVersion++;
        IsDayPlanMode = false;
        SuggestionMealType = GuessNextMealType();
        IsMealTypePickerVisible = true;
        IsLoadingSuggestions = false;
        IsMealDetailVisible = false;
        SelectedMealDetail = null;
        MealSuggestions.Clear();
        DayPlanMeals.Clear();
        _shownConceptNames.Clear();
        MealSuggestionError = string.Empty;
        SuggestionTargetText = "Built around today's targets";
        IsSuggestionModalVisible = true;
        NotifySuggestionProperties();
    }

    private void SelectSuggestionMealType(MealType mealType)
    {
        IsDayPlanMode = false;
        SuggestionMealType = mealType;
        IsMealTypePickerVisible = false;
        NotifySuggestionProperties();
        _ = LoadSuggestionsAsync();
    }

    private void RefreshSuggestions()
    {
        if (IsDayPlanMode)
        {
            _ = LoadDayPlanAsync();
            return;
        }

        foreach (var item in MealSuggestions)
            _shownConceptNames.Add(item.ConceptName);

        _ = LoadSuggestionsAsync(_shownConceptNames);
    }

    private void CloseSuggestionModal()
    {
        _suggestionLoadVersion++;
        IsSuggestionModalVisible = false;
        IsDayPlanMode = false;
        IsMealDetailVisible = false;
        _shownConceptNames.Clear();
        NotifySuggestionProperties();
    }

    private void OpenMealDetail(MealSuggestionItem? item)
    {
        if (item is null)
            return;

        SelectedMealDetail = item;
        IsMealDetailVisible = true;
        NotifyDetailProperties();
    }

    private void CloseMealDetail()
    {
        IsMealDetailVisible = false;
    }

    private async Task LoadSuggestionsAsync(IReadOnlySet<string>? excludeConceptNames = null)
    {
        var loadVersion = ++_suggestionLoadVersion;
        IsLoadingSuggestions = true;
        MealSuggestions.Clear();
        MealSuggestionError = string.Empty;
        NotifySuggestionProperties();

        try
        {
            await _referenceDataPreparation.EnsureNutritionReadyAsync();
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var nutritionService = scope.ServiceProvider.GetRequiredService<INutritionService>();

            SuggestionTargetText = $"{SuggestionMealType} · {GetMealTypeCaloriesText(SuggestionMealType)}";

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
                var items = meals.Select(MealSuggestionItem.FromSuggestedMeal).ToList();
                AssignDistinctSuggestionIcons(items);
                return items;
            });

            if (loadVersion != _suggestionLoadVersion || !IsSuggestionModalVisible)
                return;

            foreach (var item in suggestions)
                _shownConceptNames.Add(item.ConceptName);

            MealSuggestions = new ObservableCollection<MealSuggestionItem>(suggestions);
        }
        catch
        {
            if (loadVersion == _suggestionLoadVersion)
                MealSuggestionError = "Meal ideas could not refresh. Check your connection and try again.";
        }
        finally
        {
            if (loadVersion == _suggestionLoadVersion)
            {
                IsLoadingSuggestions = false;
                NotifySuggestionProperties();
            }
        }
    }

    private static void AssignDistinctSuggestionIcons(IReadOnlyList<MealSuggestionItem> suggestions)
    {
        var usedIcons = new HashSet<string>(StringComparer.Ordinal);
        var fallbackIndex = 0;

        foreach (var suggestion in suggestions)
        {
            if (usedIcons.Add(suggestion.IconGlyph))
                continue;

            while (fallbackIndex < SuggestionIconPalette.Length &&
                   usedIcons.Contains(SuggestionIconPalette[fallbackIndex]))
            {
                fallbackIndex++;
            }

            if (fallbackIndex >= SuggestionIconPalette.Length)
                break;

            suggestion.IconGlyph = SuggestionIconPalette[fallbackIndex++];
            usedIcons.Add(suggestion.IconGlyph);
        }
    }

    private void AcceptSuggestion(MealSuggestionItem? suggestion)
    {
        if (suggestion is null || suggestion.Components.Count == 0)
            return;

        ResetMealDraft();
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
        IsMealEditorVisible = true;
        IsFoodFinderExpanded = false;
        SelectedFoodResult = null;
        SearchQuery = string.Empty;
        FoodSearchResults = [];
        ResetFoodSearchPaging();
        AddFoodMessage = string.Empty;
        NotifyMealIngredientProperties();

        CloseSuggestionModal();
    }

    private void BackToMealTypePicker()
    {
        _suggestionLoadVersion++;
        IsDayPlanMode = false;
        IsMealTypePickerVisible = true;
        IsLoadingSuggestions = false;
        MealSuggestionError = string.Empty;
        DayPlanMeals.Clear();
        NotifySuggestionProperties();
    }

    private void RefreshDayMeal(DayMealPlanItem? item)
    {
        if (item is null || !item.CanRefresh)
            return;

        _ = RefreshDayMealAsync(item);
    }

    private async Task RefreshDayMealAsync(DayMealPlanItem item)
    {
        var version = _suggestionLoadVersion;
        var index = DayPlanMeals.IndexOf(item);
        if (index < 0)
            return;

        DayPlanMeals[index] = item with { IsRefreshing = true, ErrorMessage = string.Empty };
        try
        {
            await _referenceDataPreparation.EnsureNutritionReadyAsync();
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var nutritionService = scope.ServiceProvider.GetRequiredService<INutritionService>();
            var userId = await authService.GetCurrentUserIdAsync();
            var excludeNames = DayPlanMeals
                .Where(meal => meal.MealType != item.MealType)
                .Select(meal => meal.Suggestion?.ConceptName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Append(item.Suggestion?.ConceptName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var suggestions = await nutritionService.GetSuggestedMealsForTargetAsync(
                userId,
                item.Target,
                BreakfastPreference,
                CurrentPhase,
                DateTime.Today,
                excludeNames);

            if (version != _suggestionLoadVersion || !IsSuggestionModalVisible || !IsDayPlanMode)
                return;

            var refreshed = DayPlanMeals.FirstOrDefault(meal => meal.MealType == item.MealType);
            if (refreshed is null)
                return;

            var currentIndex = DayPlanMeals.IndexOf(refreshed);
            DayPlanMeals[currentIndex] = refreshed with
            {
                IsRefreshing = false,
                Suggestion = suggestions.FirstOrDefault() is { } suggestion
                    ? MealSuggestionItem.FromSuggestedMeal(suggestion)
                    : null,
                ErrorMessage = suggestions.Count == 0 ? "Try refreshing again." : string.Empty
            };
        }
        catch
        {
            if (version != _suggestionLoadVersion)
                return;

            var refreshed = DayPlanMeals.FirstOrDefault(meal => meal.MealType == item.MealType);
            if (refreshed is not null)
            {
                var currentIndex = DayPlanMeals.IndexOf(refreshed);
                DayPlanMeals[currentIndex] = refreshed with
                {
                    IsRefreshing = false,
                    ErrorMessage = "Could not refresh this meal. Try again."
                };
            }
        }
    }

    private void UsePlannedMeal(DayMealPlanItem? item)
    {
        if (item?.Suggestion is null)
            return;

        SuggestionMealType = item.MealType;
        AcceptSuggestion(item.Suggestion);
    }

    private async Task LoadDayPlanAsync()
    {
        var loadVersion = ++_suggestionLoadVersion;
        IsLoadingSuggestions = true;
        DayPlanMeals.Clear();
        MealSuggestionError = string.Empty;
        DayPlanSummaryText = "Preparing meals around today's remaining targets";
        NotifySuggestionProperties();

        try
        {
            await _referenceDataPreparation.EnsureNutritionReadyAsync();
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var nutritionService = scope.ServiceProvider.GetRequiredService<INutritionService>();
            var mealPlan = await DataLoadScheduler.RunAsync(async () =>
            {
                var userId = await authService.GetCurrentUserIdAsync();
                return await nutritionService.GetSuggestedMealPlanAsync(
                    userId,
                    BreakfastPreference,
                    CurrentPhase,
                    DateTime.Today);
            });

            if (loadVersion != _suggestionLoadVersion || !IsSuggestionModalVisible || !IsDayPlanMode)
                return;

            DayPlanSummaryText = $"{mealPlan.RemainingCalories:N0} kcal remaining · {mealPlan.DailyTargetCalories:N0} kcal daily target";
            DayPlanMeals = new ObservableCollection<DayMealPlanItem>(mealPlan.Meals.Select(entry =>
                new DayMealPlanItem(
                    entry.MealType,
                    entry.Target,
                    mealPlan.DailyTargetCalories,
                    entry.IsAlreadyLogged,
                    false,
                    entry.Suggestion is null ? null : MealSuggestionItem.FromSuggestedMeal(entry.Suggestion))));
        }
        catch
        {
            if (loadVersion == _suggestionLoadVersion)
                MealSuggestionError = "Today's meal plan could not load. Refresh to try again.";
        }
        finally
        {
            if (loadVersion == _suggestionLoadVersion)
            {
                IsLoadingSuggestions = false;
                NotifySuggestionProperties();
            }
        }
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

    private string GetMealTypeCaloriesText(MealType mealType)
    {
        var breakfastShare = IsSweetBreakfast ? 0.20f : 0.25f;
        var lunchShare = IsSweetBreakfast ? 0.32f : 0.33f;
        var dinnerShare = IsSweetBreakfast ? 0.28f : 0.25f;
        var snackShare = 1f - breakfastShare - lunchShare - dinnerShare;

        var share = mealType switch
        {
            MealType.Breakfast => breakfastShare,
            MealType.Lunch => lunchShare,
            MealType.Dinner => dinnerShare,
            MealType.Snack => snackShare,
            _ => 0.25f
        };

        return $"{(int)(TargetCalories * share)} kcal target";
    }

    private void NotifySuggestionProperties()
    {
        OnPropertyChanged(nameof(HasMealSuggestions));
        OnPropertyChanged(nameof(HasDayPlanMeals));
        OnPropertyChanged(nameof(HasMealSuggestionError));
        OnPropertyChanged(nameof(HasNoMealSuggestions));
        OnPropertyChanged(nameof(HasNoDayPlanMeals));
        OnPropertyChanged(nameof(ShowDayPlanMeals));
        OnPropertyChanged(nameof(ShowSuggestionResults));
        OnPropertyChanged(nameof(SuggestionPageTitle));
        OnPropertyChanged(nameof(LoadingSuggestionText));
        OnPropertyChanged(nameof(IsBreakfastGuessed));
        OnPropertyChanged(nameof(IsLunchGuessed));
        OnPropertyChanged(nameof(IsDinnerGuessed));
        OnPropertyChanged(nameof(IsSnackGuessed));
        OnPropertyChanged(nameof(DayPlanSummaryText));
        OnPropertyChanged(nameof(SuggestionMealTypeCaloriesText));
        OnPropertyChanged(nameof(BreakfastCaloriesHint));
        OnPropertyChanged(nameof(LunchCaloriesHint));
        OnPropertyChanged(nameof(DinnerCaloriesHint));
        OnPropertyChanged(nameof(SnackCaloriesHint));
    }

    private void NotifyDetailProperties()
    {
        OnPropertyChanged(nameof(DetailMacrosText));
        OnPropertyChanged(nameof(DetailCaloriesText));
        OnPropertyChanged(nameof(DetailDirectionsText));
        OnPropertyChanged(nameof(DetailHasSpiceBlends));
        OnPropertyChanged(nameof(RemainingCalories));
        OnPropertyChanged(nameof(RemainingCaloriesText));
        OnPropertyChanged(nameof(DailyProgressText));
        OnPropertyChanged(nameof(DailyMacroRemainingText));
        OnPropertyChanged(nameof(MealCaloriesFillPercent));
        OnPropertyChanged(nameof(ConsumedCaloriesFillPercent));
        OnPropertyChanged(nameof(MealFillPercentText));
    }
}
