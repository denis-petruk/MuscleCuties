using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UI.Models.UI.Profile;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Profile.Overview;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class ProfileViewModel : ObservableObject, IPageLoadAware, IDisposable
{
    private readonly AppPageLoadGate _loadGate = new(AppPageLoadGate.PageFreshnessWindow);
    private readonly Func<Task> _navigateToLoginAsync;
    private readonly Func<string, Task> _navigateToPreferenceAsync;
    private readonly IProfileRefreshNotifier _refreshNotifier;
    private readonly IProfileOverviewUseCase _useCase;
    private readonly ILocalPhotoCacheService? _photoCache;
    private readonly IProfileHealthConnection? _healthConnection;
    [ObservableProperty] private string _accountActionError = string.Empty;
    [ObservableProperty] private string _activeInjuryBadgeText = "0 Active";
    [ObservableProperty] private int _activeInjuryCount;

    [ObservableProperty] private int _completedSessions;
    [ObservableProperty] private CyclePhase _currentPhase = CyclePhase.Unknown;
    [ObservableProperty] private int _cycleDays = 28;
    [ObservableProperty] private int _cyclesTracked;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private UserGoal _goal;
    [ObservableProperty] private bool _hasActiveInjuries;
    [ObservableProperty] private bool _hasVaultPhotos;

    // Health & Training Section
    [ObservableProperty] private string _healthStatusText = "Tap to Connect";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;

    [ObservableProperty] private bool _isHealthConnected;
    [ObservableProperty] private bool _isLoadError;

    // Private Photo Journey Vault
    [ObservableProperty] private bool _isVaultRevealed;
    [ObservableProperty] private string _latestVaultPhotoPath = string.Empty;
    [ObservableProperty] private string _memberSince = string.Empty;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _nutritionPacingText = "Maintenance";
    [ObservableProperty] private int _nutritionStreakDays;
    [ObservableProperty] private ObservableCollection<PreferenceItem> _preferences = new();
    [ObservableProperty] private string _profileImagePath = string.Empty;
    [ObservableProperty] private string _trainingSplitText = "4 days / week";

    // Account & Preferences Section
    [ObservableProperty] private string _unitsDisplayText = "Metric (kg, cm)";
    [ObservableProperty] private int _vaultPhotoCount;
    [ObservableProperty] private string _vaultPhotoCountText = "0 photos";
    [ObservableProperty] private int _workoutStreakDays;

    public ProfileViewModel(
        IProfileOverviewUseCase useCase,
        IProfileRefreshNotifier refreshNotifier,
        Func<Task> navigateToLoginAsync,
        Func<string, Task>? navigateToPreferenceAsync = null,
        ILocalPhotoCacheService? photoCache = null,
        IProfileHealthConnection? healthConnection = null)
    {
        _useCase = useCase;
        _refreshNotifier = refreshNotifier;
        _navigateToLoginAsync = navigateToLoginAsync;
        _navigateToPreferenceAsync = navigateToPreferenceAsync ?? (_ => Task.CompletedTask);
        _photoCache = photoCache;
        _healthConnection = healthConnection;

        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync, this));
        LogoutCommand = new AsyncRelayCommand(LogoutAsync);
        DeleteAccountAndDataCommand = new AsyncRelayCommand(DeleteAccountAndDataAsync);
        OpenPreferenceCommand = new AsyncRelayCommand<PreferenceItem>(OpenPreferenceAsync);
        OpenRouteCommand = new AsyncRelayCommand<string>(OpenRouteAsync);
        ToggleVaultPrivacyCommand = new RelayCommand(ToggleVaultPrivacy);
        OpenVaultCommand = new AsyncRelayCommand(OpenVaultAsync);

        Preferences = new ObservableCollection<PreferenceItem>
        {
            new()
            {
                IconGlyph = "PersonPill24",
                Title = "Personal Info",
                Subtitle = "Name, email, password, body metrics, cycle, training basics",
                Route = "ProfilePersonalInfoPage"
            },
            new()
            {
                IconGlyph = "Food24",
                Title = "Nutrition Settings",
                Subtitle = "Dietary preferences, nutrition goal, custom macro and micro targets",
                Route = "ProfileNutritionSettingsPage"
            },
            new()
            {
                IconGlyph = "Dumbbell24",
                Title = "Activity Preferences",
                Subtitle = "Strength base, optional cardio, climbing, yoga, and recovery choices",
                Route = "ProfileWorkoutPreferencesPage"
            },
            new()
            {
                IconGlyph = "HeartPulse32",
                Title = "Injury Log",
                Subtitle = "Track injuries and see how they affect your plan",
                Route = "InjuryLogPage"
            },
            new()
            {
                IconGlyph = "ChatBubblesQuestion24",
                Title = "Feedback",
                Subtitle = "Send a note or report an issue",
                Route = "ProfileFeedbackPage"
            },
            new()
            {
                IconGlyph = "ChatSettings24",
                Title = "Units & Display",
                Subtitle = "Metric or imperial defaults for body, food, and training",
                Route = "ProfileUnitsDisplayPage"
            },
            new()
            {
                IconGlyph = "ShieldLock24",
                Title = "Privacy",
                Subtitle = "See what stays on your device and what you choose to share",
                Route = "ProfilePrivacyPage"
            }
        };
    }

    public bool IsAppleHealthAvailable => _healthConnection?.IsAvailable ?? false;

    public string UserInitial => !string.IsNullOrWhiteSpace(Name) ? Name[0].ToString().ToUpperInvariant() : "U";

    public string UserName => !string.IsNullOrWhiteSpace(Name) ? Name :
        !string.IsNullOrWhiteSpace(Email) ? Email.Split('@')[0] : UiTextCatalog.Translate("Athlete");

    public int SessionCount => CompletedSessions;
    public int PhasesTracked => WorkoutStreakDays;
    public string CyclesTrackedText => CyclesTracked == 1 ? "1 cycle" : $"{CyclesTracked} cycles";
    public string CurrentPhaseLabel => CurrentPhase.ToString();
    public bool HasProfileImage => !string.IsNullOrWhiteSpace(ProfileImagePath) &&
                                   (File.Exists(ProfileImagePath) ||
                                    (Uri.TryCreate(ProfileImagePath, UriKind.Absolute, out var uri) &&
                                     uri.Scheme == Uri.UriSchemeHttps));
    public bool HasNoProfileImage => !HasProfileImage;
    public string ProfileImageSource => HasProfileImage ? ProfileImagePath : string.Empty;
    public AsyncRelayCommand LogoutCommand { get; }
    public AsyncRelayCommand DeleteAccountAndDataCommand { get; }
    public AsyncRelayCommand<PreferenceItem> OpenPreferenceCommand { get; }
    public AsyncRelayCommand<string> OpenRouteCommand { get; }
    public RelayCommand ToggleVaultPrivacyCommand { get; }
    public AsyncRelayCommand OpenVaultCommand { get; }
    public bool HasAccountActionError => !string.IsNullOrWhiteSpace(AccountActionError);

    public AsyncRelayCommand LoadDataCommand { get; }
    public bool IsPageLoading => IsBusy && !_loadGate.HasLoaded;

    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Invalidate()
    {
        _loadGate.MarkStale();
    }

    partial void OnAccountActionErrorChanged(string value)
    {
        OnPropertyChanged(nameof(HasAccountActionError));
    }

    private async Task LoadDataCoreAsync()
    {
        IsBusy = true;
        try
        {
            var overview = await _useCase.LoadAsync();
            Email = overview.Email;
            MemberSince = overview.MemberSince is { } joined ? $"Member since {joined:MMM yyyy}" : string.Empty;
            Name = overview.Name;
            Goal = overview.Goal;
            CycleDays = overview.CycleDays;
            ProfileImagePath = overview.ProfileImagePath;
            UnitsDisplayText = $"{overview.UnitSystem} ({overview.BodyWeightUnit}, {overview.HeightUnit})";
            TrainingSplitText = $"{overview.WorkoutDaysPerWeek} days / week";
            NutritionPacingText = FormatNutritionGoalPace(overview.Goal, overview.WeightGoalPace);
            CompletedSessions = overview.CompletedSessions;
            WorkoutStreakDays = overview.WorkoutStreakDays;
            NutritionStreakDays = overview.NutritionStreakDays;
            CurrentPhase = overview.CurrentPhase;
            CyclesTracked = overview.CyclesTracked;
            if (_healthConnection is { IsAvailable: true })
            {
                var connected = await _healthConnection.IsConnectedAsync();
                IsHealthConnected = connected;
                HealthStatusText = connected ? "Connected" : "Tap to Connect";
            }
            else if (overview.IsHealthConnected is { } connected)
            {
                IsHealthConnected = connected;
                HealthStatusText = connected ? "Connected" : "Not connected";
            }

            ActiveInjuryCount = overview.ActiveInjuryCount;
            HasActiveInjuries = ActiveInjuryCount > 0;
            ActiveInjuryBadgeText = ActiveInjuryCount == 0 ? "0 Active" : $"{ActiveInjuryCount} Active";
            VaultPhotoCount = overview.VaultPhotoCount;
            VaultPhotoCountText = VaultPhotoCount == 1 ? "1 photo" : $"{VaultPhotoCount} photos";
            LatestVaultPhotoPath = overview.LatestVaultPhotoPath;
            HasVaultPhotos = VaultPhotoCount > 0;
            OnPropertyChanged(nameof(UserInitial));
            OnPropertyChanged(nameof(UserName));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ToggleVaultPrivacy()
    {
        IsVaultRevealed = !IsVaultRevealed;
    }

    private async Task OpenVaultAsync()
    {
        await _navigateToPreferenceAsync("PhotoJourneyVaultPage");
    }

    private async Task OpenRouteAsync(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return;

        await _navigateToPreferenceAsync(route);
    }

    private async Task LogoutAsync()
    {
        IsBusy = true;
        try
        {
            _refreshNotifier.InvalidateAll();
            if (_photoCache is not null)
                await _photoCache.ClearAsync();
            await _useCase.LogoutAsync();
            await _navigateToLoginAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAccountAndDataAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        AccountActionError = string.Empty;
        try
        {
            var result = await _useCase.PurgeCurrentAccountAsync();
            if (!result.AccountFound)
            {
                AccountActionError = "Could not identify the local account to delete. Please try again.";
                return;
            }

            if (!result.Succeeded)
            {
                AccountActionError =
                    "Deletion could not be completed. Your local data may still be on this device. Please contact support before handing over the device.";
                return;
            }

            _refreshNotifier.InvalidateAll();
            if (_photoCache is not null)
                await _photoCache.ClearAsync();
            await _navigateToLoginAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileViewModel] Account deletion failed ({ex.GetType().Name}).");
            AccountActionError = "Deletion could not be completed. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenPreferenceAsync(PreferenceItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Route))
            return;

        await _navigateToPreferenceAsync(item.Route);
    }

    public async Task UpdateProfileImageAsync(string? imagePath)
    {
        var savedImagePath = await _useCase.UpdateProfileImageAsync(imagePath);
        if (savedImagePath is null)
            return;

        ProfileImagePath = savedImagePath;
    }

    private static string FormatNutritionGoalPace(UserGoal goal, WeightGoalPace pace)
    {
        return goal switch
        {
            UserGoal.FatLoss => pace switch
            {
                WeightGoalPace.Steady => "Fat Loss • Steady (12% deficit)",
                WeightGoalPace.Aggressive => "Fat Loss • Aggressive (22% deficit)",
                _ => "Fat Loss"
            },
            UserGoal.MuscleTone => pace switch
            {
                WeightGoalPace.Steady => "Muscle Tone • Steady (+150 kcal)",
                WeightGoalPace.Aggressive => "Muscle Tone • Aggressive (+300 kcal)",
                _ => "Muscle Tone"
            },
            UserGoal.Strength => "Strength • Maintenance",
            UserGoal.MaintainHealth => "Maintain Health",
            _ => goal.ToString()
        };
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(UserInitial));
        OnPropertyChanged(nameof(UserName));
    }

    partial void OnCompletedSessionsChanged(int value)
    {
        OnPropertyChanged(nameof(SessionCount));
    }

    partial void OnWorkoutStreakDaysChanged(int value)
    {
        OnPropertyChanged(nameof(PhasesTracked));
    }

    partial void OnCyclesTrackedChanged(int value)
    {
        OnPropertyChanged(nameof(CyclesTrackedText));
    }

    partial void OnCurrentPhaseChanged(CyclePhase value)
    {
        OnPropertyChanged(nameof(CurrentPhaseLabel));
    }

    partial void OnProfileImagePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasProfileImage));
        OnPropertyChanged(nameof(HasNoProfileImage));
        OnPropertyChanged(nameof(ProfileImageSource));
    }
}
