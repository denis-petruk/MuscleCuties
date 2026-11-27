using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.UI.Navigation;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>The meal draft: its ingredients, the food search behind them, logging or updating the meal, and the</summary>
/// breakfast preference that decides its share of the day.</summary>
public partial class NutritionViewModel
{
    private string? _acceptedSuggestionName;
    private CyclePhase? _acceptedSuggestionPhase;
    private MealIngredientItem? _editingIngredient;
    private DateTime _editingMealDate;

    /// <summary>Loads an ingredient's amount back into the amount field so it can be changed and re-added.</summary>
    private void EditIngredient(MealIngredientItem? ingredient)
    {
        if (ingredient is null || !MealIngredients.Contains(ingredient) || IsBusy)
            return;

        _editingIngredient = ingredient;
        ResetFoodSource();
        IsCustomFoodPanelVisible = false;
        IsFoodFinderExpanded = false;
        SelectedFoodResult = CreateFoodSearchResultItem(
            ingredient.FoodItemId,
            ingredient.Name,
            ingredient.Calories,
            ingredient.Protein,
            ingredient.Carbs,
            ingredient.Fats,
            ingredient.SourceSummary
        );
        SelectedServingOption = ServingOptions.FirstOrDefault(option => option.Label == "g")
                                ?? new FoodServingOptionItem { Label = "g", Unit = "g", Grams = 1f };
        FoodGrams = FormatAmountInput(ingredient.Grams);
        AddFoodMessage = string.Empty;
    }

    /// <summary>Sets which meal the draft belongs to and re-evaluates whether it can be saved.</summary>
    private void SelectMealType(MealType mealType)
    {
        if (!IsMealChoiceLocked)
            SelectedMealType = mealType;
    }

    /// <summary>Reloads the meal when the selected meal changes.</summary>
    partial void OnSelectedMealTypeChanged(MealType value)
    {
        OnPropertyChanged(nameof(IsBreakfastSelected));
        OnPropertyChanged(nameof(IsLunchSelected));
        OnPropertyChanged(nameof(IsDinnerSelected));
        OnPropertyChanged(nameof(IsSnackSelected));
    }

    /// <summary>Adds the chosen food at the entered amount, converting the amount to grams when the serving has a weight</summary>
    /// and refusing an amount that cannot be read.</summary>
    private void AddSelectedFoodAsIngredient()
    {
        if (SelectedFoodResult is null)
        {
            AddFoodMessage = "Ui_Nutrition_Error_ChooseIngredient";
            return;
        }

        if (SelectedServingOption is null)
        {
            AddFoodMessage = "Ui_Nutrition_Error_ChooseServing";
            return;
        }

        if (!TryParseAmount(FoodGrams, out var amount))
        {
            AddFoodMessage = "Ui_Nutrition_Error_AmountPositive";
            return;
        }

        if (SelectedFoodResult.Calories <= 0f)
        {
            AddFoodMessage = "Ui_Nutrition_Error_ProductCalories";
            return;
        }

        var ingredient = CreateIngredient(SelectedFoodResult, amount, SelectedServingOption);
        var index = _editingIngredient is null ? -1 : MealIngredients.IndexOf(_editingIngredient);
        if (index >= 0)
            MealIngredients[index] = ingredient;
        else
            AddOrUpdateIngredient(ingredient);

        _editingIngredient = null;
        AddFoodMessage = string.Empty;
        ResetFoodSearchAndSelection();
    }

    /// <summary>Drops an ingredient from the draft and re-evaluates the save state.</summary>
    private void RemoveIngredient(MealIngredientItem? ingredient)
    {
        if (ingredient is null)
            return;

        MealIngredients.Remove(ingredient);
        if (MealIngredients.Count == 0)
            IsFoodFinderExpanded = false;
    }

