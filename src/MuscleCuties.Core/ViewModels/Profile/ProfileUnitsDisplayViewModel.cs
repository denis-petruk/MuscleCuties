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
    private readonly IUserRepository _userRepository;
    private readonly Action _navigateBack;

    [ObservableProperty] private bool _useMetricSystem = true;
    [ObservableProperty] private string _bodyWeightUnit = "kg";
    [ObservableProperty] private string _foodMassUnit = "g";
    [ObservableProperty] private string _heightUnit = "cm";
    [ObservableProperty] private string _distanceUnit = "km";
    [ObservableProperty] private string _energyUnit = "kcal";
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public string UnitSystemText => UseMetricSystem ? "Metric defaults" : "Imperial and US defaults";

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand BackCommand { get; }

    public ProfileUnitsDisplayViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        Action navigateBack)
    {
        _authService = authService;
        _userRepository = userRepository;
        _navigateBack = navigateBack;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        BackCommand = new RelayCommand(_navigateBack);
    }

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
            BodyWeightUnit = string.IsNullOrWhiteSpace(profile.BodyWeightUnit) ? BodyWeightUnit : profile.BodyWeightUnit;
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
            _navigateBack();
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
    }

    private static string NormalizeUnit(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
