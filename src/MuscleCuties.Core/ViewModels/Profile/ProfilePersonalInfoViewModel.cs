using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.UI.Common;
using MuscleCuties.Core.Models.UI.Profile;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Health;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfilePersonalInfoViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly IHealthSyncService? _healthSyncService;
    private readonly Action _navigateBack;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private DateTime _birthDate = DateTime.Today.AddYears(-25);
    [ObservableProperty] private string _heightCm = string.Empty;
    [ObservableProperty] private string _weightKg = string.Empty;
    [ObservableProperty] private UserGoal _goal;
    [ObservableProperty] private WeightGoalPace _weightGoalPace;
    [ObservableProperty] private TrainingExperienceLevel _trainingExperienceLevel = TrainingExperienceLevel.Beginner;
    [ObservableProperty] private CycleTrackingMode _cycleTrackingMode = CycleTrackingMode.ManualPhaseLogging;
    [ObservableProperty] private SelectionOption<CycleTrackingMode>? _selectedCycleLoggingModeOption;
    [ObservableProperty] private string _workoutDaysPerWeek = "3";
    [ObservableProperty] private string _cycleLength = "28";
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _healthSyncStatusText = "Not connected";
    [ObservableProperty] private string _healthSyncMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isHealthSyncBusy;

    public bool IsGoalPaceVisible => ProfileSelectionOptions.UsesWeightGoalPace(Goal);
    public IReadOnlyList<SelectionOption<CycleTrackingMode>> CycleLoggingModeOptions { get; } =
        ProfileSelectionOptions.CycleLoggingModes;
    public DateTime MinBirthDate { get; } = DateTime.Today.AddYears(-100);
    public DateTime MaxBirthDate { get; } = DateTime.Today.AddYears(-12);

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand ConnectAppleHealthCommand { get; }
    public AsyncRelayCommand ConnectWhoopCommand { get; }
    public RelayCommand BackCommand { get; }

    public ProfilePersonalInfoViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        Action navigateBack,
        IHealthSyncService? healthSyncService = null)
    {
        _authService = authService;
        _userRepository = userRepository;
        _healthSyncService = healthSyncService;
        _navigateBack = navigateBack;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ConnectAppleHealthCommand = new AsyncRelayCommand(() => ConnectHealthAsync(HealthDataSource.AppleHealth));
        ConnectWhoopCommand = new AsyncRelayCommand(() => ConnectHealthAsync(HealthDataSource.Whoop));
        BackCommand = new RelayCommand(_navigateBack);
        SelectedCycleLoggingModeOption = CycleLoggingModeOptions.First(option => option.Value == CycleTrackingMode);
    }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var user = await _userRepository.GetByIdAsync(userId);
            var profile = await _userRepository.GetProfileAsync(userId);
            await RefreshHealthSyncStatusAsync(userId);

            Email = user?.Email ?? string.Empty;

            if (profile is null)
                return;

            Name = profile.Name;
            BirthDate = profile.DateOfBirth == default ? DateTime.Today.AddYears(-25) : profile.DateOfBirth;
            HeightCm = FormatNumber(profile.Height);
            WeightKg = FormatNumber(profile.Weight);
            Goal = profile.Goal;
            WeightGoalPace = ProfileSelectionOptions.UsesWeightGoalPace(profile.Goal)
                ? profile.WeightGoalPace
                : WeightGoalPace.Steady;
            TrainingExperienceLevel = profile.TrainingExperienceLevel is TrainingExperienceLevel.Unknown
                ? TrainingExperienceLevel.Beginner
                : profile.TrainingExperienceLevel;
            CycleTrackingMode = NormalizeCycleLoggingMode(profile.CycleTrackingMode);
            SelectedCycleLoggingModeOption = CycleLoggingModeOptions.First(option => option.Value == CycleTrackingMode);
            WorkoutDaysPerWeek = profile.WorkoutDaysPerWeek.ToString(CultureInfo.CurrentCulture);
            CycleLength = (profile.CycleLength > 0 ? profile.CycleLength : 28).ToString(CultureInfo.CurrentCulture);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        if (!TryValidate(out var height, out var weight, out var workoutDays, out var cycleLength))
            return;

        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var user = await _userRepository.GetByIdAsync(userId);
            var profile = await _userRepository.GetProfileAsync(userId);
            var isNewProfile = profile is null;
            var email = Email.Trim().ToLowerInvariant();

            if (user is not null && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = email;
                user.UpdatedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);
            }

            profile ??= new UserProfile { UserId = userId };
            profile.Name = Name.Trim();
            profile.DateOfBirth = BirthDate.Date;
            profile.Height = height;
            profile.Weight = weight;
            profile.Goal = Goal;
            profile.WeightGoalPace = IsGoalPaceVisible ? WeightGoalPace : WeightGoalPace.Steady;
            profile.TrainingExperienceLevel = TrainingExperienceLevel;
            profile.CycleTrackingMode = NormalizeCycleLoggingMode(CycleTrackingMode);
            profile.WorkoutDaysPerWeek = workoutDays;
            profile.CycleLength = cycleLength;
            profile.UpdatedAt = DateTime.UtcNow;

            if (isNewProfile)
                await _userRepository.AddProfileAsync(profile);
            else
                await _userRepository.UpdateProfileAsync(profile);

            await _userRepository.AddSnapshotAsync(new UserProfileSnapshot
            {
                UserId = userId,
                SnapshotReason = "PersonalInfoUpdate",
                ProfileJson = JsonSerializer.Serialize(new
                {
                    profile.Name,
                    Email = email,
                    profile.DateOfBirth,
                    profile.Height,
                    profile.Weight,
                    Goal = profile.Goal.ToString(),
                    WeightGoalPace = profile.WeightGoalPace.ToString(),
                    TrainingExperienceLevel = profile.TrainingExperienceLevel.ToString(),
                    CycleTrackingMode = profile.CycleTrackingMode.ToString(),
                    profile.WorkoutDaysPerWeek,
                    profile.CycleLength
                }),
                CreatedAt = DateTime.UtcNow
            });

            StatusMessage = "Personal info saved.";
            _navigateBack();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryValidate(out float height, out float weight, out int workoutDays, out int cycleLength)
    {
        height = ParseFloat(HeightCm);
        weight = ParseFloat(WeightKg);
        workoutDays = ParseInt(WorkoutDaysPerWeek);
        cycleLength = ParseInt(CycleLength);

        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "Enter your name.";
            return false;
        }

        if (!AuthInputValidator.IsValidEmail(Email))
        {
            StatusMessage = "Enter a valid email.";
            return false;
        }

        if (height <= 0f || weight <= 0f)
        {
            StatusMessage = "Enter valid height and weight.";
            return false;
        }

        if (workoutDays is < 0 or > 7)
        {
            StatusMessage = "Workout days must be between 0 and 7.";
            return false;
        }

        if (cycleLength is < 1 or > 60)
        {
            StatusMessage = "Cycle length must be between 1 and 60 days.";
            return false;
        }

        StatusMessage = string.Empty;
        return true;
    }

    private static float ParseFloat(string value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var current)
            ? current
            : float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant)
                ? invariant
                : 0f;

    private static int ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var current)
            ? current
            : 0;

    private static string FormatNumber(float value) =>
        value <= 0f ? string.Empty : value.ToString("0.#", CultureInfo.CurrentCulture);

    private static CycleTrackingMode NormalizeCycleLoggingMode(CycleTrackingMode mode) => mode switch
    {
        CycleTrackingMode.FloConnector => CycleTrackingMode.FloConnector,
        CycleTrackingMode.LunarConnector => CycleTrackingMode.LunarConnector,
        _ => CycleTrackingMode.ManualPhaseLogging
    };

    private async Task ConnectHealthAsync(HealthDataSource source)
    {
        if (_healthSyncService is null)
        {
            HealthSyncMessage = "Health sync is not available in this build.";
            return;
        }

        IsHealthSyncBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var result = await _healthSyncService.SyncAsync(userId, source);
            HealthSyncMessage = result.Message;
            await RefreshHealthSyncStatusAsync(userId);
        }
        finally
        {
            IsHealthSyncBusy = false;
        }
    }

    private async Task RefreshHealthSyncStatusAsync(int userId)
    {
        if (_healthSyncService is null)
            return;

        var status = await _healthSyncService.GetStatusAsync(userId);
        HealthSyncStatusText = status.SummaryText;
    }

    partial void OnCycleTrackingModeChanged(CycleTrackingMode value)
    {
        var normalized = NormalizeCycleLoggingMode(value);
        var selected = CycleLoggingModeOptions.FirstOrDefault(option => option.Value == normalized);
        if (selected is not null && SelectedCycleLoggingModeOption?.Value != normalized)
            SelectedCycleLoggingModeOption = selected;
    }

    partial void OnSelectedCycleLoggingModeOptionChanged(SelectionOption<CycleTrackingMode>? value)
    {
        if (value is not null && CycleTrackingMode != value.Value)
            CycleTrackingMode = value.Value;
    }
}
