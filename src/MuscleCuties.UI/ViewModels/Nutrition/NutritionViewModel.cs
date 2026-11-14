using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.Navigation;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Formatting;
using MuscleCuties.UI.Services.Dashboard;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

namespace MuscleCuties.UI.ViewModels.Nutrition;

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

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;

    [ObservableProperty] private bool _isCustomFoodPanelVisible;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBarcodeSourceVisible), nameof(IsIngredientSearchVisible),
        nameof(MealEditorTitle))]
    private MealFoodSource _selectedFoodSource = MealFoodSource.Search;

    public MealFoodSourceTab SearchFoodSourceTab { get; } =
        new(MealFoodSource.Search) { IsSelected = true };
    public MealFoodSourceTab BarcodeFoodSourceTab { get; } = new(MealFoodSource.Barcode);
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

    public float CaloriesProgress =>
        MacroProgressFormatter.CalculateProgress(ConsumedCalories, TargetCalories);

    public string CaloriesConsumed => MacroProgressFormatter.FormatCaloriesConsumed(ConsumedCalories);
    public string CaloriesGoal => MacroProgressFormatter.FormatCaloriesGoal(TargetCalories);

    public string ProteinText => MacroProgressFormatter.FormatGramsText(ConsumedProtein, TargetProtein);
    public float ProteinProgress => MacroProgressFormatter.CalculateProgress(ConsumedProtein, TargetProtein);

    public string CarbsText => MacroProgressFormatter.FormatGramsText(ConsumedCarbs, TargetCarbs);
    public float CarbsProgress => MacroProgressFormatter.CalculateProgress(ConsumedCarbs, TargetCarbs);

    public string FatsText => MacroProgressFormatter.FormatGramsText(ConsumedFats, TargetFats);
    public float FatsProgress => MacroProgressFormatter.CalculateProgress(ConsumedFats, TargetFats);

    public string PhaseFocusBadgeText => CurrentPhase == CyclePhase.Unknown ||
                                        string.IsNullOrWhiteSpace(CurrentPhaseName)
        ? "Nutrition"
        : $"Phase focus · {CurrentPhaseName}";

    public string CurrentPhaseIconGlyph =>
        MacroProgressFormatter.GetPhaseIconGlyph(NutritionPresentationMapper.ToPresentationCyclePhase(CurrentPhase));

    public IReadOnlyList<MealType> MealTypes { get; } = Enum.GetValues<MealType>();

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in Nutrition XAML / view model contract.")]
    public IReadOnlyList<string> CustomFoodServingUnits => _foodPortionCatalog.SupportedUnits;

    public bool HasFoodSearchResults => FoodSearchResults.Count > 0;

    /// <summary>
    /// True before the user has typed anything, so the finder can invite a search instead of
    /// claiming that a query returned no matches.
    /// </summary>
    public bool IsFoodSearchIdle => string.IsNullOrWhiteSpace(SearchQuery);
    public bool ShowBrowseMoreFoods => HasFoodSearchResults && HasMoreFoodResults;
    public bool HasAddFoodMessage => !string.IsNullOrWhiteSpace(AddFoodMessage);
    public bool HasMealIngredients => MealIngredients.Count > 0;

    public bool IsFoodFinderVisible =>
        IsFoodFinderExpanded ||
        SelectedFoodResult is not null ||
        IsCustomFoodPanelVisible;

    public bool IsFoodFinderCollapsed => !IsFoodFinderVisible;

    public bool IsIngredientSearchVisible =>
        IsFoodFinderExpanded && SelectedFoodSource == MealFoodSource.Search &&
        SelectedFoodResult is null && !IsCustomFoodPanelVisible;

    public bool IsBarcodeSourceVisible =>
        IsFoodFinderExpanded && SelectedFoodSource == MealFoodSource.Barcode &&
        SelectedFoodResult is null;

    public bool IsSelectedFoodEditorVisible => SelectedFoodResult is not null;
    public string IngredientActionText => _editingIngredient is null ? "Add ingredient" : "Update ingredient";
    public bool IsSavouryBreakfast => BreakfastPreference == BreakfastPreference.Savoury;
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

    public bool HasMeals => Meals.Count > 0;
    public bool HasNoMeals => Meals.Count == 0;

    public bool IsBreakfastSelected => SelectedMealType == MealType.Breakfast;
    public bool IsLunchSelected => SelectedMealType == MealType.Lunch;
    public bool IsDinnerSelected => SelectedMealType == MealType.Dinner;
    public bool IsSnackSelected => SelectedMealType == MealType.Snack;

    public string MealEditorTitle => IsBarcodeSourceVisible ? UiTextCatalog.Get("Nutrition_MealEditor_ScanBarcodeTitle")
        : IsCustomFoodPanelVisible ? "Custom food"
        : IsSelectedFoodEditorVisible ? "Portion"
        : IsIngredientSearchVisible ? "Add food"
        : IsEditingMeal ? "Edit meal" : "Log meal";

    public string MealDraftCaloriesText => $"{MealIngredients.Sum(i => i.CaloriesForAmount):N0} kcal";
    public string MealDraftProteinText => $"{MealIngredients.Sum(i => i.ProteinForAmount):N1} g";
    public string MealDraftCarbsText => $"{MealIngredients.Sum(i => i.CarbsForAmount):N1} g";
    public string MealDraftFatsText => $"{MealIngredients.Sum(i => i.FatsForAmount):N1} g";

    public string CustomFoodToggleText => IsCustomFoodPanelVisible ? "Hide custom food" : "Create custom food";
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
                $"{MealIngredients.Count} {ingredientLabel} · {calories:N0} kcal · P {protein:N1}g · C {carbs:N1}g · F {fats:N1}g";
        }
    }

    public string LogMealButtonText => MuscleCuties.UI.Services.Localization.UiTextCatalog.Get(IsEditingMeal ? "Nutrition_Meal_SaveButton" : "Nutrition_Meal_LogButton");
    public bool IsMealContextEditorVisible => !IsMealChoiceLocked && IsMealContextExpanded;
    public bool IsMealContextSummaryVisible => IsMealChoiceLocked || !IsMealContextExpanded;
    public bool CanChangeMealContext => !IsMealChoiceLocked;
    public string MealChoiceSummary =>
        $"{UiTextCatalog.Get($"Nutrition_MealType_{SelectedMealType}")} · {MealTime.Text(SelectedMealTime)}";

    public string SelectedMealTypeText =>
        UiTextCatalog.Get($"Nutrition_MealType_{SelectedMealType}");

    public string SelectedMealDateText =>
        IsEditingMeal && _editingMealDate.Date != DateTime.Today
            ? _editingMealDate.ToString("MMM d", CultureInfo.CurrentCulture)
            : UiTextCatalog.Get("Common_Today");

    public string SelectedMealTimeText => MealTime.Text(SelectedMealTime);

    public bool IsFoodSourceTabsVisible =>
        IsFoodFinderExpanded && SelectedFoodResult is null && !IsCustomFoodPanelVisible;

    public string SelectedFoodText => SelectedFoodResult?.Name ?? "No food selected";
    public string SelectedFoodSourceText => SelectedFoodResult?.SourceSummary ?? string.Empty;
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

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ToggleMealEditorCommand { get; }
    public RelayCommand ToggleMealContextCommand { get; }
    public RelayCommand OpenMealEditorCommand { get; }
    public RelayCommand<FoodSearchResultItem> SelectFoodResultCommand { get; }
    public AsyncRelayCommand SearchFoodCommand { get; }
    public AsyncRelayCommand BrowseMoreFoodsCommand { get; }
    public RelayCommand AddIngredientCommand { get; }
    public RelayCommand OpenFoodFinderCommand { get; }
    public RelayCommand CollapseFoodFinderCommand { get; }
    public RelayCommand<MealIngredientItem> RemoveIngredientCommand { get; }
    public RelayCommand<MealIngredientItem> EditIngredientCommand { get; }
    public RelayCommand MealEditorBackCommand { get; }
    public AsyncRelayCommand LogMealCommand { get; }
    public AsyncRelayCommand<string> SetBreakfastPreferenceCommand { get; }
    public RelayCommand ToggleCustomFoodPanelCommand { get; }
    public AsyncRelayCommand CreateCustomFoodCommand { get; }
    public RelayCommand OpenMicronutrientsModalCommand { get; }
    public RelayCommand CloseMicronutrientsModalCommand { get; }
    public RelayCommand<MealItem> OpenMealBreakdownCommand { get; }
    public AsyncRelayCommand<MealItem> EditMealCommand { get; }
    public RelayCommand<MealType> SelectMealTypeCommand { get; }
    public RelayCommand OpenSuggestMealCommand { get; }
    public RelayCommand CloseSuggestionModalCommand { get; }
    public RelayCommand RefreshSuggestionsCommand { get; }
    public RelayCommand<MealSuggestionItem> AcceptSuggestionCommand { get; }
    public RelayCommand<MealType> SelectSuggestionMealTypeCommand { get; }
    public RelayCommand BackToMealTypePickerCommand { get; }
    public RelayCommand<DayMealPlanItem> RefreshDayMealCommand { get; }
    public RelayCommand<DayMealPlanItem> UsePlannedMealCommand { get; }
    public RelayCommand<MealSuggestionItem> OpenMealDetailCommand { get; }
    public RelayCommand CloseMealDetailCommand { get; }
    public RelayCommand OpenMealEntryCommand { get; }
    public RelayCommand CloseMealEntryCommand { get; }
    public RelayCommand<MealType> SelectEntryMealTypeCommand { get; }
    public AsyncRelayCommand StartManualMealCommand { get; }
    public RelayCommand StartSuggestedMealCommand { get; }
    public RelayCommand StartDayPlanCommand { get; }
    public RelayCommand ToggleTemplatePickerCommand { get; }
    public AsyncRelayCommand<MealTemplate> LogMealTemplateCommand { get; }
    public AsyncRelayCommand<MealTemplate> DeleteMealTemplateCommand { get; }
    public AsyncRelayCommand<MealItem> SaveMealAsTemplateCommand { get; }

    public AsyncRelayCommand LoadDataCommand { get; }
    public bool IsPageLoading => IsBusy && !_loadGate.HasLoaded;

    public void Invalidate()
    {
        _loadGate.MarkStale();
    }

    private Task RefreshAsync()
    {
        return _loadGate.RunAsync(LoadDataCoreAsync, this, true);
    }

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
                            ? (adjustment > 0 ? $"+{adjustment:N0} kcal" : $"{adjustment:N0} kcal")
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

    private string FormatMealTarget(string mealName, float? share) => share is { } value
        ? $"{mealName} ({(int)(TargetCalories * value)} kcal)"
        : $"{mealName} (— kcal)";

    private void ToggleMealContext()
    {
        // A locked context (for example an existing meal or a fixed suggestion time) cannot be
        // changed from the editor; the lock is a contract, not a presentation preference.
        if (IsMealChoiceLocked)
            return;

        IsMealContextExpanded = !IsMealContextExpanded;
    }

    private void ToggleMealEditor()
    {
        if (IsMealEditorVisible)
        {
            CloseMealEditor();
            return;
        }

        OpenMealEditor();
    }

    private void OpenMealEditor()
    {
        if (IsEditingMeal)
            ResetMealDraft();

        IsMealEditorVisible = true;
        PrepareMealEditor();
    }

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

    private void OpenFoodFinder()
    {
        ResetFoodSource();
        _editingIngredient = null;
        SelectedFoodResult = null;
        IsCustomFoodPanelVisible = false;
        IsFoodFinderExpanded = true;
        AddFoodMessage = string.Empty;
    }

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

    private void TriggerCelebration()
    {
        CelebrationIconSource = CyclePhaseAssets.GetVisualSource(
            NutritionPresentationMapper.ToPresentationCyclePhase(CurrentPhase));
        CelebrationToken++;
    }

    partial void OnCurrentPhaseNameChanged(string value)
    {
        OnPropertyChanged(nameof(PhaseFocusBadgeText));
    }

    partial void OnCurrentPhaseChanged(CyclePhase value)
    {
        OnPropertyChanged(nameof(CurrentPhaseIconGlyph));
        OnPropertyChanged(nameof(PhaseFocusBadgeText));
    }

    partial void OnIsEditingMealChanged(bool value)
    {
        OnPropertyChanged(nameof(MealEditorTitle));
        OnPropertyChanged(nameof(LogMealButtonText));
    }

    private void OnNutritionChanged(object? sender, NutritionChangedEventArgs e)
    {
        _loadGate.InvalidateAndReloadIfToday(e.Date, LoadDataCoreAsync, this);
    }

    [RelayCommand]
    public async Task NavigateToFoodLogAsync()
    {
        if (_navigateAsync is null)
            return;

        try
        {
            await _navigateAsync(NutritionNavigation.FoodLogRoute);
        }
        catch (Exception exception)
        {
            Trace.TraceError($"[Nutrition] Food log navigation failed: {exception.GetType().Name}");
            ReportDiaryNavigationFailure();
        }
    }

    /// <summary>A dead entry point states so instead of leaving the page unchanged and silent.</summary>
    public void ReportDiaryNavigationFailure() =>
        ShowPageStatus(UiTextCatalog.Get("Ui_Nutrition_Error_LoadFoods"));

    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
