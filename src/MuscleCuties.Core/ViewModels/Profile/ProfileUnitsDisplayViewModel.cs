using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfileUnitsDisplayViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly Func<Task> _navigateBackAsync;
    private readonly IUserRepository _userRepository;
    [ObservableProperty] private string _bodyWeightUnit = "kg";
    [ObservableProperty] private string _distanceUnit = "km";
    [ObservableProperty] private string _energyUnit = "kcal";
    [ObservableProperty] private string _foodMassUnit = "g";
    [ObservableProperty] private string _heightUnit = "cm";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private bool _useMetricSystem = true;

    public ProfileUnitsDisplayViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        Func<Task> navigateBackAsync)
    {
        _authService = authService;
        _userRepository = userRepository;
        _navigateBackAsync = navigateBackAsync;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        SelectMetricUnitsCommand = new RelayCommand(() => UseMetricSystem = true);
        SelectImperialUnitsCommand = new RelayCommand(() => UseMetricSystem = false);
        BackCommand = new AsyncRelayCommand(_navigateBackAsync);
    }

    public string UnitSystemText => UseMetricSystem ? "Metric defaults" : "Imperial and US defaults";
    public bool UseImperialSystem => !UseMetricSystem;

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand SelectMetricUnitsCommand { get; }
    public RelayCommand SelectImperialUnitsCommand { get; }
    public AsyncRelayCommand BackCommand { get; }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var profile = await _userRepository.GetProfileAsync(userId);
            if (profile is null)
                return;

            UseMetricSystem = !string.Equals(profile.UnitSystem, "Imperial", StringComparison.OrdinalIgnoreCase);
            BodyWeightUnit = string.IsNullOrWhiteSpace(profile.BodyWeightUnit)
                ? BodyWeightUnit
                : profile.BodyWeightUnit;
            FoodMassUnit = string.IsNullOrWhiteSpace(profile.FoodMassUnit) ? FoodMassUnit : profile.FoodMassUnit;
            HeightUnit = string.IsNullOrWhiteSpace(profile.HeightUnit) ? HeightUnit : profile.HeightUnit;
            DistanceUnit = string.IsNullOrWhiteSpace(profile.DistanceUnit) ? DistanceUnit : profile.DistanceUnit;
            EnergyUnit = string.IsNullOrWhiteSpace(profile.EnergyUnit) ? EnergyUnit : profile.EnergyUnit;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var profile = await _userRepository.GetProfileAsync(userId);
            if (profile is null)
            {
                StatusMessage = "Complete personal info before units.";
                return;
            }

            profile.UnitSystem = UseMetricSystem ? "Metric" : "Imperial";
            profile.BodyWeightUnit = NormalizeUnit(BodyWeightUnit, UseMetricSystem ? "kg" : "lb");
            profile.FoodMassUnit = NormalizeUnit(FoodMassUnit, UseMetricSystem ? "g" : "oz");
            profile.HeightUnit = NormalizeUnit(HeightUnit, UseMetricSystem ? "cm" : "in");
            profile.DistanceUnit = NormalizeUnit(DistanceUnit, UseMetricSystem ? "km" : "mi");
            profile.EnergyUnit = NormalizeUnit(EnergyUnit, "kcal");
            profile.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateProfileAsync(profile);
            await _userRepository.AddSnapshotAsync(new UserProfileSnapshot
            {
                UserId = userId,
                SnapshotReason = "UnitsDisplayUpdate",
                ProfileJson = JsonSerializer.Serialize(new
                {
                    profile.UnitSystem,
                    profile.BodyWeightUnit,
                    profile.FoodMassUnit,
                    profile.HeightUnit,
                    profile.DistanceUnit,
                    profile.EnergyUnit
                }),
                CreatedAt = DateTime.UtcNow
            });

            StatusMessage = "Units saved.";
            await _navigateBackAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnUseMetricSystemChanged(bool value)
    {
        if (value)
        {
            BodyWeightUnit = "kg";
            FoodMassUnit = "g";
            HeightUnit = "cm";
            DistanceUnit = "km";
        }
        else
        {
            BodyWeightUnit = "lb";
            FoodMassUnit = "oz";
            HeightUnit = "in";
            DistanceUnit = "mi";
        }

        EnergyUnit = "kcal";
        OnPropertyChanged(nameof(UnitSystemText));
        OnPropertyChanged(nameof(UseImperialSystem));
    }

    private static string NormalizeUnit(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
