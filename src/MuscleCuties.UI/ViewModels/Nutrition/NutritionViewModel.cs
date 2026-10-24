using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Dashboard;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class NutritionViewModel : ObservableObject, IPageLoadAware, IDisposable
{
    private readonly INutritionChangeNotifier? _changeNotifier;
    private readonly AppPageLoadGate _loadGate = new(AppPageLoadGate.PageFreshnessWindow);
    private readonly Func<string, Task>? _navigateAsync;
    private readonly INutritionOverviewUseCase _overviewUseCase;
    private readonly IFoodCatalogSearchUseCase _searchUseCase;
    private readonly IRecipePortionUseCase _recipePortionUseCase;
    private readonly FoodPortionCatalogOutputDto _foodPortionCatalog;
    private NutritionMealDistributionDto? _mealDistribution;
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
    [ObservableProperty] private bool _isEditingMeal;
    [ObservableProperty] private bool _isFoodFinderExpanded;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isMealEditorVisible;
    [ObservableProperty] private bool _isMealChoiceLocked;
    [ObservableProperty] private ObservableCollection<MealIngredientItem> _mealIngredients = new();
    [ObservableProperty] private ObservableCollection<MealItem> _meals = new();
    [ObservableProperty] private ObservableCollection<DailyMicronutrientItem> _micronutrients = new();
    [ObservableProperty] private string _phaseFocusCopy = string.Empty;
    [ObservableProperty] private string _phaseFocusTitle = string.Empty;
    [ObservableProperty] private string _phaseAvailability = string.Empty;
    [ObservableProperty] private string _targetSource = string.Empty;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _selectedBreakdownCaloriesText = "0 kcal";
    [ObservableProperty] private float _selectedBreakdownCarbsCalories;
    [ObservableProperty] private float _selectedBreakdownFatsCalories;
    [ObservableProperty] private string _selectedBreakdownFiberText = "0.0g fiber";
    [ObservableProperty] private ObservableCollection<MacroBreakdownItem> _selectedBreakdownMacroItems = new();
    [ObservableProperty] private string _selectedBreakdownMacrosText = "P 0.0g · C 0.0g · F 0.0g";
    private MealItem? _selectedBreakdownMeal;
    [ObservableProperty] private ObservableCollection<DailyMicronutrientItem> _selectedBreakdownMicronutrients = new();
    [ObservableProperty] private string _selectedBreakdownNutrientSummaryText = "No micronutrients tracked yet";
    [ObservableProperty] private float _selectedBreakdownProteinCalories;
    [ObservableProperty] private string _selectedBreakdownTitle = "Daily nutrition";
    [ObservableProperty] private string _selectedCustomFoodServingUnit = "g";
    [ObservableProperty] private FoodSearchResultItem? _selectedFoodResult;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MealChoiceSummary))]
    private TimeSpan _selectedMealTime = DateTime.Now.TimeOfDay;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MealChoiceSummary))]
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
        INutritionChangeNotifier? changeNotifier = null)
    {
        _overviewUseCase = overviewUseCase;
        _searchUseCase = searchUseCase;
        _recipePortionUseCase = recipePortionUseCase;
        _foodPortionCatalog = recipePortionUseCase.GetCatalog();
        _navigateAsync = navigateAsync;
        _changeNotifier = changeNotifier;
        if (_changeNotifier is not null) _changeNotifier.NutritionChanged += OnNutritionChanged;

        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync, this));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ToggleMealEditorCommand = new RelayCommand(ToggleMealEditor);
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
        SetBreakfastPreferenceCommand = new AsyncRelayCommand<string>(SetBreakfastPreferenceAsync);
        ToggleCustomFoodPanelCommand = new RelayCommand(ToggleCustomFoodPanel);
        CreateCustomFoodCommand = new AsyncRelayCommand(CreateCustomFoodAsync, () => !IsBusy);
        OpenMicronutrientsModalCommand = new RelayCommand(OpenDailyBreakdown);
        CloseMicronutrientsModalCommand = new RelayCommand(CloseBreakdownModal);
        OpenMealBreakdownCommand = new RelayCommand<MealItem>(OpenMealBreakdown);
        EditSelectedBreakdownMealCommand =
            new AsyncRelayCommand(EditSelectedBreakdownMealAsync, () => CanEditSelectedBreakdown);
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
        StartManualMealCommand = new RelayCommand(StartManualMeal);
        StartSuggestedMealCommand = new RelayCommand(StartSuggestedMeal);
        StartDayPlanCommand = new RelayCommand(StartDayPlan);
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

    public string CurrentPhaseIconGlyph => MacroProgressFormatter.GetPhaseIconGlyph(CurrentPhase);

    public IReadOnlyList<MealType> MealTypes { get; } = Enum.GetValues<MealType>();

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in Nutrition XAML / view model contract.")]
    public IReadOnlyList<string> CustomFoodServingUnits => _foodPortionCatalog.SupportedUnits;

    public bool HasFoodSearchResults => FoodSearchResults.Count > 0;
    public bool ShowBrowseMoreFoods => HasFoodSearchResults && HasMoreFoodResults;
    public bool HasAddFoodMessage => !string.IsNullOrWhiteSpace(AddFoodMessage);
    public bool HasMealIngredients => MealIngredients.Count > 0;

    public bool IsFoodFinderVisible =>
        IsFoodFinderExpanded ||
        SelectedFoodResult is not null ||
        IsCustomFoodPanelVisible;

    public bool IsFoodFinderCollapsed => !IsFoodFinderVisible;

    public bool IsIngredientSearchVisible =>
        IsFoodFinderExpanded && SelectedFoodResult is null && !IsCustomFoodPanelVisible;

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

    public string MealEditorTitle => IsCustomFoodPanelVisible ? "Custom food"
        : IsSelectedFoodEditorVisible ? "Portion"
        : IsIngredientSearchVisible ? "Add food"
        : IsEditingMeal ? "Edit meal" : "Log meal";

    public string MealDraftCaloriesText => $"{MealIngredients.Sum(i => i.CaloriesForAmount):N0} kcal";
    public string MealDraftProteinText => $"{MealIngredients.Sum(i => i.ProteinForAmount):N1} g";
    public string MealDraftCarbsText => $"{MealIngredients.Sum(i => i.CarbsForAmount):N1} g";
    public string MealDraftFatsText => $"{MealIngredients.Sum(i => i.FatsForAmount):N1} g";

    public MealItem? SelectedBreakdownMeal
    {
        get => _selectedBreakdownMeal;
        set
        {
            if (!SetProperty(ref _selectedBreakdownMeal, value))
                return;

            OnPropertyChanged(nameof(CanEditSelectedBreakdown));
            EditSelectedBreakdownMealCommand.NotifyCanExecuteChanged();
        }
    }

    public bool CanEditSelectedBreakdown => SelectedBreakdownMeal is not null;
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

    public string LogMealButtonText => IsEditingMeal ? "Save meal" : "Log meal";
    public string MealChoiceSummary => $"{SelectedMealType} · {DateTime.Today.Add(SelectedMealTime):t}";

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

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ToggleMealEditorCommand { get; }
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
    public AsyncRelayCommand EditSelectedBreakdownMealCommand { get; }
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
    public RelayCommand StartManualMealCommand { get; }
    public RelayCommand StartSuggestedMealCommand { get; }
    public RelayCommand StartDayPlanCommand { get; }

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
            ApplyTargetBreakdown(response.TargetBreakdown);
            PhaseFocusTitle = response.PhaseFocusTitle;
            PhaseFocusCopy = response.PhaseFocusCopy;

            _isBulkLoading = true;
            TargetCalories = response.Targets.Calories;
            TargetProtein = response.Targets.Protein;
            TargetCarbs = response.Targets.Carbs;
            TargetFats = response.Targets.Fats;
            _mealDistribution = response.Targets.MealDistribution;

            ConsumedCalories = response.Consumed.Calories;
            ConsumedProtein = response.Consumed.Protein;
            ConsumedCarbs = response.Consumed.Carbs;
            ConsumedFats = response.Consumed.Fats;

            Meals = new ObservableCollection<MealItem>(response.Meals.Select(MapMealDtoToItem));
            Micronutrients = new ObservableCollection<DailyMicronutrientItem>(
                response.Micronutrients.Select(MapMicroDtoToItem));

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
        _editingIngredient = null;
        SelectedFoodResult = null;
        IsCustomFoodPanelVisible = false;
        IsFoodFinderExpanded = true;
        AddFoodMessage = string.Empty;
    }

    private void CollapseFoodFinder()
    {
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

        if ((SelectedFoodResult is not null && _editingIngredient is null) || IsCustomFoodPanelVisible)
        {
            SelectedFoodResult = null;
            IsCustomFoodPanelVisible = false;
            IsFoodFinderExpanded = true;
            AddFoodMessage = string.Empty;
            return;
        }

        CollapseFoodFinder();
    }

    private void TriggerCelebration()
    {
        CelebrationIconSource = CyclePhaseAssets.GetVisualSource(CurrentPhase);
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
        if (_navigateAsync is not null)
            await _navigateAsync("FoodLogPage");
    }

    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
