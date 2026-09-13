using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Core.Models.UI.Cycle;
using MuscleCuties.Core.Models.UI.Nutrition;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.ViewModels.Nutrition;

public partial class NutritionViewModel : ObservableObject, IPageLoadAware
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ViewModelLoadGate _loadGate = new(ViewModelLoadGate.PageFreshnessWindow);
    [ObservableProperty] private string _addFoodMessage = string.Empty;
    [ObservableProperty] private string _celebrationIconSource = CyclePhaseAssets.FollicularAnimation;
    [ObservableProperty] private int _celebrationToken;
    [ObservableProperty] private float _consumedCalories;
    [ObservableProperty] private float _consumedCarbs;
    [ObservableProperty] private float _consumedFats;
    [ObservableProperty] private float _consumedProtein;
    [ObservableProperty] private CyclePhase _currentPhase;
    [ObservableProperty] private string _currentPhaseName = string.Empty;
    [ObservableProperty] private string _customFoodCalories = string.Empty;
    [ObservableProperty] private string _customFoodCarbs = string.Empty;
    [ObservableProperty] private string _customFoodFats = string.Empty;
    [ObservableProperty] private string _customFoodName = string.Empty;
    [ObservableProperty] private string _customFoodProtein = string.Empty;
    [ObservableProperty] private string _customFoodServingAmount = "100";
    private int _editingMealId;
    [ObservableProperty] private string _foodGrams = "100";
    [ObservableProperty] private ObservableCollection<FoodSearchResultItem> _foodSearchResults = new();
    [ObservableProperty] private bool _hasMoreFoodResults;
    [ObservableProperty] private bool _isAddFoodPanelVisible;
    private bool _isApplyingServingDefaults;
    [ObservableProperty] private bool _isBreakdownModalVisible;
    [ObservableProperty] private bool _isBrowsingMoreFoods;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isCustomFoodPanelVisible;
    [ObservableProperty] private bool _isEditingMeal;
    [ObservableProperty] private bool _isFoodFinderExpanded;
    [ObservableProperty] private ObservableCollection<MealIngredientItem> _mealIngredients = new();
    [ObservableProperty] private ObservableCollection<MealItem> _meals = new();
    private ProfileNutritionGoals _micronutrientGoals = ProfileNutritionGoals.Empty;
    [ObservableProperty] private ObservableCollection<DailyMicronutrientItem> _micronutrients = new();
    [ObservableProperty] private string _phaseFocusCopy = string.Empty;
    [ObservableProperty] private string _phaseFocusTitle = string.Empty;
    [ObservableProperty] private BreakfastPreference _breakfastPreference = BreakfastPreference.Savoury;
    [ObservableProperty] private ObservableCollection<MealSuggestionItem> _breakfastSuggestions = new();
    [ObservableProperty] private ObservableCollection<MealSuggestionItem> _lunchSuggestions = new();
    [ObservableProperty] private ObservableCollection<MealSuggestionItem> _dinnerSuggestions = new();
    [ObservableProperty] private ObservableCollection<MealSuggestionItem> _snackSuggestions = new();
    [ObservableProperty] private bool _isLoadingMealIdeas;
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
    [ObservableProperty] private TimeSpan _selectedMealTime = DateTime.Now.TimeOfDay;
    [ObservableProperty] private MealType _selectedMealType = MealType.Snack;
    [ObservableProperty] private FoodServingOptionItem? _selectedServingOption;
    [ObservableProperty] private ObservableCollection<FoodServingOptionItem> _servingOptions = new();

    [ObservableProperty] private float _targetCalories;
    [ObservableProperty] private float _targetCarbs;
    [ObservableProperty] private float _targetFats;
    [ObservableProperty] private float _targetProtein;

    public NutritionViewModel(
        IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
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
        SetBreakfastPreferenceCommand = new AsyncRelayCommand<string>(SetBreakfastPreferenceAsync);
        ToggleCustomFoodPanelCommand = new RelayCommand(ToggleCustomFoodPanel);
        CreateCustomFoodCommand = new AsyncRelayCommand(CreateCustomFoodAsync);
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
        MealIngredients.CollectionChanged += (_, _) => NotifyMealIngredientProperties();
    }

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
        ? "Phase focus"
        : $"Phase focus · {CurrentPhaseName}";

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
    public bool HasBreakfastSuggestions => BreakfastSuggestions.Count > 0;
    public bool HasLunchSuggestions => LunchSuggestions.Count > 0;
    public bool HasDinnerSuggestions => DinnerSuggestions.Count > 0;
    public bool HasSnackSuggestions => SnackSuggestions.Count > 0;
    public bool HasAnyMealIdeas => HasBreakfastSuggestions || HasLunchSuggestions ||
                                   HasDinnerSuggestions || HasSnackSuggestions;
    public bool IsSavouryBreakfast => BreakfastPreference == BreakfastPreference.Savoury;
    public bool IsSweetBreakfast => BreakfastPreference == BreakfastPreference.Sweet;

    public string BreakfastTargetText => $"Breakfast ({(int)(TargetCalories * (IsSweetBreakfast ? 0.20f : 0.25f))} kcal)";
    public string LunchTargetText => $"Lunch ({(int)(TargetCalories * (IsSweetBreakfast ? 0.32f : 0.35f))} kcal)";
    public string DinnerTargetText => $"Dinner ({(int)(TargetCalories * (IsSweetBreakfast ? 0.28f : 0.27f))} kcal)";
    public string SnackTargetText => $"Snack ({(int)(TargetCalories * (IsSweetBreakfast ? 0.20f : 0.13f))} kcal)";
    public bool HasMeals => Meals.Count > 0;
    public bool HasNoMeals => Meals.Count == 0;
    public string AddMealPanelTitle => IsEditingMeal ? "Edit meal" : "Build meal";

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

    public string LogMealButtonText => HasMealIngredients
        ? IsEditingMeal ? "Save meal" : "Log meal"
        : "Add ingredients first";

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

    public void Invalidate() => _loadGate.MarkStale();

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
    public bool IsPageLoading => IsBusy && !_loadGate.HasLoaded;

    private Task RefreshAsync()
    {
        return _loadGate.RunAsync(LoadDataCoreAsync, true);
    }

    private async Task LoadDataCoreAsync()
    {
        IsBusy = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var cycleService = scope.ServiceProvider.GetRequiredService<ICycleService>();
            var nutritionService = scope.ServiceProvider.GetRequiredService<INutritionService>();

            var userId = await DataLoadScheduler.RunAsync(authService.GetCurrentUserIdAsync);
            var phase = await DataLoadScheduler.RunAsync(() => cycleService.GetCurrentPhaseAsync(userId));

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

            var plan = await DataLoadScheduler.RunAsync(() =>
                nutritionService.GetDailyPlanAsync(userId, phase, DateTime.Today, BreakfastPreference));
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
                var (calories, protein, carbs, fats) =
                    await DataLoadScheduler.RunAsync(() =>
                        nutritionService.CalculateDailyTargetsAsync(userId, phase));
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

            var consumed = await LoadMealsAsync(userId);
            ConsumedCalories = consumed.Calories;
            ConsumedProtein = consumed.Protein;
            ConsumedCarbs = consumed.Carbs;
            ConsumedFats = consumed.Fats;
            NotifyDisplayProperties();
            IsBusy = false;

            await LoadMealIdeasAsync(userId, phase);
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
