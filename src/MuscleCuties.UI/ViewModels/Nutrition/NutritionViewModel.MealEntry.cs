// Add-meal chooser, meal templates, and meal-type state.
// Second half of NutritionViewModel; see its declaration for the full contract.

using System.Diagnostics.CodeAnalysis;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>The entry point into logging a meal: choose a meal type, start a manual meal, ideas or a day plan, and the</summary>
/// saved meal templates that can be logged in one tap.</summary>
public partial class NutritionViewModel
{
    private const float GramsPerHundred = 100f;
    private const float FallbackTemplateItemGrams = 100f;
    private const float SingleTemplateServing = 1f;
    private const string TemplateGramsUnit = "g";
    private static readonly TimeSpan PageStatusDuration = TimeSpan.FromSeconds(3);
    private int _pageStatusVersion;

    [ObservableProperty] private MealType _entryMealType;
    [ObservableProperty] private TimeSpan _entryMealTime;
    [ObservableProperty] private bool _isMealEntryModalVisible;
    [ObservableProperty] private bool _isTemplatePickerVisible;
    [ObservableProperty] private bool _isLoadingMealTemplates;
    [ObservableProperty] private bool _isWritingMealEntry;
    [ObservableProperty] private bool _isSavingMealTemplate;
    [ObservableProperty] private string _mealTemplateStatus = string.Empty;
    [ObservableProperty] private string _pageStatusMessage = string.Empty;
    [ObservableProperty] private bool _isPageStatusVisible;

    /// <summary>The user's meal templates.</summary>
    public ObservableCollection<MealTemplate> MealTemplates { get; } = [];

    /// <summary>True when breakfast is the entry's meal.</summary>
    public bool IsEntryBreakfastSelected => EntryMealType == MealType.Breakfast;
    /// <summary>True when lunch is the entry's meal.</summary>
    public bool IsEntryLunchSelected => EntryMealType == MealType.Lunch;
    /// <summary>True when dinner is the entry's meal.</summary>
    public bool IsEntryDinnerSelected => EntryMealType == MealType.Dinner;
    /// <summary>True when a snack is the entry's meal.</summary>
    public bool IsEntrySnackSelected => EntryMealType == MealType.Snack;
    /// <summary>True when there are meal templates.</summary>
    public bool HasMealTemplates => MealTemplates.Count > 0;
    /// <summary>True when a template status is shown.</summary>
    public bool HasMealTemplateStatus => !string.IsNullOrWhiteSpace(MealTemplateStatus);

    /// <summary>Always true, as a meal type is always chosen.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in Nutrition XAML / view model contract.")]
    public bool HasEntryMealTypeSelected => true;

    /// <summary>Opens the meal chooser and its editor together, so the page never flashes between two sheets.</summary>
    private void OpenMealEntry()
    {
        if (IsBusy)
            return;

        IsEditingMeal = false;
        ResetMealDraft();
        SelectedMealType = GuessNextMealType();
        SelectedMealTime = DateTime.Now.TimeOfDay;
        EntryMealType = SelectedMealType;
        EntryMealTime = SelectedMealTime;
        IsMealChoiceLocked = false;
        IsMealEditorVisible = false;
        IsMealEntryModalVisible = true;
        IsTemplatePickerVisible = false;
        MealTemplateStatus = string.Empty;
        NotifyEntryProperties();
        NotifyMealIngredientProperties();
        _ = LoadMealTemplatesAsync();
    }

    /// <summary>Closes the chooser and forgets which entry the user was on.</summary>
    private void CloseMealEntry()
    {
        IsMealEntryModalVisible = false;
        IsTemplatePickerVisible = false;
        MealTemplateStatus = string.Empty;
    }

    /// <summary>Records the meal type chosen in the entry sheet.</summary>
    private void SelectEntryMealType(MealType mealType)
    {
        if (IsWritingMealEntry)
            return;

        EntryMealType = mealType;
        NotifyEntryProperties();
    }

