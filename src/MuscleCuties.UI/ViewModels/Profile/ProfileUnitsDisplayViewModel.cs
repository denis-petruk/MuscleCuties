using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;

namespace MuscleCuties.UI.ViewModels.Profile;

/// <summary>
///     Units and display preferences state.
/// </summary>
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

    /// <summary>Takes the units use case and the navigation callbacks.</summary>
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

    /// <summary>The full-page indicator belongs to the first load only, so switching units does not blank the page.</summary>
    public bool IsPageLoading => AppPageLoadGate.IsLoading(IsBusy, _hasLoadedOnce);
    /// <summary>True when the preference differs from the saved one.</summary>
    public bool HasUnsavedChanges => _hasLoadedOnce && UseMetricSystem != _initialUseMetricSystem;
    /// <summary>True when the preference can be saved.</summary>
    public bool CanSave => !IsBusy;
    /// <summary>Which system is selected, named for the reader.</summary>
    public string UnitSystemText => UiTextCatalog.Get(UseMetricSystem ? "Common_Unit_Metric" : "Common_Unit_Imperial");
    /// <summary>True when imperial units are chosen.</summary>
    public bool UseImperialSystem => !UseMetricSystem;

    /// <summary>Returns to the profile.</summary>
    public AsyncRelayCommand BackCommand { get; }
    /// <summary>Loads the unit preference.</summary>
    public AsyncRelayCommand LoadDataCommand { get; }
    /// <summary>Saves the unit preference.</summary>
    public AsyncRelayCommand SaveCommand { get; }
    /// <summary>Chooses metric units.</summary>
    public RelayCommand SelectMetricUnitsCommand { get; }
    /// <summary>Chooses imperial units.</summary>
    public RelayCommand SelectImperialUnitsCommand { get; }

    /// <summary>Returns to the profile, saving first when the preference changed.</summary>
    private async Task BackAsync() =>
        await ProfileNavigationGuard.TryNavigateBackAsync(HasUnsavedChanges, ProfileDiscardPrompt.ConfirmAsync,
            _navigateBackAsync);

    /// <summary>Reads the saved unit system and keeps it as the baseline the unsaved-changes flag compares against.</summary>
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

    /// <summary>Saves the chosen system and moves the baseline, so the page stops reporting unsaved changes once it has</summary>
    /// succeeded.</summary>
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

    /// <summary>Refreshes the unit flags when the system changes.</summary>
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
