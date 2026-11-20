using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Contracts.Onboarding;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class ProfileSetupViewModel : ObservableObject
{
    private const int InchesPerFoot = 12;
    private const float CentimetersPerInch = 2.54f;
    private const float KilogramsPerPound = 0.453592f;

    private readonly Func<Task> _navigateToQuizAsync;
    private readonly IProfileSetupUseCase _useCase;
    [ObservableProperty] private DateTime _birthDate = DateTime.Today.AddYears(-18);
    private bool _birthDateWasSelected;
    private bool _birthDatePickerFocused;
    [ObservableProperty] private bool _birthDateConfirmed;
    private bool _loadingBirthDate;
    [ObservableProperty] private int _cycleLength = 28;
    [ObservableProperty] private string _errorMessage = string.Empty;
    private bool _hasLoadedProfile;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _profileImagePath = string.Empty;
    [ObservableProperty] private int _selectedFeet;
    [ObservableProperty] private SelectionOption<UserGoal>? _selectedGoalOption;

    [ObservableProperty] private int _selectedHeightCm;
    [ObservableProperty] private int _selectedInches;

    [ObservableProperty] private int _selectedWeightKg;
    [ObservableProperty] private int _selectedWeightLbs;

    // Field-level validation: every gap is reported next to the control that owns it, and all gaps
    // are reported by a single Continue press instead of one per attempt.
    [ObservableProperty] private string _birthDateError = string.Empty;
    [ObservableProperty] private string _heightError = string.Empty;
    [ObservableProperty] private string _weightError = string.Empty;
    [ObservableProperty] private string _strengthActivityError = string.Empty;
    [ObservableProperty] private string _wellnessError = string.Empty;

    /// <summary>Set from the wellness checkbox; the plan may never be built without it.</summary>
    [ObservableProperty] private bool _wellnessAcknowledged;

    [ObservableProperty] private bool _useMetricSystem = true;
    [ObservableProperty] private ObservableCollection<WorkoutActivityOptionItem> _workoutActivityOptions = new();

    public ProfileSetupViewModel(
        IProfileSetupUseCase useCase,
        Func<Task> navigateToQuizAsync)
    {
        _useCase = useCase;
        _navigateToQuizAsync = navigateToQuizAsync;
        ContinueCommand = new AsyncRelayCommand(ContinueAsync, () => !IsBusy);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        ConfirmBirthDateCommand = new RelayCommand(ConfirmBirthDate);
        SelectMetricUnitsCommand = new RelayCommand(() => UseMetricSystem = true);
        SelectImperialUnitsCommand = new RelayCommand(() => UseMetricSystem = false);
        ToggleWorkoutActivityCommand = new RelayCommand<WorkoutActivityOptionItem>(ToggleWorkoutActivity);
        WorkoutActivityOptions = ProfileWorkoutActivityOptions.Build(new HashSet<WorkoutActivityDto>());
    }

    public DateTime MinBirthDate { get; } = DateTime.Today.AddYears(-100);
    public DateTime MaxBirthDate { get; } = DateTime.Today.AddYears(-18);

    public List<int> MetricHeightOptions { get; } = Enumerable.Range(100, 121).ToList();
    public List<int> FeetOptions { get; } = Enumerable.Range(4, 4).ToList();
    public List<int> InchesOptions { get; } = Enumerable.Range(0, 12).ToList();
    public List<int> MetricWeightOptions { get; } = Enumerable.Range(30, 171).ToList();
    public List<int> ImperialWeightOptions { get; } = Enumerable.Range(66, 375).ToList();

    public IReadOnlyList<SelectionOption<UserGoal>> GoalOptions { get; } =
    [
        new(UserGoal.FatLoss, "Fat Loss"),
        new(UserGoal.MuscleTone, "Muscle Tone"),
        new(UserGoal.Strength, "Strength"),
        new(UserGoal.MaintainHealth, "Maintain Health")
    ];

    public bool UseImperialSystem => !UseMetricSystem;

    // Measurements start unanswered so an untouched form can never save the old seeded defaults.
    public bool HasAnsweredHeight => UseMetricSystem ? SelectedHeightCm > 0 : SelectedFeet > 0;
    public bool HasAnsweredWeight => UseMetricSystem ? SelectedWeightKg > 0 : SelectedWeightLbs > 0;

    public IReadOnlyList<WorkoutActivityGroupSection> GroupedWorkoutActivityOptions { get; private set; } = [];

    public string WeightUnit => UseMetricSystem ? "Common_Unit_Kg" : "Common_Unit_Lbs";
    public bool HasProfileImage => !string.IsNullOrWhiteSpace(ProfileImagePath) && File.Exists(ProfileImagePath);
    public bool HasNoProfileImage => !HasProfileImage;
    public string ProfileImageSource => HasProfileImage ? ProfileImagePath : string.Empty;

    public float Height
    {
        get => UseMetricSystem ? SelectedHeightCm : (SelectedFeet * InchesPerFoot + SelectedInches) * CentimetersPerInch;
        set => SelectedHeightCm = (int)value;
    }

    public float Weight
    {
        get => UseMetricSystem ? SelectedWeightKg : SelectedWeightLbs * KilogramsPerPound;
        set => SelectedWeightKg = (int)value;
    }

    public AsyncRelayCommand ContinueCommand { get; }
    public AsyncRelayCommand LoadDataCommand { get; }
    public RelayCommand ConfirmBirthDateCommand { get; }
    public RelayCommand SelectMetricUnitsCommand { get; }
    public RelayCommand SelectImperialUnitsCommand { get; }
    public RelayCommand<WorkoutActivityOptionItem> ToggleWorkoutActivityCommand { get; }

    public AsyncRelayCommand SaveCommand => ContinueCommand;

    private async Task LoadDataAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _useCase.LoadAsync();
            if (result.HasPendingRegistration)
            {
                if (result.Profile is { } staged)
                {
                    Name = staged.Name;
                    ApplyLoadedBirthDate(staged.BirthDate);
                    ProfileImagePath = staged.ProfileImagePath;
                    CycleLength = staged.CycleLength;
                    UseMetricSystem = staged.UseMetricSystem;
                    SelectedHeightCm = (int)staged.HeightCm;
                    SelectedWeightKg = (int)staged.WeightKg;
                    var totalInches = (int)Math.Round(staged.HeightCm / CentimetersPerInch);
                    SelectedFeet = totalInches / InchesPerFoot;
                    SelectedInches = totalInches % InchesPerFoot;
                    SelectedWeightLbs = (int)Math.Round(staged.WeightKg / KilogramsPerPound);

                    var selectedOptions = ProfileWorkoutActivityOptions.Build(
                        staged.SelectedActivityTypes.Select(value => (WorkoutActivityDto)value).ToHashSet());
                    foreach (var option in WorkoutActivityOptions)
                        option.IsSelected = selectedOptions.Any(s => s.ActivityType == option.ActivityType && s.IsSelected);
                }
                else if (!string.IsNullOrWhiteSpace(result.PendingUsername))
                {
                    Name = result.PendingUsername;
                }

                return;
            }

            var profile = result.Profile;
            if (profile is null)
                return;

            Name = profile.Name;
            ApplyLoadedBirthDate(profile.BirthDate);
            ProfileImagePath = profile.ProfileImagePath;
            var selectedActivities = profile.SelectedActivityTypes
                .Select(value => (WorkoutActivityDto)value).ToHashSet();
            var loadedOptions = ProfileWorkoutActivityOptions.Build(selectedActivities);
            foreach (var option in WorkoutActivityOptions)
                option.IsSelected =
                    loadedOptions.Any(loaded => loaded.ActivityType == option.ActivityType && loaded.IsSelected);
            _hasLoadedProfile = true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileSetupViewModel] Load failed ({ex.GetType().Name}).");
            ErrorMessage = "Onboarding_ProfileSetup_LoadError";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        ContinueCommand.NotifyCanExecuteChanged();
    }

    partial void OnUseMetricSystemChanged(bool value)
    {
        if (value)
        {
            if (SelectedHeightCm == 0 && SelectedFeet > 0)
                SelectedHeightCm = Math.Clamp(
                    (int)Math.Round((SelectedFeet * InchesPerFoot + SelectedInches) * CentimetersPerInch), 100, 220);
            if (SelectedWeightKg == 0 && SelectedWeightLbs > 0)
                SelectedWeightKg = Math.Clamp((int)Math.Round(SelectedWeightLbs * KilogramsPerPound), 30, 200);
        }
        else
        {
            if (SelectedFeet == 0 && SelectedHeightCm > 0)
            {
                var totalInches = (int)Math.Round(SelectedHeightCm / CentimetersPerInch);
                SelectedFeet = Math.Clamp(totalInches / InchesPerFoot, 4, 7);
                SelectedInches = Math.Clamp(totalInches % InchesPerFoot, 0, 11);
            }

            if (SelectedWeightLbs == 0 && SelectedWeightKg > 0)
                SelectedWeightLbs = Math.Clamp((int)Math.Round(SelectedWeightKg / KilogramsPerPound), 66, 440);
        }

        OnPropertyChanged(nameof(WeightUnit));
        OnPropertyChanged(nameof(UseImperialSystem));
    }

    partial void OnSelectedHeightCmChanged(int value)
    {
        if (value > 0 && HeightError == HeightRequiredError)
            HeightError = string.Empty;
    }

    partial void OnSelectedFeetChanged(int value)
    {
        if (value > 0 && HeightError == HeightRequiredError)
            HeightError = string.Empty;
    }

    partial void OnSelectedWeightKgChanged(int value)
    {
        if (value > 0 && WeightError == WeightRequiredError)
            WeightError = string.Empty;
    }

    partial void OnSelectedWeightLbsChanged(int value)
    {
        if (value > 0 && WeightError == WeightRequiredError)
            WeightError = string.Empty;
    }

    partial void OnWellnessAcknowledgedChanged(bool value)
    {
        if (value && WellnessError == WellnessRequiredError)
            WellnessError = string.Empty;
    }

    partial void OnBirthDateChanged(DateTime oldValue, DateTime newValue)
    {
        // The iOS date picker pushes its initial value back through the two-way binding; only a
        // real change of calendar date counts as the user answering (never invent an adult age).
        if (!_loadingBirthDate && oldValue.Date != newValue.Date)
            _birthDateWasSelected = true;
    }

    partial void OnBirthDateChanged(DateTime value)
    {
        var today = DateTime.Today;
        var age = today.Year - value.Year;
        if (value.Date > today.AddYears(-age))
            age--;

        if (age < 18)
            BirthDateError = AgeErrorKey;
        // Clear only the age error this handler raised, so a corrected date never leaves it behind.
        else if (BirthDateError == AgeErrorKey)
            BirthDateError = string.Empty;
    }

    private const string AgeErrorKey = "Ui_Onboarding_Error_Age";
    private const string BirthDateRequiredError = "Ui_Onboarding_Error_BirthDateRequired";
    private const string HeightRequiredError = "Ui_Onboarding_Error_HeightRequired";
    private const string WeightRequiredError = "Ui_Onboarding_Error_WeightRequired";
    private const string StrengthRequiredError = "Ui_Onboarding_Error_StrengthRequired";
    private const string WellnessRequiredError = "Onboarding_ProfileSetup_PleaseAcknowledgeTheWellnessNoticeTo";

    /// <summary>
    /// The picker was opened and dismissed. On iOS, closing it with Done on the pre-filled date raises no
    /// DateSelected event, so a user whose 18th birthday is today could otherwise never confirm it.
    /// Only a picker that actually received focus counts: the initial two-way binding also raises
    /// Unfocused on some iOS versions, and that must never count as the user answering.
    /// </summary>
    public void MarkBirthDatePickerFocused()
    {
        if (!_loadingBirthDate)
            _birthDatePickerFocused = true;
    }

    public void MarkBirthDateConfirmedFromPicker()
    {
        if (!_loadingBirthDate && _birthDatePickerFocused)
            MarkBirthDateAnswered();
    }

    public void MarkBirthDateSelected(DateTime? oldDate, DateTime? newDate)
    {
        if (!_loadingBirthDate && _birthDatePickerFocused && newDate is { } picked && oldDate?.Date != picked.Date)
            MarkBirthDateAnswered();
    }

    /// <summary>
    /// Explicit answer for the pre-filled date, so the turning-18-today case never depends on a
    /// platform focus event firing.
    /// </summary>
    private void ConfirmBirthDate()
    {
        if (_loadingBirthDate)
            return;
        MarkBirthDateAnswered();
    }

    private void MarkBirthDateAnswered()
    {
        _birthDateWasSelected = true;
        BirthDateConfirmed = true;
        if (BirthDateError == BirthDateRequiredError)
            BirthDateError = string.Empty;
    }

    /// <summary>True only after the user picked a birth date (or one was loaded from a saved profile).</summary>
    public bool HasSelectedBirthDate => _birthDateWasSelected;

    private void ApplyLoadedBirthDate(DateTime birthDate)
    {
        _loadingBirthDate = true;
        try
        {
            if (birthDate != default)
                BirthDate = birthDate;
            _birthDateWasSelected = birthDate != default;
            BirthDateConfirmed = birthDate != default;
        }
        finally
        {
            _loadingBirthDate = false;
        }
    }

    private async Task ContinueAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            ClearFieldErrors();

            if (!_birthDateWasSelected || BirthDate == default)
                BirthDateError = BirthDateRequiredError;
            if (!HasAnsweredHeight)
                HeightError = HeightRequiredError;
            if (!HasAnsweredWeight)
                WeightError = WeightRequiredError;

            var selectedActivities = WorkoutActivityOptions
                .Where(option => option.IsSelected)
                .Select(option => (int)option.ActivityType)
                .ToArray();

            if (!WorkoutActivityOptions.Any(option => option.IsSelected && WorkoutPlanningConstants.IsStrengthActivity(option.ActivityType)))
                StrengthActivityError = StrengthRequiredError;
            if (!WellnessAcknowledged)
                WellnessError = WellnessRequiredError;

            // Every gap is reported in one pass, beside its own control; nothing is saved while any
            // answer the planner depends on is missing.
            if (BirthDateError.Length > 0 || HeightError.Length > 0 || WeightError.Length > 0 ||
                StrengthActivityError.Length > 0 || WellnessError.Length > 0)
            {
                ErrorMessage = "Onboarding_ProfileSetup_CompleteHighlightedFields";
                return;
            }

            var heightCm = UseMetricSystem
                ? SelectedHeightCm
                : (SelectedFeet * InchesPerFoot + SelectedInches) * CentimetersPerInch;

            var weightKg = UseMetricSystem
                ? SelectedWeightKg
                : SelectedWeightLbs * KilogramsPerPound;

            var result = await _useCase.SaveAndPrepareQuizAsync(new ProfileSetupDraft(
                Name, BirthDate, heightCm, weightKg, ProfileImagePath,
                CycleLength, selectedActivities, UseMetricSystem,
                _hasLoadedProfile));
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ??
                    (result.Error == ProfileSetupError.AccountMissing
                        ? "Ui_Onboarding_Error_SetupFailed"
                        : "Auth_Register_Error_InvalidProfile");
                return;
            }

            await _navigateToQuizAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileSetupViewModel] Setup failed ({ex.GetType().Name}).");
            ErrorMessage = "Ui_Onboarding_Error_SetupFailed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearFieldErrors()
    {
        // The age error belongs to the birthday field and survives a Continue press until fixed.
        if (BirthDateError != AgeErrorKey)
            BirthDateError = string.Empty;
        HeightError = string.Empty;
        WeightError = string.Empty;
        StrengthActivityError = string.Empty;
        WellnessError = string.Empty;
    }

    private void ToggleWorkoutActivity(WorkoutActivityOptionItem? item)
    {
        if (item is null)
            return;

        ErrorMessage = ProfileWorkoutActivityOptions.ToggleSelection(WorkoutActivityOptions, item);
        if (WorkoutActivityOptions.Any(option => option.IsSelected && WorkoutPlanningConstants.IsStrengthActivity(option.ActivityType)))
            StrengthActivityError = string.Empty;
    }

    public void SetProfileImage(string? imagePath)
    {
        ProfileImagePath = string.IsNullOrWhiteSpace(imagePath) ? string.Empty : imagePath.Trim();
        ErrorMessage = string.Empty;
    }

    partial void OnWorkoutActivityOptionsChanged(ObservableCollection<WorkoutActivityOptionItem> value)
    {
        GroupedWorkoutActivityOptions = ProfileWorkoutActivityOptions.BuildGroups(value);
        OnPropertyChanged(nameof(GroupedWorkoutActivityOptions));
    }


    partial void OnProfileImagePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasProfileImage));
        OnPropertyChanged(nameof(HasNoProfileImage));
        OnPropertyChanged(nameof(ProfileImageSource));
    }
}
