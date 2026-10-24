using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class NutritionViewModel
{
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

    private readonly HashSet<string> _shownConceptNames = new();
    [ObservableProperty] private ObservableCollection<DayMealPlanItem> _dayPlanMeals = new();
    [ObservableProperty] private string _dayPlanSummaryText = string.Empty;
    [ObservableProperty] private bool _isDayPlanMode;
    [ObservableProperty] private bool _isLoadingSuggestions;
    [ObservableProperty] private bool _isMealDetailVisible;
    [ObservableProperty] private bool _isMealTypePickerVisible;
    [ObservableProperty] private bool _isSuggestionModalVisible;
    [ObservableProperty] private string _mealSuggestionError = string.Empty;
    [ObservableProperty] private ObservableCollection<MealSuggestionItem> _mealSuggestions = new();
    [ObservableProperty] private MealSuggestionItem? _selectedMealDetail;
    private int _suggestionLoadVersion;
    private TimeSpan? _suggestionMealTime;
    [ObservableProperty] private MealType _suggestionMealType = MealType.Lunch;
    [ObservableProperty] private string _suggestionTargetText = string.Empty;

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
        _suggestionMealTime = null;
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
        _suggestionMealTime = null;
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
            SuggestionTargetText = $"{SuggestionMealType} · {GetMealTypeCaloriesText(SuggestionMealType)}";

            var dtos = await _overviewUseCase.GetMealSuggestionsAsync(
                SuggestionMealType.ToString(),
                BreakfastPreference.ToString(),
                excludeConceptNames);

            if (loadVersion != _suggestionLoadVersion || !IsSuggestionModalVisible)
                return;

            var items = dtos.Select(MapSuggestionDtoToItem).ToList();
            AssignDistinctSuggestionIcons(items);

            foreach (var item in items)
                _shownConceptNames.Add(item.ConceptName);

            MealSuggestions = new ObservableCollection<MealSuggestionItem>(items);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[NutritionViewModel] Meal suggestions failed ({ex.GetType().Name}).");
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
                fallbackIndex++;

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
        _acceptedSuggestionName = suggestion.ConceptName;
        _acceptedSuggestionPhase = CurrentPhase;
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
        SelectedMealTime = _suggestionMealTime ?? DateTime.Now.TimeOfDay;
        IsMealChoiceLocked = _suggestionMealTime.HasValue;
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
            var refreshedDto = await _overviewUseCase.RefreshDayMealAsync(
                new DayMealPlanItemDto(
                    item.MealType.ToString(),
                    item.Target.Calories,
                    item.Target.Protein,
                    item.Target.Carbs,
                    item.Target.Fats,
                    item.DailyCaloriesTarget,
                    item.IsAlreadyLogged,
                    null,
                    item.ErrorMessage),
                BreakfastPreference.ToString());

            if (version != _suggestionLoadVersion || !IsSuggestionModalVisible || !IsDayPlanMode)
                return;

            var refreshed = DayPlanMeals.FirstOrDefault(meal => meal.MealType == item.MealType);
            if (refreshed is null)
                return;

            var currentIndex = DayPlanMeals.IndexOf(refreshed);
            DayPlanMeals[currentIndex] = refreshed with
            {
                IsRefreshing = false,
                Suggestion = refreshedDto.Suggestion is not null
                    ? MapSuggestionDtoToItem(refreshedDto.Suggestion)
                    : null,
                ErrorMessage = refreshedDto.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[NutritionViewModel] Meal suggestion refresh failed ({ex.GetType().Name}).");
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
            var planList = await _overviewUseCase.GetDayMealPlanAsync(BreakfastPreference.ToString());

            if (loadVersion != _suggestionLoadVersion || !IsSuggestionModalVisible || !IsDayPlanMode)
                return;

            DayPlanSummaryText = $"{(int)RemainingCalories} kcal remaining · {(int)TargetCalories} kcal daily target";
            DayPlanMeals = new ObservableCollection<DayMealPlanItem>(planList.Select(entry =>
            {
                var mt = Enum.TryParse<MealType>(entry.MealType, true, out var parsedMt) ? parsedMt : MealType.Lunch;
                return new DayMealPlanItem(
                    mt,
                    new MealNutritionTarget(mt, entry.TargetCalories, entry.TargetProtein, entry.TargetCarbs,
                        entry.TargetFats),
                    entry.DailyCaloriesTarget,
                    entry.IsAlreadyLogged,
                    false,
                    entry.Suggestion is null ? null : MapSuggestionDtoToItem(entry.Suggestion));
            }));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[NutritionViewModel] Day meal plan failed ({ex.GetType().Name}).");
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

    private static MealType GuessNextMealType()
    {
        var hour = DateTime.Now.Hour;
        return hour switch
        {
            >= 5 and < 11 => MealType.Breakfast,
            >= 11 and < 15 => MealType.Lunch,
            >= 15 and < 17 => MealType.Snack,
            >= 17 and < 22 => MealType.Dinner,
            _ => MealType.Breakfast
        };
    }

    private string GetMealTypeCaloriesText(MealType mealType)
    {
        var share = _mealDistribution;
        var targetFraction = mealType switch
        {
            MealType.Breakfast => share?.Breakfast ?? 0f,
            MealType.Lunch => share?.Lunch ?? 0f,
            MealType.Dinner => share?.Dinner ?? 0f,
            MealType.Snack => share?.Snack ?? 0f,
            _ => 0.25f
        };
        var calories = (int)(TargetCalories * targetFraction);
        return $"{calories:N0} kcal target";
    }

    private static MealSuggestionItem MapSuggestionDtoToItem(MealSuggestionDto dto)
    {
        var components = dto.Components.Select(c => new MealSuggestionComponentItem
        {
            FoodItemId = c.FoodItemId,
            Name = c.Name,
            ComponentType = Enum.TryParse<ComponentType>(c.ComponentType, true, out var ct) ? ct : ComponentType.Mixed,
            Role = Enum.TryParse<ComponentRole>(c.Role, true, out var cr) ? cr : ComponentRole.CarbBase,
            Grams = c.Grams,
            IsLocked = c.IsLocked,
            Calories = c.Calories,
            Protein = c.Protein,
            Carbs = c.Carbs,
            Fats = c.Fats
        }).ToList();

        var groups = dto.ComponentGroups.Select(g => new MealSuggestionComponentGroup(
            g.ComponentType,
            g.Title,
            g.Items.Select(ci => new MealSuggestionComponentItem
            {
                FoodItemId = ci.FoodItemId,
                Name = ci.Name,
                ComponentType = Enum.TryParse<ComponentType>(ci.ComponentType, true, out var ct)
                    ? ct
                    : ComponentType.Mixed,
                Role = Enum.TryParse<ComponentRole>(ci.Role, true, out var cr) ? cr : ComponentRole.CarbBase,
                Grams = ci.Grams,
                IsLocked = ci.IsLocked,
                Calories = ci.Calories,
                Protein = ci.Protein,
                Carbs = ci.Carbs,
                Fats = ci.Fats
            }).ToList()
        )).ToList();

        return new MealSuggestionItem
        {
            ConceptName = dto.ConceptName,
            ConceptDescription = dto.ConceptDescription,
            SpiceBlends = dto.SpiceBlendNames.Select((name, i) => new SpiceBlendDetail(name, string.Empty, i == 0))
                .ToList(),
            Components = components,
            ComponentGroups = groups,
            Macros = new MacroNutrients(dto.Calories, dto.Protein, dto.Carbs, dto.Fats),
            Score = dto.Score,
            Style = Enum.TryParse<MealStyle>(dto.Style, true, out var s) ? s : MealStyle.Plated,
            IconGlyph = dto.IconGlyph
        };
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