    /// <summary>Opens an empty editor for the chosen meal, defaulting the type to the one the clock suggests when none was</summary>
    /// picked. Returns without acting while a write is in flight.</summary>
    private Task StartManualMealAsync()
    {
        if (IsBusy || IsWritingMealEntry)
            return Task.CompletedTask;

        IsMealChoiceLocked = false;
        // Open the editor behind the chooser. NutritionPage dismisses the chooser only after the
        // editor host is loaded and fully presented, so the page cannot flash between two sheets.
        IsMealEditorVisible = true;
        PrepareMealEditor();
        SelectedMealType = Enum.IsDefined(EntryMealType) ? EntryMealType : GuessNextMealType();
        SelectedMealTime = EntryMealTime;
        NotifyEntryProperties();
        NotifyMealIngredientProperties();
        return Task.CompletedTask;
    }

    /// <summary>Opens the ideas sheet for the chosen meal and loads the first page of suggestions, remembering whether to</summary>
    /// return to the editor afterwards.</summary>
    private void StartSuggestedMeal()
    {
        if (IsBusy || IsWritingMealEntry)
            return;

        if (IsMealEntryModalVisible)
        {
            SelectedMealType = Enum.IsDefined(EntryMealType) ? EntryMealType : GuessNextMealType();
            SelectedMealTime = EntryMealTime;
        }

        _returnToMealEditorAfterSuggestion = IsMealEditorVisible;
        _suggestionMealTime = SelectedMealTime;
        SuggestionMealType = SelectedMealType;
        EntryMealType = SelectedMealType;
        EntryMealTime = SelectedMealTime;
        IsDayPlanMode = false;
        IsMealEntryModalVisible = false;
        IsMealEditorVisible = false;
        IsMealTypePickerVisible = false;
        IsLoadingSuggestions = false;
        IsMealDetailVisible = false;
        SelectedMealDetail = null;
        MealSuggestions.Clear();
        _shownConceptNames.Clear();
        MealSuggestionError = string.Empty;
        SuggestionTargetText =
            MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Nutrition_Suggestions_TargetsTitle");
        IsSuggestionModalVisible = true;
        NotifySuggestionProperties();
        _ = LoadSuggestionsAsync();
    }

    /// <summary>Opens the day-plan variant of the sheet, which plans every meal rather than one.</summary>
    private void StartDayPlan()
    {
        _returnToMealEditorAfterSuggestion = false;
        _suggestionMealTime = null;
        IsMealEntryModalVisible = false;
        IsMealEditorVisible = false;
        IsMealChoiceLocked = false;
        IsDayPlanMode = true;
        IsMealTypePickerVisible = false;
        IsLoadingSuggestions = false;
        IsMealDetailVisible = false;
        SelectedMealDetail = null;
        MealSuggestions.Clear();
        DayPlanMeals.Clear();
        _shownConceptNames.Clear();
        MealSuggestionError = string.Empty;
        DayPlanSummaryText = "Ui_Nutrition_DayPlan_Preparing";
        IsSuggestionModalVisible = true;
        NotifySuggestionProperties();
        _ = LoadDayPlanAsync();
    }

    /// <summary>Shows or hides the template list, loading it the first time it is opened.</summary>
    private void ToggleTemplatePicker()
    {
        if (IsWritingMealEntry)
            return;

        IsTemplatePickerVisible = !IsTemplatePickerVisible;
        if (IsTemplatePickerVisible)
            _ = LoadMealTemplatesAsync();
    }

    /// <summary>Loads the saved templates; without the use case the list says templates are unavailable rather than</summary>
    /// appearing empty.</summary>
    private async Task LoadMealTemplatesAsync()
    {
        if (IsLoadingMealTemplates)
            return;

        if (_getRecipesAndTemplatesUseCase is null)
        {
            MealTemplates.Clear();
            MealTemplateStatus = UiTextCatalog.Get("Nutrition_MealEntry_TemplatesUnavailable");
            NotifyMealTemplateState();
            return;
        }

        IsLoadingMealTemplates = true;
        MealTemplateStatus = string.Empty;
        try
        {
            var bundle = await _getRecipesAndTemplatesUseCase.ExecuteAsync();
            MealTemplates.Clear();
            foreach (var template in bundle.Templates)
                MealTemplates.Add(NutritionPresentationMapper.FromMealTemplate(template));

            MealTemplateStatus = MealTemplates.Count == 0
                ? UiTextCatalog.Get("Nutrition_MealEntry_NoTemplates")
                : string.Empty;
        }
        catch
        {
            MealTemplateStatus = UiTextCatalog.Get("Nutrition_MealEntry_TemplatesUnavailable");
        }
        finally
        {
            IsLoadingMealTemplates = false;
            NotifyMealTemplateState();
        }
    }

