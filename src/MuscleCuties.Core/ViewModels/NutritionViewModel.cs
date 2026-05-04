using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Core.ViewModels;

public partial class NutritionViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ICycleService _cycleService;
    private readonly INutritionService _nutritionService;

    [ObservableProperty] private float _targetCalories;
    [ObservableProperty] private float _targetProtein;
    [ObservableProperty] private float _targetCarbs;
    [ObservableProperty] private float _targetFats;
    [ObservableProperty] private float _consumedCalories;
    [ObservableProperty] private float _consumedProtein;
    [ObservableProperty] private float _consumedCarbs;
    [ObservableProperty] private float _consumedFats;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _currentPhaseName = string.Empty;
    [ObservableProperty] private string _phaseFocusCopy = string.Empty;
    // Populated once meal logging is implemented (LoggedMeal repository + NutritionPage log UI)
    [ObservableProperty] private ObservableCollection<MealItem> _meals = new();

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

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    public NutritionViewModel(
        IAuthService authService,
        ICycleService cycleService,
        INutritionService nutritionService)
    {
        _authService = authService;
        _cycleService = cycleService;
        _nutritionService = nutritionService;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        RefreshCommand = LoadDataCommand;
    }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var phase = await _cycleService.GetCurrentPhaseAsync(userId);

            CurrentPhaseName = phase.ToString();
            PhaseFocusCopy = phase switch
            {
                CyclePhase.Menstrual  => "Focus on iron-rich foods and gentle nourishment",
                CyclePhase.Follicular => "Fuel your rising energy with clean carbs and protein",
                CyclePhase.Ovulatory  => "Support peak performance with balanced macros",
                CyclePhase.Luteal     => "Prioritize complex carbs to manage cravings",
                _                     => string.Empty
            };

            var (calories, protein, carbs, fats) = await _nutritionService.CalculateDailyTargetsAsync(userId, phase);
            TargetCalories = calories;
            TargetProtein = protein;
            TargetCarbs = carbs;
            TargetFats = fats;

            ConsumedCalories = await _nutritionService.GetConsumedCaloriesAsync(userId, DateTime.Today);

            var (consumedProtein, consumedCarbs, consumedFats) =
                await _nutritionService.GetConsumedMacrosAsync(userId, DateTime.Today);
            ConsumedProtein = consumedProtein;
            ConsumedCarbs = consumedCarbs;
            ConsumedFats = consumedFats;

            NotifyDisplayProperties();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NotifyDisplayProperties()
    {
        OnPropertyChanged(nameof(CaloriesProgress));
        OnPropertyChanged(nameof(CaloriesConsumed));
        OnPropertyChanged(nameof(CaloriesGoal));
        OnPropertyChanged(nameof(ProteinText));
        OnPropertyChanged(nameof(ProteinProgress));
        OnPropertyChanged(nameof(CarbsText));
        OnPropertyChanged(nameof(CarbsProgress));
        OnPropertyChanged(nameof(FatsText));
        OnPropertyChanged(nameof(FatsProgress));
    }

    partial void OnConsumedCaloriesChanged(float value)
    {
        OnPropertyChanged(nameof(CaloriesProgress));
        OnPropertyChanged(nameof(CaloriesConsumed));
    }

    partial void OnTargetCaloriesChanged(float value)
    {
        OnPropertyChanged(nameof(CaloriesProgress));
        OnPropertyChanged(nameof(CaloriesGoal));
    }
}