    /// <summary>Saves the draft as an update or a new log depending on how the editor was opened, then reloads the day.</summary>
    /// Rejected selections and amounts become messages on the editor rather than failures of the page.</summary>
    private async Task LogMealAsync()
    {
        if (MealIngredients.Count == 0)
        {
            AddFoodMessage = "Ui_Nutrition_Error_AddIngredient";
            return;
        }

        IsBusy = true;
        try
        {
            var date = IsEditingMeal ? DateOnly.FromDateTime(_editingMealDate) : DateOnly.FromDateTime(DateTime.Today);
            var ingredients = MealIngredients
                .Select(i => new NutritionMealIngredient(
                    i.FoodItemId,
                    i.Name,
                    i.Grams,
                    i.Amount,
                    i.ServingLabel,
                    i.Calories,
                    i.Protein,
                    i.Carbs,
                    i.Fats,
                    i.SourceSummary))
                .ToList();

            if (IsEditingMeal)
            {
                await _overviewUseCase.UpdateMealAsync(new UpdateMealRequest(
                    _editingMealId,
                    date,
                    SelectedMealType.ToString(),
                    SelectedMealTime,
                    NutritionPresentationMapper.ToMealIngredients(ingredients)));
                AddFoodMessage = $"{SelectedMealType} updated at {SelectedMealTime:hh\\:mm}.";
            }
            else
            {
                await _overviewUseCase.LogMealAsync(new LogMealRequest(
                    date,
                    SelectedMealType.ToString(),
                    SelectedMealTime,
                    NutritionPresentationMapper.ToMealIngredients(ingredients),
                    _acceptedSuggestionName,
                    _acceptedSuggestionPhase?.ToString()));
                AddFoodMessage = $"{SelectedMealType} logged at {SelectedMealTime:hh\\:mm}.";
            }

            TriggerCelebration();
            IsMealEditorVisible = false;
            IsCustomFoodPanelVisible = false;
            SelectedFoodResult = null;
            IsFoodFinderExpanded = false;
            SearchQuery = string.Empty;
            FoodSearchResults = [];
            ResetFoodSearchPaging();
            FoodGrams = string.Empty;
            MealIngredients.Clear();
            IsEditingMeal = false;
            _editingMealId = 0;
            _acceptedSuggestionName = null;
            _acceptedSuggestionPhase = null;

            _loadGate.MarkStale();
            await _loadGate.RunAsync(LoadDataCoreAsync, this, true);
        }
        catch (InvalidOperationException)
        {
            AddFoodMessage = "Ui_Nutrition_Error_CheckMealSelections";
        }
        catch (ArgumentException)
        {
            AddFoodMessage = "Ui_Nutrition_Error_CheckMealAmounts";
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[NutritionViewModel] Meal log failed ({ex.GetType().Name}).");
            AddFoodMessage = "Ui_Nutrition_Error_LogMeal";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Opens a logged meal for editing with its ingredients loaded; a meal whose ingredients cannot be read is</summary>
    /// reported instead of opening an empty editor.</summary>
    private async Task EditMealAsync(MealItem? meal)
    {
        if (meal is null)
            return;

        if (meal.LoggedMealId == 0 && _navigateAsync is not null)
        {
            await _navigateAsync(NutritionNavigation.ToLogEntry(meal.HistoryId));
            return;
        }

        IsBusy = true;
        try
        {
            var ingredients = await _overviewUseCase.GetMealIngredientsAsync(meal.LoggedMealId);
            if (ingredients.Count == 0)
            {
                AddFoodMessage = "Ui_Nutrition_Error_MealNotFound";
                return;
            }

            BeginMealEdit(meal, ingredients.Select(NutritionPresentationMapper.ToMealIngredient).ToList());
        }
        catch (InvalidOperationException)
        {
            AddFoodMessage = "This meal could not be opened for editing. Refresh and try again.";
        }
        catch (ArgumentException)
        {
            AddFoodMessage = "This meal could not be opened for editing. Refresh and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private int _breakfastPreferenceVersion;
    private bool _breakfastTargetsNeedRefresh;

    /// <summary>Applies a breakfast preference and reloads the targets it changes, ignoring a response that arrives after</summary>
    /// the preference has been changed again.</summary>
    private async Task SetBreakfastPreferenceAsync(string? preference)
    {
        if (!Enum.TryParse<BreakfastPreference>(preference, true, out var parsed) ||
            !Enum.IsDefined(parsed))
            return;

        BreakfastPreference = parsed;

        OnPropertyChanged(nameof(IsSavouryBreakfast));
        OnPropertyChanged(nameof(IsSweetBreakfast));

        await RefreshBreakfastPreferenceAsync();
    }

    /// <summary>Reloads the targets for the saved breakfast preference and marks the pending refresh done; a failure keeps</summary>
    /// the previous targets and only surfaces when the suggestion sheet was waiting on it.</summary>
    private async Task RefreshBreakfastPreferenceAsync()
    {
        var preferenceVersion = ++_breakfastPreferenceVersion;
        var selectedPreference = BreakfastPreference;
        var suggestionVersion = ++_suggestionLoadVersion;
        _breakfastTargetsNeedRefresh = true;
        var refreshModal = IsSuggestionModalVisible && (IsDayPlanMode || IsBreakfastSuggestion);
        if (refreshModal)
        {
            IsLoadingSuggestions = true;
            MealSuggestionError = string.Empty;
            MealSuggestions.Clear();
            DayPlanMeals.Clear();
            NotifySuggestionProperties();
        }

        try
        {
            var targets = await _overviewUseCase.GetPlanTargetsAsync(selectedPreference.ToString());
            if (preferenceVersion != _breakfastPreferenceVersion)
                return;

            _mealDistribution = NutritionPresentationMapper.FromMealDistribution(targets.MealDistribution);
            TargetCalories = targets.Calories;
            TargetProtein = targets.Protein;
            TargetCarbs = targets.Carbs;
            TargetFats = targets.Fats;
            NotifyDisplayProperties();
            NotifyMealTargetProperties();
            NotifySuggestionProperties();
            _breakfastTargetsNeedRefresh = false;

            if (suggestionVersion == _suggestionLoadVersion && IsSuggestionModalVisible)
            {
                if (IsDayPlanMode)
                {
                    await LoadDayPlanAsync();
                }
                else if (IsBreakfastSuggestion)
                {
                    await LoadSuggestionsAsync();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[NutritionViewModel] Breakfast targets failed ({ex.GetType().Name}).");
            if (preferenceVersion == _breakfastPreferenceVersion && suggestionVersion == _suggestionLoadVersion && refreshModal)
            {
                MealSuggestionError = "Meal ideas could not refresh. Check your connection and try again.";
                IsLoadingSuggestions = false;
                NotifySuggestionProperties();
            }
        }
    }

    /// <summary>Raises the meal target properties the page binds.</summary>
    private void NotifyMealTargetProperties()
    {
        OnPropertyChanged(nameof(BreakfastTargetText));
        OnPropertyChanged(nameof(LunchTargetText));
        OnPropertyChanged(nameof(DinnerTargetText));
        OnPropertyChanged(nameof(SnackTargetText));
    }

    /// <summary>Fills the editor from an existing meal, which is what makes saving update it rather than add another.</summary>
    private void BeginMealEdit(MealItem meal, IReadOnlyList<NutritionMealIngredient> ingredients)
    {
        MealIngredients = new ObservableCollection<MealIngredientItem>(ingredients.Select(i => new MealIngredientItem
        {
            FoodItemId = i.FoodItemId,
            Name = i.Name,
            Calories = i.Calories,
            Protein = i.Protein,
            Carbs = i.Carbs,
            Fats = i.Fats,
            SourceSummary = i.SourceSummary,
            Grams = i.Grams,
            Amount = i.Amount,
            ServingLabel = i.ServingLabel,
            MacrosForAmount = NutritionPresentationMapper.FromPer100Grams(
                i.Calories, i.Protein, i.Carbs, i.Fats, i.Grams)
        }));

        _editingMealDate = DateTime.Today;
        _editingMealId = meal.LoggedMealId;
        IsEditingMeal = true;
        IsMealChoiceLocked = true;
        IsMealContextExpanded = false;
        SelectedMealType = Enum.TryParse<MealType>(meal.MealType, true, out var mt) ? mt : MealType.Snack;
        SelectedMealTime = DateTime.TryParse(meal.Time, out var t) ? t.TimeOfDay : DateTime.Now.TimeOfDay;
        IsMealEditorVisible = true;
        ResetFoodSearchAndSelection();
        AddFoodMessage = string.Empty;
        NotifyMealIngredientProperties();
    }

    /// <summary>Empties the draft — ingredients, amount, search results and edit identity — before a new meal.</summary>
    private void ResetMealDraft()
    {
        _acceptedSuggestionName = null;
        _acceptedSuggestionPhase = null;
        InvalidateFoodSearch();
        _editingIngredient = null;
        _editingMealId = 0;
        IsEditingMeal = false;
        IsMealContextExpanded = false;
        ResetFoodSearchAndSelection();
        IsCustomFoodPanelVisible = false;
        MealIngredients.Clear();
        AddFoodMessage = string.Empty;
        NotifyMealIngredientProperties();
    }

    /// <summary>Adds an ingredient, or replaces the existing one for the same food so a food cannot appear twice.</summary>
    private void AddOrUpdateIngredient(MealIngredientItem ingredient)
    {
        var existing = MealIngredients.FirstOrDefault(i => i.FoodItemId == ingredient.FoodItemId);
        if (existing is null)
        {
            MealIngredients.Add(ingredient);
            return;
        }

        var index = MealIngredients.IndexOf(existing);
        MealIngredients[index] = CreateIngredient(existing, existing.Grams + ingredient.Grams);
        NotifyMealIngredientProperties();
    }

    /// <summary>Clears the food search, its paging and the chosen food, returning the finder to its first page.</summary>
    private void ResetFoodSearchAndSelection()
    {
        ResetFoodSource();
        SelectedFoodResult = null;
        SearchQuery = string.Empty;
        ServingOptions = [];
        SelectedServingOption = null;
        FoodGrams = string.Empty;
        FoodSearchResults = [];
        ResetFoodSearchPaging();
        IsFoodFinderExpanded = false;
    }

    /// <summary>One ingredient for the meal being built.</summary>
    private static MealIngredientItem BuildMealIngredient(
        int foodId, string name, float calories, float protein, float carbs, float fats, string sourceSummary,
        float grams, float amount, string servingLabel)
    {
        return new MealIngredientItem
        {
            FoodItemId = foodId,
            Name = name,
            Calories = calories,
            Protein = protein,
            Carbs = carbs,
            Fats = fats,
            SourceSummary = sourceSummary,
            Grams = grams,
            Amount = amount,
            ServingLabel = servingLabel,
            MacrosForAmount = NutritionPresentationMapper.FromPer100Grams(
                calories, protein, carbs, fats, grams)
        };
    }

    /// <summary>The ingredient row for a food and amount.</summary>
    private static MealIngredientItem CreateIngredient(
        FoodSearchResultItem food,
        float amount,
        FoodServingOptionItem servingOption)
    {
        return BuildMealIngredient(food.FoodItemId, food.Name, food.Calories, food.Protein, food.Carbs, food.Fats,
            food.SourceSummary,
            amount * servingOption.Grams, amount, servingOption.Label);
    }

    /// <summary>A copy of an ingredient at a different weight, with its macros scaled to match.</summary>
    private static MealIngredientItem CreateIngredient(MealIngredientItem ingredient, float grams)
    {
        return BuildMealIngredient(ingredient.FoodItemId, ingredient.Name, ingredient.Calories, ingredient.Protein,
            ingredient.Carbs, ingredient.Fats, ingredient.SourceSummary,
            grams, grams, "g");
    }

    /// <summary>D14: the commit's own enabled state, exposed so the button and its hint agree.</summary>
    public bool CanSaveMeal => CanLogMeal();

    /// <summary>True when the log hint should be shown.</summary>
    public bool ShowLogMealHint => IsFoodFinderCollapsed && !CanSaveMeal;

    /// <summary>What the log button is waiting for: a hint to add food while the draft is empty, or what logging will</summary>
    /// do once it is not.</summary>
    public string LogMealHint => CanSaveMeal
        ? UiTextCatalog.Get("Nutrition_MealEditor_LogHint")
        : UiTextCatalog.Get("Nutrition_MealEditor_AddFoodPrerequisiteHint");

    /// <summary>Raises the ingredient summary properties.</summary>
    private void NotifyMealIngredientProperties()
    {
        OnPropertyChanged(nameof(HasMealIngredients));
        OnPropertyChanged(nameof(CanSaveMeal));
        OnPropertyChanged(nameof(ShowLogMealHint));
        OnPropertyChanged(nameof(LogMealHint));
        NotifyFoodFinderProperties();
        OnPropertyChanged(nameof(MealIngredientsTotalText));
        OnPropertyChanged(nameof(MealDraftCaloriesText));
        OnPropertyChanged(nameof(MealDraftProteinText));
        OnPropertyChanged(nameof(MealDraftCarbsText));
        OnPropertyChanged(nameof(MealDraftFatsText));
        OnPropertyChanged(nameof(SelectedMealDateText));
        OnPropertyChanged(nameof(SelectedMealTypeText));
        OnPropertyChanged(nameof(SelectedMealTimeText));
        OnPropertyChanged(nameof(LogMealButtonText));
        LogMealCommand.NotifyCanExecuteChanged();
    }

    /// <summary>An ingredient can be added once a food is chosen and the page is idle.</summary>
    private bool CanAddIngredient()
    {
        return !IsBusy &&
               SelectedFoodResult is not null &&
               SelectedServingOption is not null &&
               HasCalories(SelectedFoodResult.Calories) &&
               TryParseAmount(FoodGrams, out _);
    }

    /// <summary>The draft can be saved once it has at least one ingredient and the page is idle.</summary>
    private bool CanLogMeal()
    {
        return !IsBusy && MealIngredients.Count > 0;
    }
}