    /// <summary>Logs a template as a meal in one step, reporting a failure on the entry sheet.</summary>
    private async Task LogMealTemplateAsync(MealTemplate? template)
    {
        if (template is null || IsWritingMealEntry || IsLoadingMealTemplates)
            return;

        var mealType = Enum.IsDefined(EntryMealType) ? EntryMealType : GuessNextMealType();
        var ingredients = template.Items.Select(NutritionPresentationMapper.ToTemplateMealIngredient).ToList();
        if (ingredients.Count == 0)
        {
            MealTemplateStatus = UiTextCatalog.Get("Nutrition_MealEntry_TemplateEmpty");
            return;
        }

        IsWritingMealEntry = true;
        MealTemplateStatus = string.Empty;
        try
        {
            await _overviewUseCase.LogMealAsync(new LogMealRequest(
                DateOnly.FromDateTime(DateTime.Today),
                mealType.ToString(),
                EntryMealTime,
                ingredients,
                template.Name));

            IsMealEntryModalVisible = false;
            IsTemplatePickerVisible = false;
            ShowPageStatus(UiTextCatalog.Format(
                "Nutrition_MealEntry_TemplateLoggedFormat",
                template.Name,
                UiTextCatalog.Get($"Nutrition_MealType_{mealType}")));

            await _loadGate.RunAsync(LoadDataCoreAsync, this, true);
        }
        catch (InvalidOperationException)
        {
            MealTemplateStatus = UiTextCatalog.Get("Nutrition_MealEntry_TemplateLogFailed");
        }
        catch (ArgumentException)
        {
            MealTemplateStatus = UiTextCatalog.Get("Nutrition_MealEntry_TemplateLogFailed");
        }
        catch
        {
            MealTemplateStatus = UiTextCatalog.Get("Nutrition_MealEntry_TemplateLogFailed");
        }
        finally
        {
            IsWritingMealEntry = false;
        }
    }

    /// <summary>Deletes a template and refreshes the list.</summary>
    private async Task DeleteMealTemplateAsync(MealTemplate? template)
    {
        if (template is null || IsLoadingMealTemplates || _getRecipesAndTemplatesUseCase is null)
            return;

        var confirmed = await Shell.Current.DisplayAlertAsync(
            UiTextCatalog.Get("Nutrition_Template_DeleteTitle"),
            UiTextCatalog.Format("Nutrition_Template_DeleteMessage", template.Name),
            UiTextCatalog.Get("Common_Delete"),
            UiTextCatalog.Get("Common_Cancel"));
        if (!confirmed)
            return;

        IsLoadingMealTemplates = true;
        try
        {
            await _getRecipesAndTemplatesUseCase.DeleteMealTemplateAsync(template.Id);
            MealTemplates.Remove(template);
            MealTemplateStatus = MealTemplates.Count == 0
                ? UiTextCatalog.Get("Nutrition_MealEntry_NoTemplates")
                : UiTextCatalog.Get("Nutrition_Template_Deleted");
        }
        catch
        {
            MealTemplateStatus = UiTextCatalog.Get("Nutrition_Template_DeleteFailed");
        }
        finally
        {
            IsLoadingMealTemplates = false;
            NotifyMealTemplateState();
        }
    }

