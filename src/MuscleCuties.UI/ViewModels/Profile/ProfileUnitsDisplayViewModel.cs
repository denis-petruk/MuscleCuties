using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class ProfileUnitsDisplayViewModel : ObservableObject, IPageLoadAware
{
    private readonly Func<Task> _navigateBackAsync;
    private readonly IProfileRefreshNotifier? _refreshNotifier;
    private readonly IProfileUnitsUseCase _useCase;
    private bool _hasLoadedOnce;
    private bool _initialUseMetricSystem = true;

    [ObservableProperty] private string _bodyWeightUnit = "kg";
    [ObservableProperty] private string _distanceUnit = "km";
    [ObservableProperty] private string _energyUnit = "kcal";
    [ObservableProperty] private string _foodMassUnit = "g";
    [ObservableProperty] private string _heightUnit = "cm";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageLoading), nameof(CanSave))]
    private bool _isBusy;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isStatusError;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _useMetricSystem = true;

    public ProfileUnitsDisplayViewModel(
        IProfileUnitsUseCase useCase,
        Func<Task> navigateBackAsync,
        IProfileRefreshNotifier? refreshNotifier = null)
    {
        _useCase = useCase ?? throw new ArgumentNullException(nameof(useCase));
        _navigateBackAsync = navigateBackAsync ?? throw new ArgumentNullException(nameof(navigateBackAsync));
        _refreshNotifier = refreshNotifier;
        BackCommand = new AsyncRelayCommand(BackAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        SelectMetricUnitsCommand = new RelayCommand(() => UseMetricSystem = true);
        SelectImperialUnitsCommand = new RelayCommand(() => UseMetricSystem = false);
    }

    public bool IsPageLoading => IsBusy && !_hasLoadedOnce;
    public bool HasUnsavedChanges => _hasLoadedOnce && UseMetricSystem != _initialUseMetricSystem;
    public bool CanSave => !IsBusy;
    public string UnitSystemText => UiTextCatalog.Get(UseMetricSystem ? "Common_Unit_Metric" : "Common_Unit_Imperial");
    public bool UseImperialSystem => !UseMetricSystem;

    public AsyncRelayCommand BackCommand { get; }
    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand SelectMetricUnitsCommand { get; }
    public RelayCommand SelectImperialUnitsCommand { get; }

    private async Task BackAsync()
    {
        if (HasUnsavedChanges && Shell.Current is { } shell)
        {
            var discard = await shell.DisplayAlertAsync(
                UiTextCatalog.Get("Profile_UnsavedChanges_Title"),
                UiTextCatalog.Get("Profile_UnsavedChanges_Message"),
                UiTextCatalog.Get("Profile_UnsavedChanges_Discard"),
                UiTextCatalog.Get("Profile_UnsavedChanges_KeepEditing"));
            if (!discard)
                return;
        }

        await _navigateBackAsync();
    }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        IsLoadError = false;
        StatusMessage = string.Empty;
        IsStatusError = false;
        try
        {
            var result = await _useCase.LoadAsync();
            if (result.Settings is not { } settings)
            {
                _hasLoadedOnce = true;
                _initialUseMetricSystem = UseMetricSystem;
                return;
            }

            UseMetricSystem = settings.UseMetricSystem;
            BodyWeightUnit = settings.BodyWeightUnit;
            FoodMassUnit = settings.FoodMassUnit;
            HeightUnit = settings.HeightUnit;
            DistanceUnit = settings.DistanceUnit;
            EnergyUnit = settings.EnergyUnit;
            _initialUseMetricSystem = UseMetricSystem;
            _hasLoadedOnce = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileUnitsDisplayViewModel] Load failed ({ex.GetType().Name}).");
            IsLoadError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        IsStatusError = false;
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
                IsStatusError = true;
                StatusMessage = "Ui_Profile_Status_CompletePersonalInfo";
                return;
            }

            _initialUseMetricSystem = UseMetricSystem;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            _refreshNotifier?.InvalidateAll();
            IsStatusError = false;
            StatusMessage = "Ui_Profile_Status_UnitsSaved";
            await _navigateBackAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileUnitsDisplayViewModel] Save failed ({ex.GetType().Name}).");
            IsStatusError = true;
            StatusMessage = UiTextCatalog.Get("Profile_Units_SaveFailed");
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
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }
}
