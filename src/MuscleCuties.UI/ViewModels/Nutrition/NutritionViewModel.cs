using System.Diagnostics.CodeAnalysis;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.Navigation;
using MuscleCuties.UI.Services.Dashboard;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>The nutrition tab: the day's totals and targets, the logged meals, and the meal editor with its food</summary>
/// finder. Partial across files by surface; this one holds the shared state and the day load.</summary>
public partial class NutritionViewModel : ObservableObject, IPageLoadAware, IDisposable
{
    private readonly INutritionChangeNotifier? _changeNotifier;
    private readonly AppPageLoadGate _loadGate = new(AppPageLoadGate.PageFreshnessWindow);
    private readonly Func<string, Task>? _navigateAsync;
    private readonly INutritionOverviewUseCase _overviewUseCase;
    private readonly IFoodCatalogSearchUseCase _searchUseCase;
    private readonly IRecipePortionUseCase _recipePortionUseCase;
    private readonly FoodPortionCatalog _foodPortionCatalog;
    private readonly ISaveMealTemplateUseCase? _saveMealTemplateUseCase;
    private readonly IGetRecipesAndTemplatesUseCase? _getRecipesAndTemplatesUseCase;
    private readonly IHealthSyncService? _healthSync;
    private readonly ICurrentProfileUserIdProvider? _userIdProvider;
    private readonly IProfilePersonalInfoUseCase? _personalInfoUseCase;
    private NutritionMealDistribution? _mealDistribution;
    [ObservableProperty] private string _addFoodMessage = string.Empty;
    [ObservableProperty] private BreakfastPreference _breakfastPreference = BreakfastPreference.Savoury;
    [ObservableProperty] private string _celebrationIconSource = CyclePhaseAssets.FollicularAnimation;
    [ObservableProperty] private int _celebrationToken;
    [ObservableProperty] private float _consumedCalories;
    [ObservableProperty] private float _consumedCarbs;
    [ObservableProperty] private float _consumedFats;
    [ObservableProperty] private float _consumedProtein;
    [ObservableProperty] private CyclePhase _currentPhase = CyclePhase.Unknown;
    [ObservableProperty] private string _currentPhaseName = string.Empty;
    [ObservableProperty] private string _customFoodCalories = string.Empty;
    [ObservableProperty] private string _customFoodCalcium = string.Empty;
    [ObservableProperty] private string _customFoodCarbs = string.Empty;
    [ObservableProperty] private string _customFoodCompanySource = string.Empty;
    [ObservableProperty] private string _customFoodFats = string.Empty;
    [ObservableProperty] private string _customFoodIron = string.Empty;
    [ObservableProperty] private string _customFoodName = string.Empty;
    [ObservableProperty] private string _customFoodProtein = string.Empty;
    [ObservableProperty] private string _customFoodServingAmount = "100";
    [ObservableProperty] private string _customFoodVitaminB12 = string.Empty;
    [ObservableProperty] private string _customFoodVitaminD = string.Empty;
    private int _editingMealId;
    [ObservableProperty] private string _foodGrams = "100";
    [ObservableProperty] private ObservableCollection<FoodSearchResultItem> _foodSearchResults = new();
    [ObservableProperty] private bool _hasMoreFoodResults;
    private bool _isApplyingServingDefaults;
    [ObservableProperty] private bool _isBreakdownModalVisible;
    [ObservableProperty] private bool _isBrowsingMoreFoods;

    // Suppresses per-field On*Changed storms during batch macro/target assignment.
    private bool _isBulkLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;

