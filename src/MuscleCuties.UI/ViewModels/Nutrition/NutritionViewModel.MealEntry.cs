using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

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

    public ObservableCollection<MealTemplate> MealTemplates { get; } = [];

    public bool IsEntryBreakfastSelected => EntryMealType == MealType.Breakfast;
    public bool IsEntryLunchSelected => EntryMealType == MealType.Lunch;
    public bool IsEntryDinnerSelected => EntryMealType == MealType.Dinner;
    public bool IsEntrySnackSelected => EntryMealType == MealType.Snack;
    public bool HasMealTemplates => MealTemplates.Count > 0;
    public bool HasMealTemplateStatus => !string.IsNullOrWhiteSpace(MealTemplateStatus);

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in Nutrition XAML / view model contract.")]
    public bool HasEntryMealTypeSelected => true;

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

    private void CloseMealEntry()
    {
        IsMealEntryModalVisible = false;
        IsTemplatePickerVisible = false;
        MealTemplateStatus = string.Empty;
    }

    private void SelectEntryMealType(MealType mealType)
    {
        if (IsWritingMealEntry)
            return;

        EntryMealType = mealType;
        NotifyEntryProperties();
    }

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

    private void ToggleTemplatePicker()
    {
        if (IsWritingMealEntry)
            return;

        IsTemplatePickerVisible = !IsTemplatePickerVisible;
        if (IsTemplatePickerVisible)
            _ = LoadMealTemplatesAsync();
    }

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

    private void NotifyMealTemplateState()
    {
        OnPropertyChanged(nameof(HasMealTemplates));
        OnPropertyChanged(nameof(HasMealTemplateStatus));
    }

    private void ShowPageStatus(string message)
    {
        var version = ++_pageStatusVersion;
        PageStatusMessage = message;
        IsPageStatusVisible = true;
        _ = HidePageStatusAsync(version);
    }

    private async Task HidePageStatusAsync(int version)
    {
        await Task.Delay(PageStatusDuration);
        if (version == _pageStatusVersion)
            IsPageStatusVisible = false;
    }

    private void NotifyEntryProperties()
    {
        OnPropertyChanged(nameof(IsEntryBreakfastSelected));
        OnPropertyChanged(nameof(IsEntryLunchSelected));
        OnPropertyChanged(nameof(IsEntryDinnerSelected));
        OnPropertyChanged(nameof(IsEntrySnackSelected));
    }

    partial void OnMealTemplateStatusChanged(string value) => OnPropertyChanged(nameof(HasMealTemplateStatus));
}
