using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UI.Services.Units;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;

namespace MuscleCuties.UI.ViewModels.Profile;

/// <summary>The personal-info page: name and contact, birth date, body measurements in the chosen units, cycle</summary>
/// baselines and the password change, which is saved separately from the profile itself.</summary>
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

    /// <summary>Takes the use cases and the navigation callbacks.</summary>
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

    /// <summary>The first-load indicator; saving or changing the password leaves the loaded form on screen.</summary>
    public bool IsPageLoading => AppPageLoadGate.IsLoading(IsBusy, _hasLoadedOnce);
    /// <summary>True for an edited field or for a partly typed password change, since the passwords are sent on their own</summary>
    /// and would otherwise be lost without a warning.</summary>
    public bool HasUnsavedChanges =>
        _hasLoadedOnce &&
        (!string.Equals(BuildSnapshot(), _initialSnapshot, StringComparison.Ordinal) ||
         !string.IsNullOrWhiteSpace(CurrentPassword) ||
         !string.IsNullOrWhiteSpace(NewPassword) ||
         !string.IsNullOrWhiteSpace(ConfirmNewPassword));

    /// <summary>The reveal button names what it will do to the field it sits on.</summary>
    public string CurrentPasswordToggleText =>
        UiTextCatalog.Get(IsCurrentPasswordHidden ? "Auth_Show" : "Auth_Hide");

    /// <summary>The reveal button's label for the new password field.</summary>
    public string NewPasswordToggleText =>
        UiTextCatalog.Get(IsNewPasswordHidden ? "Auth_Show" : "Auth_Hide");

    /// <summary>The reveal button's label for the confirmation field.</summary>
    public string ConfirmPasswordToggleText =>
        UiTextCatalog.Get(IsConfirmPasswordHidden ? "Auth_Show" : "Auth_Hide");

    /// <summary>Shows or hides the current password.</summary>
    public RelayCommand ToggleCurrentPasswordVisibilityCommand { get; }
    /// <summary>Shows or hides the new password.</summary>
    public RelayCommand ToggleNewPasswordVisibilityCommand { get; }
    /// <summary>Shows or hides the confirmation.</summary>
    public RelayCommand ToggleConfirmPasswordVisibilityCommand { get; }

    /// <summary>Hides all three password fields again, so a revealed password does not stay on screen after the page is</summary>
    /// left or the screen goes away.</summary>
    public void MaskSensitiveInputs()
    {
        IsCurrentPasswordHidden = true;
        IsNewPasswordHidden = true;
        IsConfirmPasswordHidden = true;
    }

    /// <summary>The profile snapshot used to detect changes.</summary>
    private string BuildSnapshot() =>
        $"{Name}|{Email}|{BirthDate:O}|{HeightCm}|{WeightKg}|{CycleLength}|{SelectedPregnancyOption?.Value}|{SelectedContraceptionOption?.Value}";

    /// <summary>Leaves the page, asking first when there are unsaved changes.</summary>
    private async Task BackAsync() =>
        await ProfileNavigationGuard.TryNavigateBackAsync(HasUnsavedChanges, ProfileDiscardPrompt.ConfirmAsync,
            _navigateBackAsync);

    /// <summary>Pace applies only to the goals that use a weight target.</summary>
    public bool IsGoalPaceVisible => UsesWeightGoalPace(Goal);

    /// <summary>The pregnancy answers.</summary>
    public IReadOnlyList<SelectionOption<int>> PregnancyOptions { get; } =
    [
        new(0, "Prefer not to say"),
        new(1, "No"),
        new(2, "Yes")
    ];

    /// <summary>The contraception answers.</summary>
    public IReadOnlyList<SelectionOption<int>> ContraceptionOptions { get; } =
    [
        new(0, "Prefer not to say"),
        new(1, "Regular cycle (no hormonal contraception)"),
        new(2, "Hormonal contraception"),
        new(3, "Irregular cycle")
    ];

    /// <summary>F06: the entry hint names the unit the stored preference displays.</summary>
    public string HeightUnitLabel => UnitsFormatter.HeightUnitLabel(_useMetricSystem);

    /// <summary>The weight unit for this profile's chosen system.</summary>
    public string WeightUnitLabel => UnitsFormatter.WeightUnitLabel(_useMetricSystem);

    /// <summary>The stored height shown in the chosen units, so the summary matches what the fields are entered in.</summary>
    public string HeightMeasurementText => FormatMeasurement(HeightCm, HeightUnitLabel);

    /// <summary>The stored weight in the chosen units.</summary>
    public string WeightMeasurementText => FormatMeasurement(WeightKg, WeightUnitLabel);

    /// <summary>The earliest birth date the form accepts.</summary>
    public DateTime MinBirthDate { get; } = DateTime.Today.AddYears(-100);
    /// <summary>The latest birth date the form accepts.</summary>
    public DateTime MaxBirthDate { get; } = DateTime.Today.AddYears(-18);
    /// <summary>Loads the profile.</summary>
    public AsyncRelayCommand LoadDataCommand { get; }
    /// <summary>Saves the profile.</summary>
    public AsyncRelayCommand SaveCommand { get; }
    /// <summary>Changes the password.</summary>
    public AsyncRelayCommand ChangePasswordCommand { get; }
    /// <summary>Returns to the profile.</summary>
    public AsyncRelayCommand BackCommand { get; }

    /// <summary>Loads the profile and the unit preference it needs to display measurements, and keeps both as the</summary>
    /// baseline the unsaved-changes flag compares against.</summary>
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

    /// <summary>Saves the profile fields; the password change is deliberately not part of this save.</summary>
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

    /// <summary>Changes the password on its own call, and clears the three fields only once it has succeeded.</summary>
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

    /// <summary>Checks the numeric fields against the accepted ranges and hands back the parsed values, so the save path and</summary>
    /// the error messages can never disagree about what was entered.</summary>
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
    /// <summary>A numeric field as a number, with unreadable text counting as zero so the range check reports it.</summary>
    private static float ParseFloat(string value)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var current)
            ? current
            : float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant)
                ? invariant
                : 0f;
    }

    /// <summary>A whole-number field as a number, with the same unreadable-as-zero behaviour.</summary>
    private static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var current)
            ? current
            : 0;
    }

    /// <summary>A stored number as field text, without trailing zeros, so a value does not change when it is loaded and</summary>
    /// saved again.</summary>
    private static string FormatNumber(float value)
    {
        return value <= 0f ? string.Empty : value.ToString("0.#", CultureInfo.CurrentCulture);
    }

    /// <summary>A measurement with the unit it is in, so the summary reads unambiguously in either system.</summary>
    private static string FormatMeasurement(string value, string unitLabel)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed)
            ? DisplayFormat.Measurement(parsed, unitLabel)
            : string.Empty;
    }

    /// <summary>The goals for which a pace choice applies.</summary>
    private static bool UsesWeightGoalPace(UserGoal goal)
    {
        return goal is UserGoal.FatLoss or UserGoal.Strength;
    }

    /// <summary>Keeps the age in step with the birth date.</summary>
    partial void OnBirthDateChanged(DateTime value)
    {
        if (!IsBusy)
            _birthDateWasExplicitlyChanged = true;
    }

    /// <summary>Records that the birth date was chosen: a date picker always holds a value, so its presence alone cannot say</summary>
    /// the user answered.</summary>
    public void MarkBirthDateSelected()
    {
        if (!IsBusy)
            _birthDateWasExplicitlyChanged = true;
    }

    // Body circumference measurements
    [ObservableProperty] private bool _useMetricCircumferenceUnits = true;
    /// <summary>The measurements shown under the profile.</summary>
    [ObservableProperty] private string _circumferenceValueText = string.Empty;
    public System.Collections.ObjectModel.ObservableCollection<MuscleCuties.UseCases.Features.Progress.BodyMeasurements.BodyMeasurementOutputDto> CircumferenceReadings { get; } = [];

    // Fasting session display state
    [ObservableProperty] private MuscleCuties.UseCases.Features.Fasting.FastingSessionOutputDto? _activeFastingSession;
    [ObservableProperty] private DateTimeOffset? _plannedFastingEndUtc;
    [ObservableProperty] private bool _enablePlannedFastingEndReminder;
}
