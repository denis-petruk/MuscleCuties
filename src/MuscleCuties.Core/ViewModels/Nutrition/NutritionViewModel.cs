using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.UI.Cycle;
using MuscleCuties.Core.Models.UI.Nutrition;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Planning;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.ViewModels.Nutrition;

public partial class NutritionViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ICycleService _cycleService;
    private readonly INutritionService _nutritionService;
    private readonly ViewModelLoadGate _loadGate = new(TimeSpan.FromSeconds(20));
    private bool _isApplyingServingDefaults;
    private ProfileNutritionGoals _micronutrientGoals = ProfileNutritionGoals.Empty;

    [ObservableProperty] private float _targetCalories;
    [ObservableProperty] private float _targetProtein;
    [ObservableProperty] private float _targetCarbs;
    [ObservableProperty] private float _targetFats;
    [ObservableProperty] private float _consumedCalories;
    [ObservableProperty] private float _consumedProtein;
    [ObservableProperty] private float _consumedCarbs;
    [ObservableProperty] private float _consumedFats;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isAddFoodPanelVisible;
    [ObservableProperty] private bool _isEditingMeal;
    [ObservableProperty] private CyclePhase _currentPhase;
    [ObservableProperty] private string _currentPhaseName = string.Empty;
    [ObservableProperty] private string _phaseFocusTitle = string.Empty;
    [ObservableProperty] private string _phaseFocusCopy = string.Empty;
    [ObservableProperty] private ObservableCollection<MealItem> _meals = new();
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private ObservableCollection<FoodSearchResultItem> _foodSearchResults = new();
    [ObservableProperty] private FoodSearchResultItem? _selectedFoodResult;
    [ObservableProperty] private string _foodGrams = "100";
    [ObservableProperty] private ObservableCollection<FoodServingOptionItem> _servingOptions = new();
    [ObservableProperty] private FoodServingOptionItem? _selectedServingOption;
    [ObservableProperty] private bool _isFoodFinderExpanded;
    [ObservableProperty] private MealType _selectedMealType = MealType.Snack;
    [ObservableProperty] private TimeSpan _selectedMealTime = DateTime.Now.TimeOfDay;
    [ObservableProperty] private string _addFoodMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<MealIngredientItem> _mealIngredients = new();
    [ObservableProperty] private ObservableCollection<MealTemplateItem> _readyMealTemplates = new();
    [ObservableProperty] private bool _isBrowsingMoreFoods;
    [ObservableProperty] private bool _hasMoreFoodResults;
    [ObservableProperty] private bool _isCustomFoodPanelVisible;
    [ObservableProperty] private bool _isBreakdownModalVisible;
    [ObservableProperty] private ObservableCollection<DailyMicronutrientItem> _micronutrients = new();
    [ObservableProperty] private ObservableCollection<MacroBreakdownItem> _selectedBreakdownMacroItems = new();
    [ObservableProperty] private ObservableCollection<DailyMicronutrientItem> _selectedBreakdownMicronutrients = new();
    [ObservableProperty] private string _selectedBreakdownTitle = "Daily nutrition";
    [ObservableProperty] private string _selectedBreakdownCaloriesText = "0 kcal";
    [ObservableProperty] private string _selectedBreakdownMacrosText = "P 0.0g · C 0.0g · F 0.0g";
    [ObservableProperty] private string _selectedBreakdownFiberText = "0.0g fiber";
    [ObservableProperty] private string _selectedBreakdownNutrientSummaryText = "No micronutrients tracked yet";
    [ObservableProperty] private float _selectedBreakdownProteinCalories;
    [ObservableProperty] private float _selectedBreakdownCarbsCalories;
    [ObservableProperty] private float _selectedBreakdownFatsCalories;
    [ObservableProperty] private string _customFoodName = string.Empty;
    [ObservableProperty] private string _customFoodCalories = string.Empty;
    [ObservableProperty] private string _customFoodProtein = string.Empty;
    [ObservableProperty] private string _customFoodCarbs = string.Empty;
    [ObservableProperty] private string _customFoodFats = string.Empty;
    [ObservableProperty] private string _customFoodServingAmount = "100";
    [ObservableProperty] private string _selectedCustomFoodServingUnit = "g";
    [ObservableProperty] private int _celebrationToken;
    [ObservableProperty] private string _celebrationIconSource = CyclePhaseAssets.FollicularAnimation;
    private MealItem? _selectedBreakdownMeal;
    private int _editingMealId;

    public float CaloriesProgress =>
        TargetCalories <= 0 ? 0f : Math.Clamp(ConsumedCalories / TargetCalories, 0f, 1f);

    public string CaloriesConsumed => $"{(int)ConsumedCalories:N0} kcal";
    public string CaloriesGoal => $"/ {(int)TargetCalories:N0} kcal";

    public string ProteinText => $"{(int)ConsumedProtein}g / {(int)TargetProtein}g";
    public float ProteinProgress => TargetProtein > 0 ? Math.Clamp(ConsumedProtein / TargetProtein, 0f, 1f) : 0f;

    public string CarbsText => $"{(int)ConsumedCarbs}g / {(int)TargetCarbs}g";
    public float CarbsProgress => TargetCarbs > 0 ? Math.Clamp(ConsumedCarbs / TargetCarbs, 0f, 1f) : 0f;

    public string FatsText => $"{(int)ConsumedFats}g / {(int)TargetFats}g";
    public float FatsProgress => TargetFats > 0 ? Math.Clamp(ConsumedFats / TargetFats, 0f, 1f) : 0f;
    public string PhaseFocusBadgeText => string.IsNullOrWhiteSpace(CurrentPhaseName)
        ? "PHASE FOCUS"
        : $"PHASE FOCUS · {CurrentPhaseName.ToUpperInvariant()}";
    public string CurrentPhaseIconGlyph => CurrentPhase switch
    {
        CyclePhase.Menstrual => "Drop24",
        CyclePhase.Follicular => "LeafThree24",
        CyclePhase.Ovulatory => "Fire24",
        CyclePhase.Luteal => "WeatherMoon24",
        _ => "HeartCircle24"
    };
    public IReadOnlyList<MealType> MealTypes { get; } = Enum.GetValues<MealType>();
    public IReadOnlyList<string> CustomFoodServingUnits => FoodServingOptions.CustomFoodUnits;
    public bool HasFoodSearchResults => FoodSearchResults.Count > 0;
    public bool ShowBrowseMoreFoods => HasFoodSearchResults && HasMoreFoodResults;
    public bool HasAddFoodMessage => !string.IsNullOrWhiteSpace(AddFoodMessage);
    public bool HasMealIngredients => MealIngredients.Count > 0;
    public bool IsFoodFinderVisible =>
        IsFoodFinderExpanded ||
        SelectedFoodResult is not null ||
        IsCustomFoodPanelVisible;
    public bool IsFoodFinderCollapsed => !IsFoodFinderVisible;
    public bool IsSelectedFoodEditorVisible => SelectedFoodResult is not null;
    public bool HasReadyMealTemplates => ReadyMealTemplates.Count > 0;
    public bool HasMeals => Meals.Count > 0;
    public bool HasNoMeals => Meals.Count == 0;
    public string AddMealPanelTitle => IsEditingMeal ? "EDIT MEAL" : "BUILD MEAL";
    public string SelectedMealTimeText => DateTime.Today.Add(SelectedMealTime).ToString("h:mm tt", CultureInfo.CurrentCulture);
    public string MicronutrientSummaryText
    {
        get
        {
            if (Micronutrients.Count == 0)
                return "No micronutrients tracked yet";

            var complete = Micronutrients.Count(item => item.IsGoalHit);
            return $"{complete} of {Micronutrients.Count} daily targets reached";
        }
    }
    public string DayFiberText => BuildFiberText(Micronutrients);
    public bool HasSelectedBreakdownMacroItems => SelectedBreakdownMacroItems.Count > 0;
    public bool HasSelectedBreakdownMicronutrients => SelectedBreakdownMicronutrients.Count > 0;
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
    public string FoodSearchResultsTitle => FoodSearchResults.Count == 0
        ? "PRODUCTS"
        : $"PRODUCTS ({FoodSearchResults.Count})";
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
            return $"{MealIngredients.Count} {ingredientLabel} · {calories:N0} kcal · P {protein:N1}g · C {carbs:N1}g · F {fats:N1}g";
        }
    }
    public string LogMealButtonText => HasMealIngredients
        ? IsEditingMeal ? "Save meal" : "Log meal"
        : "Add ingredients first";
    public string SelectedFoodText => SelectedFoodResult?.Name ?? "No food selected";
    public string SelectedFoodSourceText => SelectedFoodResult?.SourceSummary ?? string.Empty;
    public bool HasSelectedFoodSource => !string.IsNullOrWhiteSpace(SelectedFoodSourceText);
    public string SelectedFoodServingText
    {
        get
        {
            if (SelectedFoodResult is null)
                return "Choose a food to preview serving nutrition.";

            if (SelectedServingOption is null)
                return "Choose a serving option.";

            if (!HasCalories(SelectedFoodResult.Calories))
                return "Nutrition values are unavailable for this food.";

            return BuildServingNutritionPreview(SelectedFoodResult, SelectedServingOption);
        }
    }

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
            return $"{FormatServingAmount(amount, SelectedServingOption.Label)}: {BuildNutritionForGrams(SelectedFoodResult, grams)}";
        }
    }

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ToggleAddFoodPanelCommand { get; }
    public RelayCommand OpenAddFoodPanelCommand { get; }
    public RelayCommand<FoodSearchResultItem> SelectFoodResultCommand { get; }
    public RelayCommand DismissFoodSearchResultsCommand { get; }
    public AsyncRelayCommand SearchFoodCommand { get; }
    public AsyncRelayCommand BrowseMoreFoodsCommand { get; }
    public RelayCommand AddIngredientCommand { get; }
    public RelayCommand OpenFoodFinderCommand { get; }
    public RelayCommand CollapseFoodFinderCommand { get; }
    public RelayCommand<MealIngredientItem> RemoveIngredientCommand { get; }
    public AsyncRelayCommand LogMealCommand { get; }
    public RelayCommand<MealTemplateItem> ApplyReadyMealTemplateCommand { get; }
    public RelayCommand ToggleCustomFoodPanelCommand { get; }
    public AsyncRelayCommand CreateCustomFoodCommand { get; }
    public RelayCommand OpenMicronutrientsModalCommand { get; }
    public RelayCommand CloseMicronutrientsModalCommand { get; }
    public RelayCommand<MealItem> OpenMealBreakdownCommand { get; }
    public AsyncRelayCommand EditSelectedBreakdownMealCommand { get; }

    public NutritionViewModel(
        IAuthService authService,
        ICycleService cycleService,
        INutritionService nutritionService)
    {
        _authService = authService;
        _cycleService = cycleService;
        _nutritionService = nutritionService;
        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ToggleAddFoodPanelCommand = new RelayCommand(ToggleAddFoodPanel);
        OpenAddFoodPanelCommand = new RelayCommand(OpenAddFoodPanel);
        SelectFoodResultCommand = new RelayCommand<FoodSearchResultItem>(SelectFoodResult);
        DismissFoodSearchResultsCommand = new RelayCommand(DismissFoodSearchResults);
        SearchFoodCommand = new AsyncRelayCommand(SearchFoodAsync, CanSearchFood);
        BrowseMoreFoodsCommand = new AsyncRelayCommand(BrowseMoreFoodsAsync, CanBrowseMoreFoodResults);
        AddIngredientCommand = new RelayCommand(AddSelectedFoodAsIngredient, CanAddIngredient);
        OpenFoodFinderCommand = new RelayCommand(OpenFoodFinder);
        CollapseFoodFinderCommand = new RelayCommand(CollapseFoodFinder);
        RemoveIngredientCommand = new RelayCommand<MealIngredientItem>(RemoveIngredient);
        LogMealCommand = new AsyncRelayCommand(LogMealAsync, CanLogMeal);
        ApplyReadyMealTemplateCommand = new RelayCommand<MealTemplateItem>(ApplyReadyMealTemplate);
        ToggleCustomFoodPanelCommand = new RelayCommand(ToggleCustomFoodPanel);
        CreateCustomFoodCommand = new AsyncRelayCommand(CreateCustomFoodAsync);
        OpenMicronutrientsModalCommand = new RelayCommand(OpenDailyBreakdown);
        CloseMicronutrientsModalCommand = new RelayCommand(CloseBreakdownModal);
        OpenMealBreakdownCommand = new RelayCommand<MealItem>(OpenMealBreakdown);
        EditSelectedBreakdownMealCommand = new AsyncRelayCommand(EditSelectedBreakdownMealAsync, () => CanEditSelectedBreakdown);
        MealIngredients.CollectionChanged += (_, _) => NotifyMealIngredientProperties();
    }

    private Task RefreshAsync() =>
        _loadGate.RunAsync(LoadDataCoreAsync, force: true);

    private async Task LoadDataCoreAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var phase = await _cycleService.GetCurrentPhaseAsync(userId);

            CurrentPhase = phase;
            CurrentPhaseName = phase.ToString();
            PhaseFocusTitle = phase switch
            {
                CyclePhase.Menstrual => "Iron + comfort",
                CyclePhase.Follicular => "Fuel base",
                CyclePhase.Ovulatory => "Peak plate",
                CyclePhase.Luteal => "Steady cravings",
                _ => "Nutrition focus"
            };
            PhaseFocusCopy = phase switch
            {
                CyclePhase.Menstrual => "Iron, warm carbs, easy protein.",
                CyclePhase.Follicular => "Clean carbs, lean protein.",
                CyclePhase.Ovulatory => "Hydrate, balance, push.",
                CyclePhase.Luteal => "Fiber, magnesium, steady carbs.",
                _ => string.Empty
            };

            var plan = await _nutritionService.GetDailyPlanAsync(userId, phase, DateTime.Today);
            if (plan is not null)
            {
                TargetCalories = plan.Calories;
                TargetProtein = plan.Protein;
                TargetCarbs = plan.Carbs;
                TargetFats = plan.Fats;
                _micronutrientGoals = plan.Goals;
            }
            else
            {
                var (calories, protein, carbs, fats) = await _nutritionService.CalculateDailyTargetsAsync(userId, phase);
                TargetCalories = calories;
                TargetProtein = protein;
                TargetCarbs = carbs;
                TargetFats = fats;
                _micronutrientGoals = ProfileNutritionGoals.FromCalculated(
                    calories,
                    protein,
                    carbs,
                    fats,
                    25f,
                    2.3f);
            }

            await LoadReadyMealTemplatesAsync(userId, phase);

            var consumed = await LoadMealsAsync(userId);
            ConsumedCalories = consumed.Calories;
            ConsumedProtein = consumed.Protein;
            ConsumedCarbs = consumed.Carbs;
            ConsumedFats = consumed.Fats;
            NotifyDisplayProperties();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ToggleAddFoodPanel()
    {
        if (IsAddFoodPanelVisible)
        {
            CloseAddFoodPanel();
            return;
        }

        OpenAddFoodPanel();
    }

    private void OpenAddFoodPanel()
    {
        if (IsEditingMeal)
            ResetMealDraft();

        IsAddFoodPanelVisible = true;
        PrepareAddFoodPanel();
    }

    private void CloseAddFoodPanel()
    {
        CollapseFoodFinder();
        IsAddFoodPanelVisible = false;

        if (IsEditingMeal)
            ResetMealDraft();
    }

    private void PrepareAddFoodPanel()
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
        IsFoodFinderExpanded = true;
        AddFoodMessage = string.Empty;
    }

    private void CollapseFoodFinder()
    {
        SelectedFoodResult = null;
        SearchQuery = string.Empty;
        FoodSearchResults = [];
        ResetFoodSearchPaging();
        IsCustomFoodPanelVisible = false;
        IsFoodFinderExpanded = false;
        AddFoodMessage = string.Empty;
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
    }

    partial void OnIsEditingMealChanged(bool value)
    {
        OnPropertyChanged(nameof(AddMealPanelTitle));
        OnPropertyChanged(nameof(LogMealButtonText));
    }
}