    /// <summary>Saves a logged meal as a reusable template under its own name.</summary>
    private async Task SaveMealAsTemplateAsync(MealItem? meal)
    {
        if (meal is null || IsSavingMealTemplate)
            return;

        if (_saveMealTemplateUseCase is null)
        {
            ShowPageStatus(UiTextCatalog.Get("Nutrition_Template_SaveFailed"));
            return;
        }

        IsSavingMealTemplate = true;
        try
        {
            var mealType = Enum.TryParse<MealType>(meal.MealType, true, out var parsed)
                ? parsed
                : MealType.Snack;
            var items = await BuildMealTemplateItemsAsync(meal);
            var templateName = UiTextCatalog.Format(
                "Nutrition_Template_NameFormat",
                UiTextCatalog.Get($"Nutrition_MealType_{mealType}"),
                DateTime.Today);
            var saved = await _saveMealTemplateUseCase.ExecuteAsync(
                NutritionPresentationMapper.CreateSaveMealTemplateRequest(templateName, (int)mealType, items));
            var template = NutritionPresentationMapper.FromMealTemplate(saved);

            if (MealTemplates.All(existing => existing.Id != template.Id))
                MealTemplates.Insert(0, template);
            NotifyMealTemplateState();
            ShowPageStatus(UiTextCatalog.Format("Nutrition_Template_SavedFormat", templateName));
        }
        catch
        {
            ShowPageStatus(UiTextCatalog.Get("Nutrition_Template_SaveFailed"));
        }
        finally
        {
            IsSavingMealTemplate = false;
        }
    }

    /// <summary>The template rows a meal would become, one per ingredient, with gram weights worked out from the serving</summary>
    /// the user chose.</summary>
    private async Task<IReadOnlyList<MealTemplateItem>> BuildMealTemplateItemsAsync(MealItem meal)
    {
        if (meal.LoggedMealId > 0)
        {
            try
            {
                var ingredients = await _overviewUseCase.GetMealIngredientsAsync(meal.LoggedMealId);
                if (ingredients.Count > 0)
                {
                    return ingredients.Select(ingredient =>
                    {
                        var grams = ingredient.Grams > 0 ? ingredient.Grams : ingredient.Amount;
                        if (grams <= 0)
                            grams = FallbackTemplateItemGrams;
                        var factor = grams / GramsPerHundred;
                        return new MealTemplateItem(
                            Guid.NewGuid(),
                            ingredient.Name,
                            grams,
                            TemplateGramsUnit,
                            SingleTemplateServing,
                            ingredient.Calories * factor,
                            ingredient.Protein * factor,
                            ingredient.Carbs * factor,
                            ingredient.Fats * factor);
                    }).ToList();
                }
            }
            catch
            {
                // A saved meal can still become a template from its card totals when
                // ingredient retrieval is temporarily unavailable.
            }
        }

        return
        [
            new MealTemplateItem(
                Guid.NewGuid(),
                meal.Name,
                FallbackTemplateItemGrams,
                TemplateGramsUnit,
                SingleTemplateServing,
                meal.Macros.Calories,
                meal.Macros.Protein,
                meal.Macros.Carbs,
                meal.Macros.Fats)
        ];
    }

    /// <summary>Re-publishes the template list's visibility and status after it changes.</summary>
    private void NotifyMealTemplateState()
    {
        OnPropertyChanged(nameof(HasMealTemplates));
        OnPropertyChanged(nameof(HasMealTemplateStatus));
    }

    /// <summary>Shows a transient status line; a newer message supersedes the older one's timer.</summary>
    private void ShowPageStatus(string message)
    {
        var version = ++_pageStatusVersion;
        PageStatusMessage = message;
        IsPageStatusVisible = true;
        _ = HidePageStatusAsync(version);
    }

    /// <summary>Hides the status line after its delay unless a newer message has replaced it.</summary>
    private async Task HidePageStatusAsync(int version)
    {
        await Task.Delay(PageStatusDuration);
        if (version == _pageStatusVersion)
            IsPageStatusVisible = false;
    }

    /// <summary>Raises the entry properties the page binds.</summary>
    private void NotifyEntryProperties()
    {
        OnPropertyChanged(nameof(IsEntryBreakfastSelected));
        OnPropertyChanged(nameof(IsEntryLunchSelected));
        OnPropertyChanged(nameof(IsEntryDinnerSelected));
        OnPropertyChanged(nameof(IsEntrySnackSelected));
    }

    /// <summary>Refreshes the status flag when the text changes.</summary>
    partial void OnMealTemplateStatusChanged(string value) => OnPropertyChanged(nameof(HasMealTemplateStatus));
}
