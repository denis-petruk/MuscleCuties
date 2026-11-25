// Meal suggestions and the day-plan flow.
// Second half of NutritionViewModel; see its declaration for the full contract.

using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>Suggestion state for the nutrition page: the meal-idea sheet and the day plan it shares a surface with,</summary>
/// plus the meal detail opened from a suggestion.</summary>
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
    private bool _returnToMealEditorAfterSuggestion;
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

    /// <summary>True when suggestions are loaded.</summary>
    public bool HasMealSuggestions => MealSuggestions.Count > 0;
    /// <summary>True when the day plan has meals.</summary>
    public bool HasDayPlanMeals => DayPlanMeals.Count > 0;
    /// <summary>True when loading suggestions failed.</summary>
    public bool HasMealSuggestionError => !string.IsNullOrWhiteSpace(MealSuggestionError);

    /// <summary>True when the search produced no suggestions.</summary>
    public bool HasNoMealSuggestions =>
        !IsDayPlanMode &&
        !IsLoadingSuggestions &&
        !HasMealSuggestionError &&
        MealSuggestions.Count == 0;

    /// <summary>True when the day plan has no meals.</summary>
    public bool HasNoDayPlanMeals =>
        IsDayPlanMode &&
        !IsLoadingSuggestions &&
        !HasMealSuggestionError &&
        DayPlanMeals.Count == 0;

    /// <summary>True when the day plan is shown.</summary>
    public bool ShowDayPlanMeals =>
        IsDayPlanMode && HasDayPlanMeals && !IsLoadingSuggestions && !HasMealSuggestionError;

    /// <summary>True when suggestion results are shown.</summary>
    public bool ShowSuggestionResults => !IsDayPlanMode && HasMealSuggestions;
    /// <summary>The sheet's heading, which names a full day plan or a single meal depending on the mode.</summary>
    public string SuggestionPageTitle => IsDayPlanMode ? "Today's meal plan" : "Meal ideas";
    /// <summary>The loading line, worded for building a day or for finding one meal.</summary>
    public string LoadingSuggestionText => IsDayPlanMode ? "Building today's meal plan" : "Finding a good fit";

    /// <summary>True when the suggestion is for breakfast.</summary>
    public bool IsBreakfastSuggestion => SuggestionMealType == MealType.Breakfast;
    /// <summary>The breakfast preference is offered wherever a breakfast would be planned — a day plan always, a single</summary>
    /// meal only when it is breakfast.</summary>
    public bool ShowBreakfastPreferenceToggle =>
        (!IsDayPlanMode && IsBreakfastSuggestion) || IsDayPlanMode;

    /// <summary>The calorie allowance for the meal being suggested.</summary>
    public string SuggestionMealTypeCaloriesText => GetMealTypeCaloriesText(SuggestionMealType);

    /// <summary>The allowance beside the breakfast choice.</summary>
    public string BreakfastCaloriesHint => GetMealTypeCaloriesText(MealType.Breakfast);
    /// <summary>The allowance beside the lunch choice.</summary>
    public string LunchCaloriesHint => GetMealTypeCaloriesText(MealType.Lunch);
    /// <summary>The allowance beside the dinner choice.</summary>
    public string DinnerCaloriesHint => GetMealTypeCaloriesText(MealType.Dinner);
    /// <summary>The allowance beside the snack choice.</summary>
    public string SnackCaloriesHint => GetMealTypeCaloriesText(MealType.Snack);

    /// <summary>The macros line of the shown meal.</summary>
    public string DetailMacrosText => SelectedMealDetail?.MacrosText ?? string.Empty;
    /// <summary>The calories line of the shown meal.</summary>
    public string DetailCaloriesText => SelectedMealDetail?.CaloriesText ?? string.Empty;
    /// <summary>The directions of the shown meal.</summary>
    public string DetailDirectionsText => SelectedMealDetail?.DirectionsText ?? string.Empty;
    /// <summary>True when the shown meal lists spice blends.</summary>
    public bool DetailHasSpiceBlends => SelectedMealDetail?.HasSpiceBlends ?? false;

    /// <summary>What is left of the day's allowance, floored at zero so an overshoot reads as none left rather than a</summary>
    /// negative remainder.</summary>
    public float RemainingCalories => Math.Max(0, TargetCalories - ConsumedCalories);
    /// <summary>The remainder as a whole number in the daily-remaining wording.</summary>
    public string RemainingCaloriesText =>
        UiTextCatalog.Format("Nutrition_RemainingTodayFormat", (int)RemainingCalories);

    /// <summary>Consumed against target for the day, both rounded to whole calories.</summary>
    public string DailyProgressText =>
        UiTextCatalog.Format("Nutrition_DailyProgressFormat", (int)ConsumedCalories, (int)TargetCalories);

    public string DailyMacroRemainingText
    {
        get
        {
            var rp = Math.Max(0, TargetProtein - ConsumedProtein);
            var rc = Math.Max(0, TargetCarbs - ConsumedCarbs);
            var rf = Math.Max(0, TargetFats - ConsumedFats);
            return UiTextCatalog.Format("Nutrition_Breakdown_RemainingFormat", DisplayFormat.Macros(rp, rc, rf));
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

    /// <summary>The day's progress as a 0–1 fraction for the ring; without a target there is nothing to fill.</summary>
    public float ConsumedCaloriesFillPercent =>
        TargetCalories <= 0 ? 0 : Math.Clamp(ConsumedCalories / TargetCalories, 0f, 1f);

    public string MealFillPercentText
    {
        get
        {
            if (TargetCalories <= 0 || SelectedMealDetail is null)
                return string.Empty;
            var pct = SelectedMealDetail.Macros.Calories / TargetCalories * 100f;
            return UiTextCatalog.Format("Nutrition_MealDetail_DailyTargetShareFormat", pct);
        }
    }

    /// <summary>Opens the meal editor, which is where a suggestion is accepted into.</summary>
    private void OpenSuggestMeal()
    {
        OpenMealEntry();
    }

    /// <summary>Leaves day-plan mode for a single meal of the chosen type and loads ideas for it.</summary>
    private void SelectSuggestionMealType(MealType mealType)
    {
        IsDayPlanMode = false;
        SuggestionMealType = mealType;
        IsMealTypePickerVisible = false;
        NotifySuggestionProperties();
        _ = LoadSuggestionsAsync();
    }

    /// <summary>Reloads the ideas, first refreshing a stale breakfast target when the surface being shown depends on it.</summary>
    private void RefreshSuggestions()
    {
        if (_breakfastTargetsNeedRefresh && (IsDayPlanMode || IsBreakfastSuggestion))
        {
            _ = RefreshBreakfastPreferenceAsync();
            return;
        }

        if (IsDayPlanMode)
        {
            _ = LoadDayPlanAsync();
            return;
        }

        foreach (var item in MealSuggestions)
            _shownConceptNames.Add(item.ConceptName);

        _ = LoadSuggestionsAsync(_shownConceptNames);
    }

    /// <summary>Closes the sheet, invalidating any idea still loading, and reopens the meal editor when the sheet was</summary>
    /// entered from it.</summary>
    private void CloseSuggestionModal()
    {
        var reopenMealEditor = _returnToMealEditorAfterSuggestion;
        _returnToMealEditorAfterSuggestion = false;
        _suggestionLoadVersion++;
        _suggestionMealTime = null;
        IsSuggestionModalVisible = false;
        IsDayPlanMode = false;
        IsMealDetailVisible = false;
        _shownConceptNames.Clear();
        NotifySuggestionProperties();

        if (reopenMealEditor)
            IsMealEditorVisible = true;
    }

    /// <summary>Shows one suggestion's detail and publishes the fields the detail view binds to.</summary>
    private void OpenMealDetail(MealSuggestionItem? item)
    {
        if (item is null)
            return;

        SelectedMealDetail = item;
        IsMealDetailVisible = true;
        NotifyDetailProperties();
    }

    /// <summary>Hides the detail, keeping the selection so reopening is instant.</summary>
    private void CloseMealDetail()
    {
        IsMealDetailVisible = false;
    }

    /// <summary>Loads ideas, skipping the named concepts so a refresh offers something new, and ignoring a response that</summary>
    /// arrives after the sheet has moved on.</summary>
    private async Task LoadSuggestionsAsync(IReadOnlySet<string>? excludeConceptNames = null)
    {
        if (_breakfastTargetsNeedRefresh && IsBreakfastSuggestion)
        {
            await RefreshBreakfastPreferenceAsync();
            return;
        }

        var loadVersion = ++_suggestionLoadVersion;
        IsLoadingSuggestions = true;
        MealSuggestions.Clear();
        MealSuggestionError = string.Empty;
        NotifySuggestionProperties();

        try
        {
            SuggestionTargetText = UiTextCatalog.Format(
                SuggestionMealType switch
                {
                    MealType.Breakfast => "Nutrition_MealSuggestion_BreakfastValue",
                    MealType.Lunch => "Nutrition_MealSuggestion_LunchValue",
                    MealType.Dinner => "Nutrition_MealSuggestion_DinnerValue",
                    _ => "Nutrition_MealSuggestion_SnackValue"
                },
                GetMealTypeCaloriesText(SuggestionMealType));

            var dtos = await _overviewUseCase.GetMealSuggestionsAsync(
                SuggestionMealType.ToString(),
                BreakfastPreference.ToString(),
                excludeConceptNames);

            if (loadVersion != _suggestionLoadVersion || !IsSuggestionModalVisible)
                return;

            var items = dtos.Select(d => MapSuggestionDtoToItem(NutritionPresentationMapper.FromSuggestion(d))).ToList();
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

    /// <summary>Gives each idea a different icon, cycling the palette only when there are more ideas than icons.</summary>
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

    /// <summary>Adds a suggestion's components to the meal being edited, refusing one that has nothing to add.</summary>
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
                ServingLabel = UiTextCatalog.Get("Nutrition_Unit_GramsShort"),
                Calories = component.Calories,
                Protein = component.Protein,
                Carbs = component.Carbs,
                Fats = component.Fats,
                MacrosForAmount = NutritionPresentationMapper.FromPer100Grams(
                    component.Calories, component.Protein, component.Carbs, component.Fats, component.Grams)
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

    /// <summary>Returns from an idea to the meal-type choice it came from.</summary>
    private void BackToMealTypePicker()
    {
        CloseSuggestionModal();
        OpenMealEntry();
    }

    /// <summary>Replaces one meal of the day plan with a fresh proposal for the same slot.</summary>
    private void RefreshDayMeal(DayMealPlanItem? item)
    {
        if (item is null || !item.CanRefresh)
            return;

        _ = RefreshDayMealAsync(item);
    }

    /// <summary>Replaces one meal of the day plan with a fresh proposal for the same slot, keeping the rest of the plan</summary>
    /// the user has already seen.</summary>
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
                NutritionPresentationMapper.ToDayMealPlanRequest(item),
                BreakfastPreference.ToString());

            if (version != _suggestionLoadVersion || !IsSuggestionModalVisible || !IsDayPlanMode)
                return;

            var refreshed = DayPlanMeals.FirstOrDefault(meal => meal.MealType == item.MealType);
            if (refreshed is null)
                return;

            var currentIndex = DayPlanMeals.IndexOf(refreshed);
            var plan = NutritionPresentationMapper.FromDayMealPlan(refreshedDto);
            DayPlanMeals[currentIndex] = refreshed with
            {
                IsRefreshing = false,
                Suggestion = plan.Suggestion is not null
                    ? MapSuggestionDtoToItem(plan.Suggestion)
                    : null,
                ErrorMessage = plan.ErrorMessage
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
                    ErrorMessage = "Ui_Nutrition_Error_RefreshMeal"
                };
            }
        }
    }

    /// <summary>Accepts a planned meal into the editor, so the user can adjust it before logging.</summary>
    private void UsePlannedMeal(DayMealPlanItem? item)
    {
        if (item?.Suggestion is null)
            return;

        SuggestionMealType = item.MealType;
        AcceptSuggestion(item.Suggestion);
    }

    /// <summary>Builds today's meal plan, first refreshing a stale breakfast target because the plan's shares depend on</summary>
    /// it, and ignoring a plan that arrives after the sheet has moved on.</summary>
    private async Task LoadDayPlanAsync()
    {
        if (_breakfastTargetsNeedRefresh)
        {
            await RefreshBreakfastPreferenceAsync();
            return;
        }

        var loadVersion = ++_suggestionLoadVersion;
        IsLoadingSuggestions = true;
        DayPlanMeals.Clear();
        MealSuggestionError = string.Empty;
        DayPlanSummaryText = "Ui_Nutrition_DayPlan_Preparing";
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
                var plan = NutritionPresentationMapper.FromDayMealPlan(entry);
                return new DayMealPlanItem(
                    mt,
                    NutritionPresentationMapper.FromMealTargetValues(
                        plan.TargetCalories, plan.TargetProtein, plan.TargetCarbs, plan.TargetFats),
                    plan.DailyCaloriesTarget,
                    plan.IsAlreadyLogged,
                    false,
                    plan.Suggestion is null ? null : MapSuggestionDtoToItem(plan.Suggestion));
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

    /// <summary>The meal the clock suggests: breakfast until mid-morning, lunch over midday, a snack in the afternoon and</summary>
    /// dinner in the evening, defaulting to breakfast outside those hours.</summary>
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

    /// <summary>The calorie allowance for one meal type, from the plan's distribution.</summary>
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
        return DisplayFormat.Calories(calories);
    }

    /// <summary>A suggested meal as a list item, translating its name and components and falling back to mixed/carb-base</summary>
    /// when a newer server sends a component kind this build does not know.</summary>
    private static MealSuggestionItem MapSuggestionDtoToItem(NutritionMealSuggestion dto)
    {
        var components = dto.Components.Select(c => new MealSuggestionComponentItem
        {
            FoodItemId = c.FoodItemId,
            Name = UiTextCatalog.TranslateRuntimeText(c.Name),
            ComponentType = Enum.TryParse<ComponentType>(c.ComponentType, true, out var ct) ? ct : ComponentType.Mixed,
            Role = Enum.TryParse<ComponentRole>(c.Role, true, out var cr) ? cr : ComponentRole.CarbBase,
            Grams = c.Grams,
            IsLocked = c.IsLocked,
            Calories = c.Calories,
            Protein = c.Protein,
            Carbs = c.Carbs,
            Fats = c.Fats,
            CaloriesForAmount = NutritionPresentationMapper
                .FromPer100Grams(c.Calories, c.Protein, c.Carbs, c.Fats, c.Grams).Calories,
            NutritionForAmountText = NutritionPresentationMapper
                .FromPer100Grams(c.Calories, c.Protein, c.Carbs, c.Fats, c.Grams).NutritionText
        }).ToList();

        var groups = dto.ComponentGroups.Select(g => new MealSuggestionComponentGroup(
            g.ComponentType,
            UiTextCatalog.TranslateRuntimeText(g.Title),
            g.Items.Select(ci => new MealSuggestionComponentItem
            {
                FoodItemId = ci.FoodItemId,
                Name = UiTextCatalog.TranslateRuntimeText(ci.Name),
                ComponentType = Enum.TryParse<ComponentType>(ci.ComponentType, true, out var ct)
                    ? ct
                    : ComponentType.Mixed,
                Role = Enum.TryParse<ComponentRole>(ci.Role, true, out var cr) ? cr : ComponentRole.CarbBase,
                Grams = ci.Grams,
                IsLocked = ci.IsLocked,
                Calories = ci.Calories,
                Protein = ci.Protein,
                Carbs = ci.Carbs,
                Fats = ci.Fats,
                CaloriesForAmount = NutritionPresentationMapper
                    .FromPer100Grams(ci.Calories, ci.Protein, ci.Carbs, ci.Fats, ci.Grams).Calories,
                NutritionForAmountText = NutritionPresentationMapper
                    .FromPer100Grams(ci.Calories, ci.Protein, ci.Carbs, ci.Fats, ci.Grams).NutritionText
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
            Macros = NutritionPresentationMapper.FromTotals(dto.Calories, dto.Protein, dto.Carbs, dto.Fats),
            Score = dto.Score,
            Style = Enum.TryParse<MealStyle>(dto.Style, true, out var s) ? s : MealStyle.Plated,
            IconGlyph = dto.IconGlyph
        };
    }

    /// <summary>Raises the suggestion properties.</summary>
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
        OnPropertyChanged(nameof(DayPlanSummaryText));
        OnPropertyChanged(nameof(SuggestionMealTypeCaloriesText));
        OnPropertyChanged(nameof(BreakfastCaloriesHint));
        OnPropertyChanged(nameof(LunchCaloriesHint));
        OnPropertyChanged(nameof(DinnerCaloriesHint));
        OnPropertyChanged(nameof(SnackCaloriesHint));
        OnPropertyChanged(nameof(IsBreakfastSuggestion));
        OnPropertyChanged(nameof(ShowBreakfastPreferenceToggle));
        OnPropertyChanged(nameof(IsSavouryBreakfast));
        OnPropertyChanged(nameof(IsSweetBreakfast));
    }

    /// <summary>Raises the meal detail properties.</summary>
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
