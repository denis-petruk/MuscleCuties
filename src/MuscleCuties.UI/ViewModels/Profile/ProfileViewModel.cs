using System.Diagnostics.CodeAnalysis;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.Core.Models.Enums.Users;
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
    [ObservableProperty] private string _accountActionError = string.Empty;
    [ObservableProperty] private string _activeInjuryBadgeText = UiTextCatalog.Format(
        "Profile_Summary_ActiveInjuryFormat", 0);
    [ObservableProperty] private int _activeInjuryCount;

    [ObservableProperty] private int _completedSessions;
    [ObservableProperty] private CyclePhase _currentPhase = CyclePhase.Unknown;
    [ObservableProperty] private int _cycleDays = 28;
    [ObservableProperty] private int _cyclesTracked;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private UserGoal _goal;
    [ObservableProperty] private bool _hasActiveInjuries;
    [ObservableProperty] private bool _hasVaultPhotos;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPageLoading), nameof(IsAccountActionBusy))]
    private bool _isBusy;

    [ObservableProperty] private bool _isLoadError;

    // Private Photo Journey Vault
    [ObservableProperty] private bool _isVaultRevealed;
    [ObservableProperty] private string _latestVaultPhotoPath = string.Empty;
    [ObservableProperty] private string _memberSince = string.Empty;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NutritionRowAccessibilityText))]
    private string _nutritionPacingText = UiTextCatalog.Get("Profile_Summary_Maintenance");
    [ObservableProperty] private int _nutritionStreakDays;
    [ObservableProperty] private string _profileImagePath = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkoutAvailabilityRowAccessibilityText))]
    private string _availableWeekdaysText = UiTextCatalog.Format(
        "Profile_Summary_AvailableDaysFormat", 3);

    // Account & Preferences Section
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnitsDisplayRowAccessibilityText))]
    private string _unitsDisplayText = UiTextCatalog.Get("Profile_Summary_MetricUnits");
    [ObservableProperty] private int _vaultPhotoCount;
    [ObservableProperty] private string _vaultPhotoCountText = UiTextCatalog.Format(
        "Profile_Summary_PhotoCountFormat", 0);
    [ObservableProperty] private int _workoutStreakDays;

    public ProfileViewModel(
        IProfileOverviewUseCase useCase,
        IProfileRefreshNotifier refreshNotifier,
        Func<Task> navigateToLoginAsync,
        Func<string, Task>? navigateToPreferenceAsync = null,
        ILocalPhotoCacheService? photoCache = null)
    {
        _useCase = useCase;
        _refreshNotifier = refreshNotifier;
        _navigateToLoginAsync = navigateToLoginAsync;
        _navigateToPreferenceAsync = navigateToPreferenceAsync ?? (_ => Task.CompletedTask);
        _photoCache = photoCache;

        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync, this));
        LogoutCommand = new AsyncRelayCommand(LogoutAsync);
        DeleteAccountAndDataCommand = new AsyncRelayCommand(DeleteAccountAndDataAsync);
        OpenRouteCommand = new AsyncRelayCommand<string>(OpenRouteAsync);
        ToggleVaultPrivacyCommand = new RelayCommand(ToggleVaultPrivacy);
        OpenVaultCommand = new AsyncRelayCommand(OpenVaultAsync);
    }

    public string UserInitial => !string.IsNullOrWhiteSpace(Name) ? Name[0].ToString().ToUpperInvariant() : "U";

    public string UserName => !string.IsNullOrWhiteSpace(Name) ? Name :
        !string.IsNullOrWhiteSpace(Email) ? Email.Split('@')[0] : UiTextCatalog.TranslateRuntimeText("Athlete");

    public int SessionCount => CompletedSessions;
    public int PhasesTracked => CyclesTracked;
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
    public AsyncRelayCommand<string> OpenRouteCommand { get; }
    public RelayCommand ToggleVaultPrivacyCommand { get; }
    public AsyncRelayCommand OpenVaultCommand { get; }
    public bool HasAccountActionError => !string.IsNullOrWhiteSpace(AccountActionError);

    // The photo preview has three mutually exclusive states: an actionable empty state,
    // a revealed photo, and the privacy veil. Without photos there is nothing to reveal,
    // so the veil and its "show" action must not appear.
    public bool ShowVaultEmptyState => !HasVaultPhotos;
    public bool ShowVaultPhotoPreview => HasVaultPhotos && IsVaultRevealed;
    public bool ShowVaultPrivacyOverlay => HasVaultPhotos && !IsVaultRevealed;

    public string WorkoutAvailabilityRowAccessibilityText =>
        RowAccessibilityText("Profile_Details_WorkoutAvailability", AvailableWeekdaysText);

    public string NutritionRowAccessibilityText =>
        RowAccessibilityText("Profile_Details_Nutrition", NutritionPacingText);

    public static string PersonalInfoRowAccessibilityText =>
        RowAccessibilityText("Profile_Details_PersonalInfo",
            UiTextCatalog.Get("Profile_Details_NameEmailPasswordCycleBaselines"));

    public string UnitsDisplayRowAccessibilityText =>
        RowAccessibilityText("Profile_Details_UnitsDisplay", UnitsDisplayText);

    public static string PrivacyRowAccessibilityText =>
        RowAccessibilityText("Profile_Privacy_Policy",
            UiTextCatalog.Get("Profile_Details_HealthRecordsStayOnThisDevice"));

    public static string SendFeedbackRowAccessibilityText =>
        RowAccessibilityText("Profile_Details_SendFeedback",
            UiTextCatalog.Get("Profile_Details_ReportAnIssueOrSuggestA"));

    public AsyncRelayCommand LoadDataCommand { get; }
    public bool IsPageLoading => IsBusy && !_loadGate.HasLoaded;
    public bool IsAccountActionBusy => IsBusy && _loadGate.HasLoaded;

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
            AvailableWeekdaysText = $"{overview.AvailableWeekdaysCount} available days / week";
            NutritionPacingText = FormatNutritionGoalPace(overview.Goal, overview.WeightGoalPace);
            CompletedSessions = overview.CompletedSessions;
            WorkoutStreakDays = overview.WorkoutStreakDays;
            NutritionStreakDays = overview.NutritionStreakDays;
            CurrentPhase = overview.CurrentPhase;
            CyclesTracked = overview.CyclesTracked;
            ActiveInjuryCount = overview.ActiveInjuryCount;
            HasActiveInjuries = ActiveInjuryCount > 0;
            ActiveInjuryBadgeText = UiTextCatalog.Format(
                "Profile_Summary_ActiveInjuryFormat", ActiveInjuryCount);
            if (overview.VaultPhotoCount < 0)
            {
                VaultPhotoCount = 0;
                VaultPhotoCountText = UiTextCatalog.Get("Profile_PhotoJourney_PhotoUnavailable");
                LatestVaultPhotoPath = string.Empty;
                HasVaultPhotos = false;
            }
            else
            {
                VaultPhotoCount = overview.VaultPhotoCount;
                VaultPhotoCountText = VaultPhotoCount == 1 ? "1 photo" : $"{VaultPhotoCount} photos";
                LatestVaultPhotoPath = overview.LatestVaultPhotoPath;
                HasVaultPhotos = VaultPhotoCount > 0;
            }
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

    /// <summary>
    ///     A settings row is one VoiceOver element. Its value is part of the label because a row
    ///     that carries a tap gesture stops exposing its child labels once it is a button.
    /// </summary>
    private static string RowAccessibilityText(string titleKey, string detail) =>
        UiTextCatalog.Format("Profile_Details_RowAccessibilityFormat",
            UiTextCatalog.Get(titleKey), UiTextCatalog.TranslateRuntimeText(detail));

    private async Task OpenRouteAsync(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return;

        await _navigateToPreferenceAsync(route);
    }

    private async Task LogoutAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        AccountActionError = string.Empty;
        try
        {
            _refreshNotifier.InvalidateAll();
            if (_photoCache is not null)
                await _photoCache.ClearAsync();
            await _useCase.LogoutAsync();
            await _navigateToLoginAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileViewModel] Logout failed ({ex.GetType().Name}).");
            AccountActionError = UiTextCatalog.Get("Profile_Logout_Failed");
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
                AccountActionError = result.FailureMessage ??
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

    partial void OnCyclesTrackedChanged(int value)
    {
        OnPropertyChanged(nameof(PhasesTracked));
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

    partial void OnHasVaultPhotosChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowVaultEmptyState));
        OnPropertyChanged(nameof(ShowVaultPhotoPreview));
        OnPropertyChanged(nameof(ShowVaultPrivacyOverlay));
    }

    partial void OnIsVaultRevealedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowVaultPhotoPreview));
        OnPropertyChanged(nameof(ShowVaultPrivacyOverlay));
    }
}
