using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class ProfileUnitsDisplayViewModel : ObservableObject
{
    private readonly Func<Task> _navigateBackAsync;
    private readonly IProfileUnitsUseCase _useCase;
    [ObservableProperty] private string _bodyWeightUnit = "kg";
    [ObservableProperty] private string _distanceUnit = "km";
    [ObservableProperty] private string _energyUnit = "kcal";
    [ObservableProperty] private string _foodMassUnit = "g";
    [ObservableProperty] private string _heightUnit = "cm";

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _useMetricSystem = true;

    public ProfileUnitsDisplayViewModel(IProfileUnitsUseCase useCase, Func<Task> navigateBackAsync)
    {
        _useCase = useCase ?? throw new ArgumentNullException(nameof(useCase));
        _navigateBackAsync = navigateBackAsync ?? throw new ArgumentNullException(nameof(navigateBackAsync));
        BackCommand = new AsyncRelayCommand(navigateBackAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        SelectMetricUnitsCommand = new RelayCommand(() => UseMetricSystem = true);
        SelectImperialUnitsCommand = new RelayCommand(() => UseMetricSystem = false);
    }

    public string UnitSystemText => UseMetricSystem ? "Metric defaults" : "Imperial and US defaults";
    public bool UseImperialSystem => !UseMetricSystem;

    public AsyncRelayCommand BackCommand { get; }
    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand SelectMetricUnitsCommand { get; }
    public RelayCommand SelectImperialUnitsCommand { get; }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var result = await _useCase.LoadAsync();
            if (result.Settings is not { } settings)
                return;

            UseMetricSystem = settings.UseMetricSystem;
            BodyWeightUnit = settings.BodyWeightUnit;
            FoodMassUnit = settings.FoodMassUnit;
            HeightUnit = settings.HeightUnit;
            DistanceUnit = settings.DistanceUnit;
            EnergyUnit = settings.EnergyUnit;
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
            var result = await _useCase.SaveAsync(new SaveProfileUnitsRequest(
                UseMetricSystem,
                BodyWeightUnit,
                FoodMassUnit,
                HeightUnit,
                DistanceUnit,
                EnergyUnit));
            if (!result.Succeeded)
            {
                StatusMessage = "Complete personal info before units.";
                return;
            }

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
}