    [ObservableProperty] private bool _isCustomFoodPanelVisible;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBarcodeSourceVisible), nameof(IsIngredientSearchVisible),
        nameof(MealEditorTitle))]
    private MealFoodSource _selectedFoodSource = MealFoodSource.Search;

    /// <summary>The tab for searching foods.</summary>
    public MealFoodSourceTab SearchFoodSourceTab { get; } =
        new(MealFoodSource.Search) { IsSelected = true };
    /// <summary>The tab for scanning a barcode.</summary>
    public MealFoodSourceTab BarcodeFoodSourceTab { get; } = new(MealFoodSource.Barcode);
    /// <summary>The tab for a custom food.</summary>
    public MealFoodSourceTab CustomFoodSourceTab { get; } = new(MealFoodSource.Custom);

    [ObservableProperty] private bool _isEditingMeal;
    [ObservableProperty] private bool _isFoodFinderExpanded;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isMealEditorVisible;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMealContextEditorVisible), nameof(IsMealContextSummaryVisible),
        nameof(CanChangeMealContext))]
    private bool _isMealChoiceLocked;

    // RM03/NM01: disclosure is independent of the contractual meal-context lock. A new or manual
    // meal starts compact; expanding is an explicit action, and a locked context never expands.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMealContextEditorVisible), nameof(IsMealContextSummaryVisible))]
    private bool _isMealContextExpanded;
    [ObservableProperty] private ObservableCollection<MealIngredientItem> _mealIngredients = new();
    [ObservableProperty] private ObservableCollection<MealItem> _meals = new();
    [ObservableProperty] private ObservableCollection<DailyMicronutrientItem> _micronutrients = new();
    [ObservableProperty] private string _phaseFocusCopy = string.Empty;
    [ObservableProperty] private string _phaseFocusTitle = string.Empty;
    [ObservableProperty] private string _phaseAvailability = string.Empty;
    [ObservableProperty] private string _targetSource = string.Empty;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _selectedBreakdownCoverageText = string.Empty;
    [ObservableProperty] private string _selectedCustomFoodServingUnit = "g";
    [ObservableProperty] private FoodSearchResultItem? _selectedFoodResult;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MealChoiceSummary), nameof(SelectedMealTimeText))]
    private TimeSpan _selectedMealTime = DateTime.Now.TimeOfDay;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MealChoiceSummary), nameof(SelectedMealTypeText))]
    private MealType _selectedMealType = MealType.Snack;
    [ObservableProperty] private FoodServingOptionItem? _selectedServingOption;
    [ObservableProperty] private ObservableCollection<FoodServingOptionItem> _servingOptions = new();

    [ObservableProperty] private float _targetCalories;
    [ObservableProperty] private float _targetCarbs;
    [ObservableProperty] private float _targetFats;
    [ObservableProperty] private float _targetProtein;

    /// <summary>Takes the use cases and callbacks the nutrition page reports to.</summary>
    public NutritionViewModel(
        INutritionOverviewUseCase overviewUseCase,
        IFoodCatalogSearchUseCase searchUseCase,
        IRecipePortionUseCase recipePortionUseCase,
        Func<string, Task>? navigateAsync = null,
        INutritionChangeNotifier? changeNotifier = null,
        MuscleCuties.UseCases.Features.Nutrition.Barcode.ILookupBarcodeUseCase? barcodeUseCase = null,
        IBarcodeScannerHardwareService? barcodeHardwareService = null,
        ISaveMealTemplateUseCase? saveMealTemplateUseCase = null,
        IGetRecipesAndTemplatesUseCase? getRecipesAndTemplatesUseCase = null,
        IHealthSyncService? healthSync = null,
        ICurrentProfileUserIdProvider? userIdProvider = null,
        IProfilePersonalInfoUseCase? personalInfoUseCase = null)
    {
        _overviewUseCase = overviewUseCase;
        _searchUseCase = searchUseCase;
        _recipePortionUseCase = recipePortionUseCase;
        _foodPortionCatalog = NutritionPresentationMapper.GetPortionCatalog(recipePortionUseCase);
        _navigateAsync = navigateAsync;
        _changeNotifier = changeNotifier;
        _barcodeUseCase = barcodeUseCase;
        _barcodeHardwareService = barcodeHardwareService;
        _saveMealTemplateUseCase = saveMealTemplateUseCase;
        _getRecipesAndTemplatesUseCase = getRecipesAndTemplatesUseCase;
        _healthSync = healthSync;
        _userIdProvider = userIdProvider;
        _personalInfoUseCase = personalInfoUseCase;
        if (_changeNotifier is not null) _changeNotifier.NutritionChanged += OnNutritionChanged;

        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync, this));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ToggleMealEditorCommand = new RelayCommand(ToggleMealEditor);
        ToggleMealContextCommand = new RelayCommand(ToggleMealContext);
        OpenMealEditorCommand = new RelayCommand(OpenMealEditor);
        SelectFoodResultCommand = new RelayCommand<FoodSearchResultItem>(SelectFoodResult);
        SearchFoodCommand = new AsyncRelayCommand(SearchFoodAsync, CanSearchFood);
        BrowseMoreFoodsCommand = new AsyncRelayCommand(BrowseMoreFoodsAsync, CanBrowseMoreFoodResults);
        AddIngredientCommand = new RelayCommand(AddSelectedFoodAsIngredient, CanAddIngredient);
        OpenFoodFinderCommand = new RelayCommand(OpenFoodFinder);
        CollapseFoodFinderCommand = new RelayCommand(CollapseFoodFinder);
        RemoveIngredientCommand = new RelayCommand<MealIngredientItem>(RemoveIngredient);
        EditIngredientCommand = new RelayCommand<MealIngredientItem>(EditIngredient);
        MealEditorBackCommand = new RelayCommand(GoBackInMealEditor);
        LogMealCommand = new AsyncRelayCommand(LogMealAsync, CanLogMeal);
        SetBreakfastPreferenceCommand = new AsyncRelayCommand<string>(SetBreakfastPreferenceAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        ToggleCustomFoodPanelCommand = new RelayCommand(ToggleCustomFoodPanel);
        CreateCustomFoodCommand = new AsyncRelayCommand(CreateCustomFoodAsync, () => !IsBusy);
        Breakdown = new NutritionBreakdownPresenter(
            () => IsBreakdownModalVisible = false,
            EditBreakdownMealAsync);
        OpenMicronutrientsModalCommand = new RelayCommand(OpenDailyBreakdown);
        CloseMicronutrientsModalCommand = new RelayCommand(CloseBreakdownModal);
        OpenMealBreakdownCommand = new RelayCommand<MealItem>(OpenMealBreakdown);
        EditMealCommand = new AsyncRelayCommand<MealItem>(meal => EditMealAsync(meal));
        SelectMealTypeCommand = new RelayCommand<MealType>(SelectMealType);
        OpenSuggestMealCommand = new RelayCommand(OpenSuggestMeal);
        CloseSuggestionModalCommand = new RelayCommand(CloseSuggestionModal);
        RefreshSuggestionsCommand = new RelayCommand(RefreshSuggestions);
        AcceptSuggestionCommand = new RelayCommand<MealSuggestionItem>(AcceptSuggestion);
        SelectSuggestionMealTypeCommand = new RelayCommand<MealType>(SelectSuggestionMealType);
        BackToMealTypePickerCommand = new RelayCommand(BackToMealTypePicker);
        RefreshDayMealCommand = new RelayCommand<DayMealPlanItem>(RefreshDayMeal);
        UsePlannedMealCommand = new RelayCommand<DayMealPlanItem>(UsePlannedMeal);
        OpenMealDetailCommand = new RelayCommand<MealSuggestionItem>(OpenMealDetail);
        CloseMealDetailCommand = new RelayCommand(CloseMealDetail);
        OpenMealEntryCommand = new RelayCommand(OpenMealEntry);
        CloseMealEntryCommand = new RelayCommand(CloseMealEntry);
        SelectEntryMealTypeCommand = new RelayCommand<MealType>(SelectEntryMealType);
        StartManualMealCommand = new AsyncRelayCommand(StartManualMealAsync);
        StartSuggestedMealCommand = new RelayCommand(StartSuggestedMeal);
        StartDayPlanCommand = new RelayCommand(StartDayPlan);
        ToggleTemplatePickerCommand = new RelayCommand(ToggleTemplatePicker);
        LogMealTemplateCommand = new AsyncRelayCommand<MealTemplate>(LogMealTemplateAsync);
        DeleteMealTemplateCommand = new AsyncRelayCommand<MealTemplate>(DeleteMealTemplateAsync);
        SaveMealAsTemplateCommand = new AsyncRelayCommand<MealItem>(SaveMealAsTemplateAsync);
        MealIngredients.CollectionChanged += OnMealIngredientsCollectionChanged;
    }

    /// <summary>How full the calorie ring is, from the shared progress formatter so every ring on the page agrees.</summary>
    public float CaloriesProgress =>
        MacroProgressFormatter.CalculateProgress(ConsumedCalories, TargetCalories);

    /// <summary>Consumed calories with the card's own formatting, not the raw number the totals carry.</summary>
    public string CaloriesConsumed => MacroProgressFormatter.FormatCaloriesConsumed(ConsumedCalories);
    /// <summary>The same value as a whole number, for the places that show it without a unit.</summary>
    public string ConsumedCaloriesText => DisplayFormat.WholeNumber(ConsumedCalories);
    /// <summary>The day's calorie target formatted as the card's goal line.</summary>
    public string CaloriesGoal => MacroProgressFormatter.FormatCaloriesGoal(TargetCalories);

    /// <summary>Protein eaten against its target, as the macro row reads it.</summary>
    public string ProteinText => MacroProgressFormatter.FormatGramsText(ConsumedProtein, TargetProtein);
    /// <summary>Protein progress as a 0–1 fraction for the macro bar.</summary>
    public float ProteinProgress => MacroProgressFormatter.CalculateProgress(ConsumedProtein, TargetProtein);

    /// <summary>Carbohydrate eaten against its target.</summary>
    public string CarbsText => MacroProgressFormatter.FormatGramsText(ConsumedCarbs, TargetCarbs);
    /// <summary>Carbohydrate progress as a fraction.</summary>
    public float CarbsProgress => MacroProgressFormatter.CalculateProgress(ConsumedCarbs, TargetCarbs);

    /// <summary>Fat eaten against its target.</summary>
    public string FatsText => MacroProgressFormatter.FormatGramsText(ConsumedFats, TargetFats);
    /// <summary>Fat progress as a fraction.</summary>
    public float FatsProgress => MacroProgressFormatter.CalculateProgress(ConsumedFats, TargetFats);

    /// <summary>The badge above the targets: the cycle phase when one is known, otherwise the neutral daily-targets</summary>
    /// wording, because a targets card cannot be labelled with a phase it does not have.</summary>
    public string PhaseFocusBadgeText => CurrentPhase == CyclePhase.Unknown ||
                                        string.IsNullOrWhiteSpace(CurrentPhaseName)
        ? MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Nutrition_Breakdown_DailyTargets")
        : $"Phase focus · {CurrentPhaseName}";

    /// <summary>The glyph matching the cycle phase shown beside the targets.</summary>
    public string CurrentPhaseIconGlyph =>
        MacroProgressFormatter.GetPhaseIconGlyph(NutritionPresentationMapper.ToPresentationCyclePhase(CurrentPhase));


    /// <summary>The serving units a custom food can use.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in Nutrition XAML / view model contract.")]
    public IReadOnlyList<string> CustomFoodServingUnits => _foodPortionCatalog.SupportedUnits;

    /// <summary>True when the search returned foods.</summary>
    public bool HasFoodSearchResults => FoodSearchResults.Count > 0;

    /// <summary>
    /// True before the user has typed anything, so the finder can invite a search instead of
    /// claiming that a query returned no matches.
    /// </summary>
    public bool IsFoodSearchIdle => string.IsNullOrWhiteSpace(SearchQuery);
    /// <summary>True when more foods can be loaded.</summary>
    public bool ShowBrowseMoreFoods => HasFoodSearchResults && HasMoreFoodResults;
    /// <summary>True when the finder shows a message.</summary>
    public bool HasAddFoodMessage => !string.IsNullOrWhiteSpace(AddFoodMessage);
    /// <summary>True when the meal has ingredients.</summary>
    public bool HasMealIngredients => MealIngredients.Count > 0;

    /// <summary>True when the food finder is shown.</summary>
    public bool IsFoodFinderVisible =>
        IsFoodFinderExpanded ||
        SelectedFoodResult is not null ||
        IsCustomFoodPanelVisible;

    /// <summary>True when the food finder is hidden.</summary>
    public bool IsFoodFinderCollapsed => !IsFoodFinderVisible;

    /// <summary>True when the ingredient search is shown.</summary>
    public bool IsIngredientSearchVisible =>
        IsFoodFinderExpanded && SelectedFoodSource == MealFoodSource.Search &&
        SelectedFoodResult is null && !IsCustomFoodPanelVisible;

    /// <summary>True when the barcode source is shown.</summary>
    public bool IsBarcodeSourceVisible =>
        IsFoodFinderExpanded && SelectedFoodSource == MealFoodSource.Barcode &&
        SelectedFoodResult is null;

    /// <summary>True when a food is selected for editing.</summary>
    public bool IsSelectedFoodEditorVisible => SelectedFoodResult is not null;
    /// <summary>Whether the ingredient button will add a food or replace the one being edited.</summary>
    public string IngredientActionText => _editingIngredient is null ? "Add ingredient" : "Update ingredient";
    /// <summary>True when the breakfast preference is savoury.</summary>
    public bool IsSavouryBreakfast => BreakfastPreference == BreakfastPreference.Savoury;
    /// <summary>True when the breakfast preference is sweet.</summary>
    public bool IsSweetBreakfast => BreakfastPreference == BreakfastPreference.Sweet;

    public string BreakfastTargetText
    {
        get => FormatMealTarget("Breakfast", _mealDistribution?.Breakfast);
    }

    public string LunchTargetText
    {
        get => FormatMealTarget("Lunch", _mealDistribution?.Lunch);
    }

    public string DinnerTargetText
    {
        get => FormatMealTarget("Dinner", _mealDistribution?.Dinner);
    }

    public string SnackTargetText
    {
        get => _mealDistribution is { Snack: <= 0f }
            ? "Snack (optional)"
            : FormatMealTarget("Snack", _mealDistribution?.Snack);
    }

    /// <summary>True when the day has meals.</summary>
    public bool HasMeals => Meals.Count > 0;
    /// <summary>True when the day has no meals.</summary>
    public bool HasNoMeals => Meals.Count == 0;

    /// <summary>True when breakfast is the selected meal.</summary>
    public bool IsBreakfastSelected => SelectedMealType == MealType.Breakfast;
    /// <summary>True when lunch is the selected meal.</summary>
    public bool IsLunchSelected => SelectedMealType == MealType.Lunch;
    /// <summary>True when dinner is the selected meal.</summary>
    public bool IsDinnerSelected => SelectedMealType == MealType.Dinner;
    /// <summary>True when a snack is the selected meal.</summary>
    public bool IsSnackSelected => SelectedMealType == MealType.Snack;

    /// <summary>The editor's heading, which names the step the user is in — scanning, custom food, portion, search, or the</summary>
    /// meal itself.</summary>
    public string MealEditorTitle => IsBarcodeSourceVisible ? UiTextCatalog.Get("Nutrition_MealEditor_ScanBarcodeTitle")
        : IsCustomFoodPanelVisible ? "Custom food"
        : IsSelectedFoodEditorVisible ? "Portion"
        : IsIngredientSearchVisible ? "Add food"
        : IsEditingMeal ? "Edit meal" : "Log meal";

    /// <summary>The draft meal's calories, summed over its ingredients.</summary>
    public string MealDraftCaloriesText => DisplayFormat.Calories(MealIngredients.Sum(i => i.CaloriesForAmount));
    /// <summary>The draft meal's protein, summed over its ingredients.</summary>
    public string MealDraftProteinText => DisplayFormat.Grams(MealIngredients.Sum(i => i.ProteinForAmount));
    /// <summary>The draft meal's carbohydrate, summed over its ingredients.</summary>
    public string MealDraftCarbsText => DisplayFormat.Grams(MealIngredients.Sum(i => i.CarbsForAmount));
    /// <summary>The draft meal's fat, summed over its ingredients.</summary>
    public string MealDraftFatsText => DisplayFormat.Grams(MealIngredients.Sum(i => i.FatsForAmount));

    /// <summary>The custom-food toggle names the action it will take: open the form or close it.</summary>
    public string CustomFoodToggleText => IsCustomFoodPanelVisible ? "Hide custom food" : "Create custom food";
    /// <summary>The browse button says it is working while the next page loads, so a slow page cannot be tapped twice</summary>
    /// unknowingly.</summary>
    public string BrowseMoreFoodsButtonText => IsBrowsingMoreFoods ? "Browsing..." : "Browse more";

    public string MealIngredientsTotalText
    {
        get
        {
            if (MealIngredients.Count == 0)
                return "No ingredients added yet.";

            var calories = MealIngredients.Sum(i => i.CaloriesForAmount);
            var protein = MealIngredients.Sum(i => i.ProteinForAmount);
            var carbs = MealIngredients.Sum(i => i.CarbsForAmount);
            var fats = MealIngredients.Sum(i => i.FatsForAmount);
            var ingredientLabel = MealIngredients.Count == 1 ? "ingredient" : "ingredients";
            return
                $"{MealIngredients.Count} {ingredientLabel} · {DisplayFormat.Calories(calories)} · {DisplayFormat.Macros(protein, carbs, fats)}";
        }
    }

    /// <summary>The save button says log or save, depending on whether the editor is creating a meal or changing one.</summary>
    public string LogMealButtonText => MuscleCuties.UI.Services.Localization.UiTextCatalog.Get(IsEditingMeal ? "Nutrition_Meal_SaveButton" : "Nutrition_Meal_LogButton");
    /// <summary>True when the meal context can be edited.</summary>
    public bool IsMealContextEditorVisible => !IsMealChoiceLocked && IsMealContextExpanded;
    /// <summary>True when the meal context summary is shown.</summary>
    public bool IsMealContextSummaryVisible => IsMealChoiceLocked || !IsMealContextExpanded;
    /// <summary>True when the meal context can change.</summary>
    public bool CanChangeMealContext => !IsMealChoiceLocked;
    /// <summary>Which meal and when, in one line, for the collapsed context row.</summary>
    public string MealChoiceSummary =>
        $"{UiTextCatalog.Get($"Nutrition_MealType_{SelectedMealType}")} · {DisplayFormat.Time(SelectedMealTime)}";

    /// <summary>The chosen meal's name, from its catalog key.</summary>
    public string SelectedMealTypeText =>
        UiTextCatalog.Get($"Nutrition_MealType_{SelectedMealType}");

    /// <summary>The date a meal is being logged or edited for: "today" unless an older meal is being edited.</summary>
    public string SelectedMealDateText =>
        IsEditingMeal && _editingMealDate.Date != DateTime.Today
            ? DisplayFormat.DayMonth(_editingMealDate)
            : UiTextCatalog.Get("Common_Today");

    /// <summary>The chosen meal time as a clock.</summary>
    public string SelectedMealTimeText => DisplayFormat.Time(SelectedMealTime);

    /// <summary>True when the food source tabs are shown.</summary>
    public bool IsFoodSourceTabsVisible =>
        IsFoodFinderExpanded && SelectedFoodResult is null && !IsCustomFoodPanelVisible;

    /// <summary>The chosen food's name, or a prompt to choose one.</summary>
    public string SelectedFoodText => SelectedFoodResult?.Name ?? "No food selected";
    /// <summary>Where the chosen food comes from, empty when it has no source to show.</summary>
    public string SelectedFoodSourceText => SelectedFoodResult?.SourceSummary ?? string.Empty;
    /// <summary>True when a food source is chosen.</summary>
    public bool HasSelectedFoodSource => !string.IsNullOrWhiteSpace(SelectedFoodSourceText);

    public string SelectedFoodAmountText
    {
        get
        {
            if (SelectedFoodResult is null)
                return "Choose a food and enter an amount.";

            if (SelectedServingOption is null)
                return "Choose a serving option.";

            if (!TryParseAmount(FoodGrams, out var amount))
                return "Enter amount greater than zero.";

            if (!HasCalories(SelectedFoodResult.Calories))
                return "Nutrition values are unavailable for this food.";

            var grams = amount * SelectedServingOption.Grams;
            return
                $"{FormatServingAmount(amount, SelectedServingOption.Label)}: {BuildNutritionForGrams(SelectedFoodResult, grams)}";
        }
    }

    /// <summary>RM05: one presenter shared with the Daily Food Log; each caller supplies its own date's values.</summary>
    public NutritionBreakdownPresenter Breakdown { get; }

    /// <summary>Reloads the day.</summary>
    public AsyncRelayCommand RefreshCommand { get; }
    /// <summary>Shows or hides the meal editor.</summary>
    public RelayCommand ToggleMealEditorCommand { get; }
    /// <summary>Shows or hides the meal context.</summary>
    public RelayCommand ToggleMealContextCommand { get; }
    /// <summary>Opens the meal editor.</summary>
    public RelayCommand OpenMealEditorCommand { get; }
    /// <summary>Selects a search result.</summary>
    public RelayCommand<FoodSearchResultItem> SelectFoodResultCommand { get; }
    /// <summary>Searches the catalog.</summary>
    public AsyncRelayCommand SearchFoodCommand { get; }
    /// <summary>Loads more search results.</summary>
    public AsyncRelayCommand BrowseMoreFoodsCommand { get; }
    /// <summary>Adds the selected food to the meal.</summary>
    public RelayCommand AddIngredientCommand { get; }
    /// <summary>Opens the food finder.</summary>
    public RelayCommand OpenFoodFinderCommand { get; }
    /// <summary>Collapses the food finder.</summary>
    public RelayCommand CollapseFoodFinderCommand { get; }
    /// <summary>Removes an ingredient.</summary>
    public RelayCommand<MealIngredientItem> RemoveIngredientCommand { get; }
    /// <summary>Edits an ingredient.</summary>
    public RelayCommand<MealIngredientItem> EditIngredientCommand { get; }
    /// <summary>Leaves the meal editor.</summary>
    public RelayCommand MealEditorBackCommand { get; }
    /// <summary>Logs the meal.</summary>
    public AsyncRelayCommand LogMealCommand { get; }
    /// <summary>Stores the breakfast preference.</summary>
    public AsyncRelayCommand<string> SetBreakfastPreferenceCommand { get; }
    /// <summary>Shows or hides the custom food panel.</summary>
    public RelayCommand ToggleCustomFoodPanelCommand { get; }
    /// <summary>Creates the custom food.</summary>
    public AsyncRelayCommand CreateCustomFoodCommand { get; }
    /// <summary>Opens the micronutrients modal.</summary>
    public RelayCommand OpenMicronutrientsModalCommand { get; }
    /// <summary>Closes the micronutrients modal.</summary>
    public RelayCommand CloseMicronutrientsModalCommand { get; }
    /// <summary>Opens a meal's nutrition breakdown.</summary>
    public RelayCommand<MealItem> OpenMealBreakdownCommand { get; }
    /// <summary>Opens a meal for editing.</summary>
    public AsyncRelayCommand<MealItem> EditMealCommand { get; }
    /// <summary>Selects the meal being edited.</summary>
    public RelayCommand<MealType> SelectMealTypeCommand { get; }
    /// <summary>Opens the meal suggestions.</summary>
    public RelayCommand OpenSuggestMealCommand { get; }
    /// <summary>Closes the suggestions.</summary>
    public RelayCommand CloseSuggestionModalCommand { get; }
    /// <summary>Regenerates the suggestions.</summary>
    public RelayCommand RefreshSuggestionsCommand { get; }
    /// <summary>Accepts a suggestion as the meal.</summary>
    public RelayCommand<MealSuggestionItem> AcceptSuggestionCommand { get; }
    /// <summary>Chooses the meal to suggest for.</summary>
    public RelayCommand<MealType> SelectSuggestionMealTypeCommand { get; }
    /// <summary>Returns to the meal picker.</summary>
    public RelayCommand BackToMealTypePickerCommand { get; }
    /// <summary>Regenerates a planned meal.</summary>
    public RelayCommand<DayMealPlanItem> RefreshDayMealCommand { get; }
    /// <summary>Logs the planned meal.</summary>
    public RelayCommand<DayMealPlanItem> UsePlannedMealCommand { get; }
    /// <summary>Opens a suggestion's detail.</summary>
    public RelayCommand<MealSuggestionItem> OpenMealDetailCommand { get; }
    /// <summary>Closes the suggestion detail.</summary>
    public RelayCommand CloseMealDetailCommand { get; }
    /// <summary>Opens the meal entry editor.</summary>
    public RelayCommand OpenMealEntryCommand { get; }
    /// <summary>Closes the meal entry editor.</summary>
    public RelayCommand CloseMealEntryCommand { get; }
    /// <summary>Selects the meal the entry logs to.</summary>
    public RelayCommand<MealType> SelectEntryMealTypeCommand { get; }
    /// <summary>Starts a manually built meal.</summary>
    public AsyncRelayCommand StartManualMealCommand { get; }
    /// <summary>Starts a meal from suggestions.</summary>
    public RelayCommand StartSuggestedMealCommand { get; }
    /// <summary>Starts a meal from the day plan.</summary>
    public RelayCommand StartDayPlanCommand { get; }
    /// <summary>Shows or hides the template picker.</summary>
    public RelayCommand ToggleTemplatePickerCommand { get; }
    /// <summary>Logs a meal template.</summary>
    public AsyncRelayCommand<MealTemplate> LogMealTemplateCommand { get; }
    /// <summary>Deletes a meal template.</summary>
    public AsyncRelayCommand<MealTemplate> DeleteMealTemplateCommand { get; }
    /// <summary>Saves a meal as a template.</summary>
    public AsyncRelayCommand<MealItem> SaveMealAsTemplateCommand { get; }

    /// <summary>Loads the day.</summary>
    public AsyncRelayCommand LoadDataCommand { get; }
    /// <summary>The full-page indicator, shown on a busy page that has not finished a load yet.</summary>
    public bool IsPageLoading => _loadGate.IsLoading(IsBusy);

    /// <summary>Marks the day stale so the next appearance reloads it.</summary>
    public void Invalidate()
    {
        _loadGate.MarkStale();
    }

    /// <summary>Reloads the day through the load gate, forcing it past the freshness window.</summary>
    private Task RefreshAsync()
    {
        return _loadGate.RunAsync(LoadDataCoreAsync, this, true);
    }

    /// <summary>The day load: targets, meals, micronutrients and the cycle phase that colours the targets card.</summary>
    private async Task LoadDataCoreAsync()
    {
        IsBusy = true;
        try
        {
            var response = await _overviewUseCase.GetOverviewAsync(
                new NutritionOverviewRequest(DateOnly.FromDateTime(DateTime.Today), BreakfastPreference.ToString()));

            CurrentPhase = Enum.TryParse<CyclePhase>(response.CurrentPhase, true, out var cp) &&
                           Enum.IsDefined(cp) && response.PhaseAvailability == "Available"
                ? cp
                : CyclePhase.Unknown;
            CurrentPhaseName = CurrentPhase == CyclePhase.Unknown ? string.Empty : response.CurrentPhaseName;
            PhaseAvailability = response.PhaseAvailability;
            TargetSource = response.TargetSource;
            ApplyTargetBreakdown(NutritionPresentationMapper.FromTargetBreakdown(response.TargetBreakdown));
            PhaseFocusTitle = response.PhaseFocusTitle;
            PhaseFocusCopy = response.PhaseFocusCopy;

            _isBulkLoading = true;
            TargetCalories = response.Targets.Calories;
            TargetProtein = response.Targets.Protein;
            TargetCarbs = response.Targets.Carbs;
            TargetFats = response.Targets.Fats;
            _mealDistribution = NutritionPresentationMapper.FromMealDistribution(response.Targets.MealDistribution);

            if (_healthSync is not null && _userIdProvider is not null)
            {
                var userId = await _userIdProvider.GetCurrentUserIdAsync();
                if (userId > 0)
                {
                    var healthSummary = await _healthSync.GetCachedWeeklySummaryAsync(userId);
                    if (healthSummary is not null && healthSummary.HasMovementData)
                    {
                        // No invented defaults: without a known weight and age there is no activity adjustment.
                        var weightKg = 0f;
                        var age = 0;
                        if (_personalInfoUseCase is not null)
                        {
                            var info = await _personalInfoUseCase.LoadAsync();
                            if (info.Profile is not null)
                            {
                                if (info.Profile.Weight > 0) weightKg = info.Profile.Weight;
                                if (info.Profile.DateOfBirth != default)
                                {
                                    age = DateTime.Today.Year - info.Profile.DateOfBirth.Year;
                                    if (info.Profile.DateOfBirth.Date > DateTime.Today.AddYears(-age)) age--;
                                }
                            }
                        }
                        var adjustment = weightKg > 0f && age > 0
                            ? HealthEnergyAdjustment.CalculateDailyCalories(healthSummary, weightKg, age, 3, CurrentPhase)
                            : 0f;
                        ActivityAdjustmentCalories = adjustment;
                        HasActivityAdjustment = adjustment != 0f;
                        ActivityAdjustmentText = HasActivityAdjustment
                            ? (adjustment > 0 ? $"+{DisplayFormat.Calories(adjustment)}" : DisplayFormat.Calories(adjustment))
                            : string.Empty;
                        TargetCalories += adjustment;
                    }
                    else
                    {
                        HasActivityAdjustment = false;
                        ActivityAdjustmentText = string.Empty;
                        ActivityAdjustmentCalories = 0f;
                    }
                }
            }

            ConsumedCalories = response.Consumed.Calories;
            ConsumedProtein = response.Consumed.Protein;
            ConsumedCarbs = response.Consumed.Carbs;
            ConsumedFats = response.Consumed.Fats;

            Meals = new ObservableCollection<MealItem>(
                response.Meals.Select(m => MapMealDtoToItem(NutritionPresentationMapper.FromMealSummary(m))));
            RefreshMealTargetShares();
            Micronutrients = new ObservableCollection<DailyMicronutrientItem>(
                response.Micronutrients.Select(m => MapMicroDtoToItem(NutritionPresentationMapper.FromMicronutrient(m))));
            SelectedBreakdownCoverageText = DescribeMicronutrientCoverage(
                NutritionPresentationMapper.FromMicronutrientCoverage(response.MicronutrientCoverage));

            _isBulkLoading = false;
            NotifyDisplayProperties();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>One meal's target calories, with a dash instead of a number when the plan gives that meal no share.</summary>
    private string FormatMealTarget(string mealName, float? share) => share is { } value
        ? $"{mealName} ({(int)(TargetCalories * value)} kcal)"
        : $"{mealName} (— kcal)";

    /// <summary>Expands or collapses the meal-and-time row; collapsed it shows the summary instead.</summary>
    private void ToggleMealContext()
    {
        // A locked context (for example an existing meal or a fixed suggestion time) cannot be
        // changed from the editor; the lock is a contract, not a presentation preference.
        if (IsMealChoiceLocked)
            return;

        IsMealContextExpanded = !IsMealContextExpanded;
    }

    /// <summary>Opens the editor or closes it, depending on where the user already is.</summary>
    private void ToggleMealEditor()
    {
        if (IsMealEditorVisible)
        {
            CloseMealEditor();
            return;
        }

        OpenMealEditor();
    }

    /// <summary>Opens the editor, dropping the previous edit's draft so an abandoned edit cannot be saved by accident.</summary>
    private void OpenMealEditor()
    {
        if (IsEditingMeal)
            ResetMealDraft();

        IsMealEditorVisible = true;
        PrepareMealEditor();
    }

    /// <summary>Closes the editor and its food finder, unlocked, discarding the draft when it was an edit; ignored while a</summary>
    /// write is in flight so the sheet cannot vanish mid-save.</summary>
    private void CloseMealEditor()
    {
        if (IsBusy)
            return;

        CollapseFoodFinder();
        IsMealEditorVisible = false;
        IsMealChoiceLocked = false;

        if (IsEditingMeal)
            ResetMealDraft();
    }

    /// <summary>Resets the editor's transient state: an empty draft starts at the current time with the finder closed, a</summary>
    /// draft carried in from another surface keeps its contents.</summary>
    private void PrepareMealEditor()
    {
        AddFoodMessage = string.Empty;
        if (MealIngredients.Count == 0)
        {
            SelectedMealTime = DateTime.Now.TimeOfDay;
            FoodGrams = string.Empty;
            IsFoodFinderExpanded = false;
            return;
        }

        IsFoodFinderExpanded = false;
    }

    /// <summary>Opens the finder on catalog search with nothing selected, so the user always starts from the same place.</summary>
    private void OpenFoodFinder()
    {
        ResetFoodSource();
        _editingIngredient = null;
        SelectedFoodResult = null;
        IsCustomFoodPanelVisible = false;
        IsFoodFinderExpanded = true;
        AddFoodMessage = string.Empty;
    }

    /// <summary>Closes the finder and throws away its query, results, paging and selection.</summary>
    private void CollapseFoodFinder()
    {
        ResetFoodSource();
        InvalidateFoodSearch();
        _editingIngredient = null;
        SelectedFoodResult = null;
        SearchQuery = string.Empty;
        FoodSearchResults = [];
        ResetFoodSearchPaging();
        IsCustomFoodPanelVisible = false;
        IsFoodFinderExpanded = false;
        AddFoodMessage = string.Empty;
    }

    /// <summary>Steps back one level — from a chosen food to the finder, then out of the editor — so back never skips the</summary>
    /// level the user came from.</summary>
    private void GoBackInMealEditor()
    {
        if (IsBusy)
            return;

        if (SelectedFoodResult is not null && _editingIngredient is null)
        {
            SelectedFoodResult = null;
            IsFoodFinderExpanded = true;
            AddFoodMessage = string.Empty;
            return;
        }

        if (IsCustomFoodPanelVisible || SelectedFoodSource != MealFoodSource.Search)
        {
            SelectedFoodResult = null;
            IsCustomFoodPanelVisible = false;
            SelectFoodSource(MealFoodSource.Search);
            AddFoodMessage = string.Empty;
            return;
        }

        CollapseFoodFinder();
    }

    /// <summary>Starts the celebration burst, taking its artwork from the current cycle phase and bumping the token so the</summary>
    /// view replays it even for the same phase.</summary>
    private void TriggerCelebration()
    {
        CelebrationIconSource = CyclePhaseAssets.GetVisualSource(
            NutritionPresentationMapper.ToPresentationCyclePhase(CurrentPhase));
        CelebrationToken++;
    }

    /// <summary>Refreshes the phase guidance when the name changes.</summary>
    partial void OnCurrentPhaseNameChanged(string value)
    {
        OnPropertyChanged(nameof(PhaseFocusBadgeText));
    }

    /// <summary>Refreshes the phase visuals when the phase changes.</summary>
    partial void OnCurrentPhaseChanged(CyclePhase value)
    {
        OnPropertyChanged(nameof(CurrentPhaseIconGlyph));
        OnPropertyChanged(nameof(PhaseFocusBadgeText));
    }

    /// <summary>Refreshes the editor flags when the meal editor opens.</summary>
    partial void OnIsEditingMealChanged(bool value)
    {
        OnPropertyChanged(nameof(MealEditorTitle));
        OnPropertyChanged(nameof(LogMealButtonText));
    }

    /// <summary>Reloads the day when the nutrition data changes.</summary>
    private void OnNutritionChanged(object? sender, NutritionChangedEventArgs e)
    {
        _loadGate.InvalidateAndReloadIfToday(e.Date, LoadDataCoreAsync, this);
    }


    /// <summary>A dead entry point states so instead of leaving the page unchanged and silent.</summary>
    public void ReportDiaryNavigationFailure() =>
        ShowPageStatus(UiTextCatalog.Get("Ui_Nutrition_Error_LoadFoods"));

    /// <summary>Releases the view model's subscriptions.</summary>
    public void Dispose()
    {
        _barcodeScan?.Dispose();
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
