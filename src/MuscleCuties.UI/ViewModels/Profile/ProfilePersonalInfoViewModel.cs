using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UI.Services.Units;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class ProfilePersonalInfoViewModel : ObservableObject, IPageLoadAware
{
    private readonly IProfilePersonalInfoUseCase _useCase;
    private readonly IProfileUnitsUseCase _units;
    private readonly IAccountPasswordValidator _passwordValidator;
    private readonly IProfileRefreshNotifier _refreshNotifier;

    // F06: height and weight are entered in the saved system and stored in cm/kg.
    private bool _useMetricSystem = true;
    private readonly Func<Task> _navigateBackAsync;
    private bool _hasLoadedOnce;
    private string _initialSnapshot = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;

    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isStatusError;
    private bool _hasStoredBirthDate;
    private bool _birthDateWasExplicitlyChanged;
    [ObservableProperty] private DateTime _birthDate;
    [ObservableProperty] private string _cycleLength = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private UserGoal _goal;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeightMeasurementText))]
    private string _heightCm = string.Empty;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private TrainingExperienceLevel _trainingExperienceLevel = TrainingExperienceLevel.Unknown;
    [ObservableProperty] private WeightGoalPace _weightGoalPace;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeightMeasurementText))]
    private string _weightKg = string.Empty;
    [ObservableProperty] private string _currentPassword = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmNewPassword = string.Empty;
    [ObservableProperty] private string _passwordStatusMessage = string.Empty;
    [ObservableProperty] private bool _isPasswordStatusError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPasswordToggleText))]
    private bool _isCurrentPasswordHidden = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewPasswordToggleText))]
    private bool _isNewPasswordHidden = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfirmPasswordToggleText))]
    private bool _isConfirmPasswordHidden = true;

    [ObservableProperty] private SelectionOption<int>? _selectedPregnancyOption;
    [ObservableProperty] private SelectionOption<int>? _selectedContraceptionOption;

    public ProfilePersonalInfoViewModel(
        IProfilePersonalInfoUseCase useCase,
        IAccountPasswordValidator passwordValidator,
        IProfileRefreshNotifier refreshNotifier,
        IProfileUnitsUseCase units,
        Func<Task> navigateBackAsync,
        BodyMeasurementClient? measurementClient = null)
    {
        _useCase = useCase;
        _units = units;
        _passwordValidator = passwordValidator;
        _refreshNotifier = refreshNotifier;
        _navigateBackAsync = navigateBackAsync;
        _measurementClient = measurementClient;
        BackCommand = new AsyncRelayCommand(BackAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ChangePasswordCommand = new AsyncRelayCommand(ChangePasswordAsync);
        ToggleCurrentPasswordVisibilityCommand = new RelayCommand(() => IsCurrentPasswordHidden = !IsCurrentPasswordHidden);
        ToggleNewPasswordVisibilityCommand = new RelayCommand(() => IsNewPasswordHidden = !IsNewPasswordHidden);
        ToggleConfirmPasswordVisibilityCommand = new RelayCommand(() => IsConfirmPasswordHidden = !IsConfirmPasswordHidden);
        SelectedPregnancyOption = PregnancyOptions[0];
        SelectedContraceptionOption = ContraceptionOptions[0];
    }

    public bool IsPageLoading => IsBusy && !_hasLoadedOnce;
    public bool HasUnsavedChanges =>
        _hasLoadedOnce &&
        (!string.Equals(BuildSnapshot(), _initialSnapshot, StringComparison.Ordinal) ||
         !string.IsNullOrWhiteSpace(CurrentPassword) ||
         !string.IsNullOrWhiteSpace(NewPassword) ||
         !string.IsNullOrWhiteSpace(ConfirmNewPassword));

    public string CurrentPasswordToggleText =>
        UiTextCatalog.Get(IsCurrentPasswordHidden ? "Auth_Show" : "Auth_Hide");

    public string NewPasswordToggleText =>
        UiTextCatalog.Get(IsNewPasswordHidden ? "Auth_Show" : "Auth_Hide");

    public string ConfirmPasswordToggleText =>
        UiTextCatalog.Get(IsConfirmPasswordHidden ? "Auth_Show" : "Auth_Hide");

    public RelayCommand ToggleCurrentPasswordVisibilityCommand { get; }
    public RelayCommand ToggleNewPasswordVisibilityCommand { get; }
    public RelayCommand ToggleConfirmPasswordVisibilityCommand { get; }

    public void MaskSensitiveInputs()
    {
        IsCurrentPasswordHidden = true;
        IsNewPasswordHidden = true;
        IsConfirmPasswordHidden = true;
    }

    private string BuildSnapshot() =>
        $"{Name}|{Email}|{BirthDate:O}|{HeightCm}|{WeightKg}|{CycleLength}|{SelectedPregnancyOption?.Value}|{SelectedContraceptionOption?.Value}";

    private async Task BackAsync() =>
        await ProfileNavigationGuard.TryNavigateBackAsync(HasUnsavedChanges, ProfileDiscardPrompt.ConfirmAsync,
            _navigateBackAsync);

    public bool IsGoalPaceVisible => UsesWeightGoalPace(Goal);

    public IReadOnlyList<SelectionOption<int>> PregnancyOptions { get; } =
    [
        new(0, "Prefer not to say"),
        new(1, "No"),
        new(2, "Yes")
    ];

    public IReadOnlyList<SelectionOption<int>> ContraceptionOptions { get; } =
    [
        new(0, "Prefer not to say"),
        new(1, "Regular cycle (no hormonal contraception)"),
        new(2, "Hormonal contraception"),
        new(3, "Irregular cycle")
    ];

    /// <summary>F06: the entry hint names the unit the stored preference displays.</summary>
    public string HeightUnitLabel => UnitsFormatter.HeightUnitLabel(_useMetricSystem);

    public string WeightUnitLabel => UnitsFormatter.WeightUnitLabel(_useMetricSystem);

    public string HeightMeasurementText => FormatMeasurement(HeightCm, HeightUnitLabel);

    public string WeightMeasurementText => FormatMeasurement(WeightKg, WeightUnitLabel);

    public DateTime MinBirthDate { get; } = DateTime.Today.AddYears(-100);
    public DateTime MaxBirthDate { get; } = DateTime.Today.AddYears(-18);
    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand ChangePasswordCommand { get; }
    public AsyncRelayCommand BackCommand { get; }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        IsLoadError = false;
        StatusMessage = string.Empty;
        IsStatusError = false;
        PasswordStatusMessage = string.Empty;
        IsPasswordStatusError = false;
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmNewPassword = string.Empty;
        MaskSensitiveInputs();
        _hasStoredBirthDate = false;
        _birthDateWasExplicitlyChanged = false;
        try
        {
            var result = await _useCase.LoadAsync();
            Email = result.Email;
            var profile = result.Profile;

            if (profile is null)
            {
                _hasLoadedOnce = true;
                _initialSnapshot = BuildSnapshot();
                return;
            }

            var units = await _units.LoadAsync();
            _useMetricSystem = units.Settings?.UseMetricSystem ?? _useMetricSystem;
            OnPropertyChanged(nameof(HeightUnitLabel));
            OnPropertyChanged(nameof(WeightUnitLabel));
            OnPropertyChanged(nameof(HeightMeasurementText));
            OnPropertyChanged(nameof(WeightMeasurementText));

            Name = profile.Name;
            _hasStoredBirthDate = profile.DateOfBirth != default;
            // Mirror the onboarding picker: an unset date stays unset instead of showing an
            // invented adult date that the user never chose.
            if (_hasStoredBirthDate)
                BirthDate = profile.DateOfBirth;
            _birthDateWasExplicitlyChanged = false;
            HeightCm = FormatNumber((float)UnitsFormatter.ToDisplayHeight(profile.Height, _useMetricSystem));
            WeightKg = FormatNumber((float)UnitsFormatter.ToDisplayWeight(profile.Weight, _useMetricSystem));
            Goal = (UserGoal)profile.Goal;
            WeightGoalPace = UsesWeightGoalPace(Goal)
                ? (WeightGoalPace)profile.WeightGoalPace
                : WeightGoalPace.Steady;
            TrainingExperienceLevel = (TrainingExperienceLevel)profile.TrainingExperienceLevel;
            CycleLength = profile.CycleLength > 0
                ? profile.CycleLength.ToString(CultureInfo.CurrentCulture)
                : string.Empty;
            SelectedPregnancyOption = PregnancyOptions.FirstOrDefault(o => o.Value == profile.PregnancyStatus) ??
                                      PregnancyOptions[0];
            SelectedContraceptionOption =
                ContraceptionOptions.FirstOrDefault(o => o.Value == profile.ContraceptionStatus) ??
                ContraceptionOptions[0];
            _hasLoadedOnce = true;
            _initialSnapshot = BuildSnapshot();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfilePersonalInfoViewModel] Load failed ({ex.GetType().Name}).");
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

        if (!TryValidate(out var height, out var weight, out var cycleLength))
            return;

        IsBusy = true;
        IsStatusError = false;
        try
        {
            var result = await _useCase.SaveAsync(new SavePersonalInfoRequest(
                Email, Name, BirthDate, height, weight,
                (int)Goal, (int)(IsGoalPaceVisible ? WeightGoalPace : WeightGoalPace.Steady),
                (int)TrainingExperienceLevel, cycleLength,
                SelectedPregnancyOption?.Value ?? 0,
                SelectedContraceptionOption?.Value ?? 0));
            if (!result.Succeeded)
            {
                IsStatusError = true;
                StatusMessage = result.Error switch
                {
                    PersonalInfoSaveError.AccountMissing => "Active account not found.",
                    PersonalInfoSaveError.InvalidBirthDate => "Please provide your date of birth.",
                    PersonalInfoSaveError.Underage =>
                        $"Please provide a valid date of birth (must be at least {PersonalInfoConstraints.MinimumEligibleAgeYears} years old).",
                    _ => "Check your personal info and try again."
                };
                return;
            }

            _refreshNotifier.InvalidateAll();
            _initialSnapshot = BuildSnapshot();
            IsStatusError = false;
            StatusMessage = "Ui_Profile_Status_PersonalInfoSaved";
            await _navigateBackAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfilePersonalInfoViewModel] Save failed ({ex.GetType().Name}).");
            IsStatusError = true;
            StatusMessage = UiTextCatalog.Get("Profile_PersonalInfo_SaveFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ChangePasswordAsync()
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            IsPasswordStatusError = true;
            PasswordStatusMessage = "Ui_Profile_Status_CurrentPasswordRequired";
            return;
        }

        if (!_passwordValidator.TryValidateConfirmation(NewPassword, ConfirmNewPassword, out var passwordError))
        {
            IsPasswordStatusError = true;
            PasswordStatusMessage = passwordError;
            return;
        }

        IsBusy = true;
        IsPasswordStatusError = false;
        try
        {
            var result = await _useCase.ChangePasswordAsync(
                new ChangeAccountPasswordRequest(CurrentPassword, NewPassword));
            if (result.Succeeded)
            {
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmNewPassword = string.Empty;
                MaskSensitiveInputs();
            }

            IsPasswordStatusError = !result.Succeeded;
            PasswordStatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfilePersonalInfoViewModel] ChangePassword failed ({ex.GetType().Name}).");
            IsPasswordStatusError = true;
            PasswordStatusMessage = UiTextCatalog.Get("Profile_PersonalInfo_PasswordChangeFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryValidate(out float height, out float weight, out int cycleLength)
    {
        height = (float)UnitsFormatter.FromDisplayHeight(ParseFloat(HeightCm), _useMetricSystem);
        weight = (float)UnitsFormatter.FromDisplayWeight(ParseFloat(WeightKg), _useMetricSystem);
        cycleLength = ParseInt(CycleLength);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
            errors.Add(UiTextCatalog.Get("Ui_Profile_Error_NameRequired"));

        if (!_passwordValidator.IsValidEmail(Email))
            errors.Add(UiTextCatalog.Get("Ui_Profile_Error_EmailInvalid"));

        if (!_hasStoredBirthDate && !_birthDateWasExplicitlyChanged)
            errors.Add(UiTextCatalog.Get("Ui_Profile_Error_BirthDateRequired"));

        if (height <= 0f || weight <= 0f)
        {
            errors.Add(UiTextCatalog.Get("Ui_Profile_Error_HeightWeightInvalid"));
        }
        else
        {
            if (height is < 100f or > 250f)
                errors.Add(UiTextCatalog.Get("Profile_PersonalInfo_HeightBoundsError"));
            if (weight is < 30f or > 300f)
                errors.Add(UiTextCatalog.Get("Profile_PersonalInfo_WeightBoundsError"));
        }

        if (cycleLength is < 0 or > 60 ||
            (!string.IsNullOrWhiteSpace(CycleLength) && cycleLength == 0))
            errors.Add(UiTextCatalog.Get("Ui_Profile_Error_CycleLength"));

        if (errors.Count > 0)
        {
            IsStatusError = true;
            StatusMessage = string.Join(" ", errors);
            return false;
        }

        IsStatusError = false;
        StatusMessage = string.Empty;
        return true;
    }

    // Parses user-entered decimals using current locale first, falling back to invariant "." separator.
    private static float ParseFloat(string value)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var current)
            ? current
            : float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant)
                ? invariant
                : 0f;
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var current)
            ? current
            : 0;
    }

    private static string FormatNumber(float value)
    {
        return value <= 0f ? string.Empty : value.ToString("0.#", CultureInfo.CurrentCulture);
    }

    private static string FormatMeasurement(string value, string unitLabel)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed)
            ? DisplayFormat.Measurement(parsed, unitLabel)
            : string.Empty;
    }

    private static bool UsesWeightGoalPace(UserGoal goal)
    {
        return goal is UserGoal.FatLoss or UserGoal.Strength;
    }

    partial void OnBirthDateChanged(DateTime value)
    {
        if (!IsBusy)
            _birthDateWasExplicitlyChanged = true;
    }

    public void MarkBirthDateSelected()
    {
        if (!IsBusy)
            _birthDateWasExplicitlyChanged = true;
    }

    // Body circumference measurements
    [ObservableProperty] private bool _useMetricCircumferenceUnits = true;
    [ObservableProperty] private string _circumferenceValueText = string.Empty;
    public System.Collections.ObjectModel.ObservableCollection<MuscleCuties.UseCases.Features.Progress.BodyMeasurements.BodyMeasurementOutputDto> CircumferenceReadings { get; } = [];

    // Fasting session display state
    [ObservableProperty] private MuscleCuties.UseCases.Features.Fasting.FastingSessionOutputDto? _activeFastingSession;
    [ObservableProperty] private DateTimeOffset? _plannedFastingEndUtc;
    [ObservableProperty] private bool _enablePlannedFastingEndReminder;
}
