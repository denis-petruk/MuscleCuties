using MuscleCuties.Contracts.Onboarding;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Profile;

/// <summary>The first-run setup form: height and weight in the chosen system, birth date, goal and activity, saved as</summary>
/// the profile before the app opens.</summary>
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

    /// <summary>Takes the setup use case and the quiz navigation callback.</summary>
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

    /// <summary>The earliest birth date the form accepts.</summary>
    public DateTime MinBirthDate { get; } = DateTime.Today.AddYears(-100);
    /// <summary>The latest birth date the form accepts.</summary>
    public DateTime MaxBirthDate { get; } = DateTime.Today.AddYears(-18);

    /// <summary>The height values in centimetres.</summary>
    public List<int> MetricHeightOptions { get; } = Enumerable.Range(100, 121).ToList();
    /// <summary>The feet values.</summary>
    public List<int> FeetOptions { get; } = Enumerable.Range(4, 4).ToList();
    /// <summary>The inch values.</summary>
    public List<int> InchesOptions { get; } = Enumerable.Range(0, 12).ToList();
    /// <summary>The weight values in kilograms.</summary>
    public List<int> MetricWeightOptions { get; } = Enumerable.Range(30, 171).ToList();
    /// <summary>The weight values in pounds.</summary>
    public List<int> ImperialWeightOptions { get; } = Enumerable.Range(66, 375).ToList();

    /// <summary>Selectable goals with their localized labels.</summary>
    public IReadOnlyList<SelectionOption<UserGoal>> GoalOptions { get; } =
    [
        new(UserGoal.FatLoss, "Fat Loss"),
        new(UserGoal.MuscleTone, "Muscle Tone"),
        new(UserGoal.Strength, "Strength"),
        new(UserGoal.MaintainHealth, "Maintain Health")
    ];

    /// <summary>True when imperial units are chosen.</summary>
    public bool UseImperialSystem => !UseMetricSystem;

    // Measurements start unanswered so an untouched form can never save the old seeded defaults.
    /// <summary>Height only counts as answered once a value above zero is entered, so an untouched form can never save the</summary>
    /// seeded defaults.</summary>
    public bool HasAnsweredHeight => UseMetricSystem ? SelectedHeightCm > 0 : SelectedFeet > 0;
    /// <summary>The same rule for weight, which also starts unanswered.</summary>
    public bool HasAnsweredWeight => UseMetricSystem ? SelectedWeightKg > 0 : SelectedWeightLbs > 0;

    /// <summary>The activity choices grouped into their sections for the picker, rebuilt whenever the selection changes.</summary>
    public IReadOnlyList<WorkoutActivityGroupSection> GroupedWorkoutActivityOptions { get; private set; } = [];

    /// <summary>The resource key for the weight unit this system enters.</summary>
    public string WeightUnit => UseMetricSystem ? "Common_Unit_Kg" : "Common_Unit_Lbs";
    /// <summary>An avatar is shown only while its file still exists, so a deleted picture does not leave an empty frame.</summary>
    public bool HasProfileImage => !string.IsNullOrWhiteSpace(ProfileImagePath) && File.Exists(ProfileImagePath);
    /// <summary>True when no photo was picked.</summary>
    public bool HasNoProfileImage => !HasProfileImage;
    /// <summary>The avatar path when there is a usable one, empty when there is not.</summary>
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

    /// <summary>Saves the profile and continues.</summary>
    public AsyncRelayCommand ContinueCommand { get; }
    /// <summary>Loads the setup draft.</summary>
    public AsyncRelayCommand LoadDataCommand { get; }
    /// <summary>Accepts the chosen birth date.</summary>
    public RelayCommand ConfirmBirthDateCommand { get; }
    /// <summary>Chooses metric units.</summary>
    public RelayCommand SelectMetricUnitsCommand { get; }
    /// <summary>Chooses imperial units.</summary>
    public RelayCommand SelectImperialUnitsCommand { get; }
    /// <summary>Toggles a workout activity.</summary>
    public RelayCommand<WorkoutActivityOptionItem> ToggleWorkoutActivityCommand { get; }

    /// <summary>Saves the profile.</summary>
    public AsyncRelayCommand SaveCommand => ContinueCommand;

    /// <summary>Fills the form from an existing profile, loading the birth date behind a flag so the picker's own value is</summary>
    /// never counted as the user's answer.</summary>
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

    /// <summary>Refreshes the command states when busy changes.</summary>
    partial void OnIsBusyChanged(bool value)
    {
        ContinueCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Converts the shown height and weight when the units change.</summary>
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

    /// <summary>Keeps the imperial height in step.</summary>
    partial void OnSelectedHeightCmChanged(int value)
    {
        if (value > 0 && HeightError == HeightRequiredError)
            HeightError = string.Empty;
    }

    /// <summary>Keeps the metric height in step.</summary>
    partial void OnSelectedFeetChanged(int value)
    {
        if (value > 0 && HeightError == HeightRequiredError)
            HeightError = string.Empty;
    }

    /// <summary>Keeps the imperial weight in step.</summary>
    partial void OnSelectedWeightKgChanged(int value)
    {
        if (value > 0 && WeightError == WeightRequiredError)
            WeightError = string.Empty;
    }

    /// <summary>Keeps the metric weight in step.</summary>
    partial void OnSelectedWeightLbsChanged(int value)
    {
        if (value > 0 && WeightError == WeightRequiredError)
            WeightError = string.Empty;
    }

    /// <summary>Refreshes the continue state when the acknowledgement changes.</summary>
    partial void OnWellnessAcknowledgedChanged(bool value)
    {
        if (value && WellnessError == WellnessRequiredError)
            WellnessError = string.Empty;
    }

    /// <summary>Keeps the age in step when the birth date changes.</summary>
    partial void OnBirthDateChanged(DateTime oldValue, DateTime newValue)
    {
        // The iOS date picker pushes its initial value back through the two-way binding; only a
        // real change of calendar date counts as the user answering (never invent an adult age).
        if (!_loadingBirthDate && oldValue.Date != newValue.Date)
            _birthDateWasSelected = true;
    }

    /// <summary>Refreshes the age shown for the birth date.</summary>
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

    /// <summary>Treats the picker being closed as an answer, but only when the user actually opened it.</summary>
    public void MarkBirthDateConfirmedFromPicker()
    {
        if (!_loadingBirthDate && _birthDatePickerFocused)
            MarkBirthDateAnswered();
    }

    /// <summary>Treats a changed date as an answer; a programmatic load or re-picking the same day is not one.</summary>
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

    /// <summary>Records that the birth date has been answered, which is what the form's validity check reads.</summary>
    private void MarkBirthDateAnswered()
    {
        _birthDateWasSelected = true;
        BirthDateConfirmed = true;
        if (BirthDateError == BirthDateRequiredError)
            BirthDateError = string.Empty;
    }


    /// <summary>Shows the stored birth date without that counting as a choice.</summary>
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

    /// <summary>Validates the form, saves the profile and moves on; anything refused stays on the page as field errors.</summary>
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

    /// <summary>Drops the per-field errors so a retry starts from a clean form rather than showing stale complaints.</summary>
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

    /// <summary>Turns one activity on or off and keeps the grouped sections in step with the selection.</summary>
    private void ToggleWorkoutActivity(WorkoutActivityOptionItem? item)
    {
        if (item is null)
            return;

        ErrorMessage = ProfileWorkoutActivityOptions.ToggleSelection(WorkoutActivityOptions, item);
        if (WorkoutActivityOptions.Any(option => option.IsSelected && WorkoutPlanningConstants.IsStrengthActivity(option.ActivityType)))
            StrengthActivityError = string.Empty;
    }

    /// <summary>Stores the chosen avatar and republishes everything that depends on whether a picture exists.</summary>
    public void SetProfileImage(string? imagePath)
    {
        ProfileImagePath = string.IsNullOrWhiteSpace(imagePath) ? string.Empty : imagePath.Trim();
        ErrorMessage = string.Empty;
    }

    /// <summary>Follows the activity options when they are replaced.</summary>
    partial void OnWorkoutActivityOptionsChanged(ObservableCollection<WorkoutActivityOptionItem> value)
    {
        GroupedWorkoutActivityOptions = ProfileWorkoutActivityOptions.BuildGroups(value);
        OnPropertyChanged(nameof(GroupedWorkoutActivityOptions));
    }


    /// <summary>Refreshes the photo state when the path changes.</summary>
    partial void OnProfileImagePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasProfileImage));
        OnPropertyChanged(nameof(HasNoProfileImage));
        OnPropertyChanged(nameof(ProfileImageSource));
    }
}
