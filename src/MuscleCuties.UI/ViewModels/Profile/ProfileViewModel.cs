using System.Diagnostics.CodeAnalysis;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Profile.Overview;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.UI.ViewModels.Profile;

/// <summary>The you tab: the athlete's identity and summary figures, the rows that lead to each settings page, and the</summary>
/// photo vault's privacy state.</summary>
public partial class ProfileViewModel : ObservableObject, IPageLoadAware, IDisposable
{
    private readonly AppPageLoadGate _loadGate = new(AppPageLoadGate.PageFreshnessWindow);
    private readonly Func<Task> _navigateToLoginAsync;
    private readonly Func<string, Task> _navigateToPreferenceAsync;
    private readonly IProfileRefreshNotifier _refreshNotifier;
    private readonly IProfileOverviewUseCase _useCase;
    private readonly ILocalPhotoCacheService? _photoCache;
    [ObservableProperty] private string _accountActionError = string.Empty;
    [ObservableProperty]
    private string _activeInjuryBadgeText = UiTextCatalog.Format(
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageLoading), nameof(IsAccountActionBusy))]
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
    [ObservableProperty]
    private string _vaultPhotoCountText = UiTextCatalog.Format(
        "Profile_Summary_PhotoCountFormat", 0);
    [ObservableProperty] private int _workoutStreakDays;

    /// <summary>Takes the profile use cases and the navigation callbacks.</summary>
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

    /// <summary>The avatar letter, taken from the display name and falling back to a neutral letter when there is no</summary>
    /// name.</summary>
    public string UserInitial => !string.IsNullOrWhiteSpace(Name) ? Name[0].ToString().ToUpperInvariant() : "U";

    /// <summary>The name shown for the user.</summary>
    public string UserName => !string.IsNullOrWhiteSpace(Name) ? Name :
        !string.IsNullOrWhiteSpace(Email) ? Email.Split('@')[0] : UiTextCatalog.TranslateRuntimeText("Athlete");

    /// <summary>How many sessions the profile has completed.</summary>
    public int SessionCount => CompletedSessions;
    /// <summary>How many cycles the profile has tracked.</summary>
    public int PhasesTracked => CyclesTracked;
    /// <summary>How many cycles have been tracked, worded for one or many.</summary>
    public string CyclesTrackedText => CyclesTracked == 1 ? "1 cycle" : $"{CyclesTracked} cycles";
    /// <summary>The current cycle phase as the summary row shows it.</summary>
    public string CurrentPhaseLabel => CurrentPhase.ToString();
    /// <summary>An avatar is only shown when its file still exists or it is an HTTPS image, so a deleted local file or a</summary>
    /// stale path cannot leave a broken picture.</summary>
    public bool HasProfileImage => !string.IsNullOrWhiteSpace(ProfileImagePath) &&
                                   (File.Exists(ProfileImagePath) ||
                                    (Uri.TryCreate(ProfileImagePath, UriKind.Absolute, out var uri) &&
                                     uri.Scheme == Uri.UriSchemeHttps));
    /// <summary>True when no profile photo is set.</summary>
    public bool HasNoProfileImage => !HasProfileImage;
    /// <summary>The avatar path when there is a usable one, empty when there is not.</summary>
    public string ProfileImageSource => HasProfileImage ? ProfileImagePath : string.Empty;
    /// <summary>Signs the user out.</summary>
    public AsyncRelayCommand LogoutCommand { get; }
    /// <summary>Deletes the account and its data.</summary>
    public AsyncRelayCommand DeleteAccountAndDataCommand { get; }
    /// <summary>Opens the given route.</summary>
    public AsyncRelayCommand<string> OpenRouteCommand { get; }
    /// <summary>Shows or hides the vault photos.</summary>
    public RelayCommand ToggleVaultPrivacyCommand { get; }
    /// <summary>Opens the photo vault.</summary>
    public AsyncRelayCommand OpenVaultCommand { get; }
    /// <summary>True when an account action failed.</summary>
    public bool HasAccountActionError => !string.IsNullOrWhiteSpace(AccountActionError);

    // The photo preview has three mutually exclusive states: an actionable empty state,
    // a revealed photo, and the privacy veil. Without photos there is nothing to reveal,
    // so the veil and its "show" action must not appear.
    /// <summary>True when the vault has no photos.</summary>
    public bool ShowVaultEmptyState => !HasVaultPhotos;
    /// <summary>True when the vault preview is shown.</summary>
    public bool ShowVaultPhotoPreview => HasVaultPhotos && IsVaultRevealed;
    /// <summary>True when the vault privacy overlay is shown.</summary>
    public bool ShowVaultPrivacyOverlay => HasVaultPhotos && !IsVaultRevealed;

    /// <summary>The availability row as spoken text: its title plus the days it currently holds.</summary>
    public string WorkoutAvailabilityRowAccessibilityText =>
        RowAccessibilityText("Profile_Details_WorkoutAvailability", AvailableWeekdaysText);

    /// <summary>The nutrition row as spoken text, with the current pacing.</summary>
    public string NutritionRowAccessibilityText =>
        RowAccessibilityText("Profile_Details_Nutrition", NutritionPacingText);

    /// <summary>The personal-info row as spoken text, naming what the page holds.</summary>
    public static string PersonalInfoRowAccessibilityText =>
        RowAccessibilityText("Profile_Details_PersonalInfo",
            UiTextCatalog.Get("Profile_Details_NameEmailPasswordCycleBaselines"));

    /// <summary>The units row as spoken text, with the system currently chosen.</summary>
    public string UnitsDisplayRowAccessibilityText =>
        RowAccessibilityText("Profile_Details_UnitsDisplay", UnitsDisplayText);

    /// <summary>The privacy row as spoken text, stating that health records stay on the device.</summary>
    public static string PrivacyRowAccessibilityText =>
        RowAccessibilityText("Profile_Privacy_Policy",
            UiTextCatalog.Get("Profile_Details_HealthRecordsStayOnThisDevice"));

    /// <summary>The feedback row as spoken text, naming what it is for.</summary>
    public static string SendFeedbackRowAccessibilityText =>
        RowAccessibilityText("Profile_Details_SendFeedback",
            UiTextCatalog.Get("Profile_Details_ReportAnIssueOrSuggestA"));

    /// <summary>Loads the profile.</summary>
    public AsyncRelayCommand LoadDataCommand { get; }
    /// <summary>The first-load indicator, not shown for a reload the page has already rendered once.</summary>
    public bool IsPageLoading => _loadGate.IsLoading(IsBusy);
    /// <summary>True while an account action runs.</summary>
    public bool IsAccountActionBusy => IsBusy && _loadGate.HasLoaded;

    /// <summary>Releases the view model's subscriptions.</summary>
    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Marks the summary stale so the next appearance reloads it.</summary>
    public void Invalidate()
    {
        _loadGate.MarkStale();
    }

    /// <summary>Refreshes the error flag when the text changes.</summary>
    partial void OnAccountActionErrorChanged(string value)
    {
        OnPropertyChanged(nameof(HasAccountActionError));
    }

    /// <summary>Loads the identity, the summary figures and the vault's first photo, which is what the tab shows without</summary>
    /// the user opening anything.</summary>
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

    /// <summary>Reveals or re-covers the vault's photos; the veil is the only thing standing between a shoulder-surfer</summary>
    /// and the pictures.</summary>
    private void ToggleVaultPrivacy()
    {
        IsVaultRevealed = !IsVaultRevealed;
    }

    /// <summary>Opens the photo journey vault.</summary>
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

    /// <summary>Opens a settings page by its registered route, ignoring an empty one so a row without a target does</summary>
    /// nothing.</summary>
    private async Task OpenRouteAsync(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return;

        await _navigateToPreferenceAsync(route);
    }

    /// <summary>Signs out and returns to the login screen, after the confirmation the command's own page puts up.</summary>
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

    /// <summary>Deletes the account and its data and returns to login; a refusal is reported on the page rather than leaving</summary>
    /// the user in a half-deleted state.</summary>
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

    /// <summary>Stores the newly chosen avatar path and republishes everything that depends on whether a picture exists.</summary>
    public async Task UpdateProfileImageAsync(string? imagePath)
    {
        var savedImagePath = await _useCase.UpdateProfileImageAsync(imagePath);
        if (savedImagePath is null)
            return;

        ProfileImagePath = savedImagePath;
    }

    /// <summary>The goal and its pace as the single line the details row shows, with no pace for goals that do not use</summary>
    /// one.</summary>
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

    /// <summary>Refreshes the shown name when it changes.</summary>
    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(UserInitial));
        OnPropertyChanged(nameof(UserName));
    }

    /// <summary>Refreshes the session count.</summary>
    partial void OnCompletedSessionsChanged(int value)
    {
        OnPropertyChanged(nameof(SessionCount));
    }

    /// <summary>Refreshes the phase count.</summary>
    partial void OnCyclesTrackedChanged(int value)
    {
        OnPropertyChanged(nameof(PhasesTracked));
        OnPropertyChanged(nameof(CyclesTrackedText));
    }

    /// <summary>Refreshes the phase visuals when the phase changes.</summary>
    partial void OnCurrentPhaseChanged(CyclePhase value)
    {
        OnPropertyChanged(nameof(CurrentPhaseLabel));
    }

    /// <summary>Refreshes the photo state when the path changes.</summary>
    partial void OnProfileImagePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasProfileImage));
        OnPropertyChanged(nameof(HasNoProfileImage));
        OnPropertyChanged(nameof(ProfileImageSource));
    }

    /// <summary>Refreshes the vault flags when photos change.</summary>
    partial void OnHasVaultPhotosChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowVaultEmptyState));
        OnPropertyChanged(nameof(ShowVaultPhotoPreview));
        OnPropertyChanged(nameof(ShowVaultPrivacyOverlay));
    }

    /// <summary>Refreshes the vault flags when the blur changes.</summary>
    partial void OnIsVaultRevealedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowVaultPhotoPreview));
        OnPropertyChanged(nameof(ShowVaultPrivacyOverlay));
    }
}
