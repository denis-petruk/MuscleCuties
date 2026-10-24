using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class ProfilePersonalInfoViewModel : ObservableObject
{
    private readonly IProfilePersonalInfoUseCase _useCase;
    private readonly IAccountPasswordValidator _passwordValidator;
    private readonly IProfileRefreshNotifier _refreshNotifier;
    private readonly Func<Task> _navigateBackAsync;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    private bool _hasStoredBirthDate;
    private bool _birthDateWasExplicitlyChanged;
    [ObservableProperty] private DateTime _birthDate = DateTime.Today.AddYears(-PersonalInfoConstraints.MinimumEligibleAgeYears);
    [ObservableProperty] private string _cycleLength = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private UserGoal _goal;
    [ObservableProperty] private string _heightCm = string.Empty;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private TrainingExperienceLevel _trainingExperienceLevel = TrainingExperienceLevel.Unknown;
    [ObservableProperty] private WeightGoalPace _weightGoalPace;
    [ObservableProperty] private string _weightKg = string.Empty;
    [ObservableProperty] private string _currentPassword = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmNewPassword = string.Empty;
    [ObservableProperty] private string _passwordStatusMessage = string.Empty;

    [ObservableProperty] private SelectionOption<int>? _selectedPregnancyOption;
    [ObservableProperty] private SelectionOption<int>? _selectedContraceptionOption;

    public ProfilePersonalInfoViewModel(
        IProfilePersonalInfoUseCase useCase,
        IAccountPasswordValidator passwordValidator,
        IProfileRefreshNotifier refreshNotifier,
        Func<Task> navigateBackAsync)
    {
        _useCase = useCase;
        _passwordValidator = passwordValidator;
        _refreshNotifier = refreshNotifier;
        _navigateBackAsync = navigateBackAsync;
        BackCommand = new AsyncRelayCommand(navigateBackAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ChangePasswordCommand = new AsyncRelayCommand(ChangePasswordAsync);
        SelectedPregnancyOption = PregnancyOptions[0];
        SelectedContraceptionOption = ContraceptionOptions[0];
    }

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

    public DateTime MinBirthDate { get; } = DateTime.Today.AddYears(-100);
    public DateTime MaxBirthDate { get; } = DateTime.Today.AddYears(-18);
    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand ChangePasswordCommand { get; }
    public AsyncRelayCommand BackCommand { get; }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        PasswordStatusMessage = string.Empty;
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmNewPassword = string.Empty;
        _hasStoredBirthDate = false;
        _birthDateWasExplicitlyChanged = false;
        try
        {
            var result = await _useCase.LoadAsync();
            Email = result.Email;
            var profile = result.Profile;

            if (profile is null)
                return;

            Name = profile.Name;
            _hasStoredBirthDate = profile.DateOfBirth != default;
            BirthDate = _hasStoredBirthDate ? profile.DateOfBirth : MaxBirthDate;
            _birthDateWasExplicitlyChanged = false;
            HeightCm = FormatNumber(profile.Height);
            WeightKg = FormatNumber(profile.Weight);
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
            StatusMessage = "Personal info saved.";
            await _navigateBackAsync();
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
            PasswordStatusMessage = "Enter your current password.";
            return;
        }

        if (!_passwordValidator.TryValidateConfirmation(NewPassword, ConfirmNewPassword, out var passwordError))
        {
            PasswordStatusMessage = passwordError;
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _useCase.ChangePasswordAsync(
                new ChangeAccountPasswordRequest(CurrentPassword, NewPassword));
            if (result.Succeeded)
            {
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmNewPassword = string.Empty;
            }

            PasswordStatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryValidate(out float height, out float weight, out int cycleLength)
    {
        height = ParseFloat(HeightCm);
        weight = ParseFloat(WeightKg);
        cycleLength = ParseInt(CycleLength);

        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "Enter your name.";
            return false;
        }

        if (!_passwordValidator.IsValidEmail(Email))
        {
            StatusMessage = "Enter a valid email.";
            return false;
        }

        if (!_hasStoredBirthDate && !_birthDateWasExplicitlyChanged)
        {
            StatusMessage = "Select your birth date before saving.";
            return false;
        }

        if (height <= 0f || weight <= 0f)
        {
            StatusMessage = "Enter valid height and weight.";
            return false;
        }

        if (cycleLength is < 0 or > 60 ||
            (!string.IsNullOrWhiteSpace(CycleLength) && cycleLength == 0))
        {
            StatusMessage = "Cycle length must be between 1 and 60 days.";
            return false;
        }

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
