using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.UI.Profile;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Progress;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfileViewModel : ObservableObject, IPageLoadAware
{
    private readonly Lazy<IAppPreloadService> _preloadService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ViewModelLoadGate _loadGate = new(ViewModelLoadGate.PageFreshnessWindow);
    private readonly Func<Task> _navigateToLoginAsync;
    private readonly Func<string, Task> _navigateToPreferenceAsync;
    [ObservableProperty] private int _completedSessions;
    [ObservableProperty] private CyclePhase _currentPhase = CyclePhase.Follicular;
    [ObservableProperty] private int _cycleDays = 28;
    [ObservableProperty] private int _cyclesTracked;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private UserGoal _goal;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private string _memberSince = string.Empty;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private int _nutritionStreakDays;
    [ObservableProperty] private ObservableCollection<PreferenceItem> _preferences = new();
    [ObservableProperty] private string _profileImagePath = string.Empty;
    [ObservableProperty] private int _workoutStreakDays;

    public ProfileViewModel(
        IServiceScopeFactory scopeFactory,
        Lazy<IAppPreloadService> preloadService,
        Func<Task> navigateToLoginAsync,
        Func<string, Task>? navigateToPreferenceAsync = null)
    {
        _scopeFactory = scopeFactory;
        _preloadService = preloadService;
        _navigateToLoginAsync = navigateToLoginAsync;
        _navigateToPreferenceAsync = navigateToPreferenceAsync ?? (_ => Task.CompletedTask);
        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync));
        LogoutCommand = new AsyncRelayCommand(LogoutAsync);
        OpenPreferenceCommand = new AsyncRelayCommand<PreferenceItem>(OpenPreferenceAsync);
        Preferences = new ObservableCollection<PreferenceItem>
        {
            new()
            {
                IconGlyph = "PersonPill24",
                Title = "Personal Info",
                Subtitle = "Name, email, body metrics, cycle, training basics",
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
                IconGlyph = "ChatBubblesQuestion24",
                Title = "Feedback",
                Subtitle = "Send private beta feedback to the handsome, jacked developer",
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
                Subtitle = "Private beta, no medical advice, no sharing beyond the handsome, jacked developer",
                Route = "ProfilePrivacyPage"
            }
        };
    }

    public string UserInitial => Name.Length > 0 ? Name[0].ToString().ToUpper() : "?";
    public string UserName => Name;
    public int SessionCount => CompletedSessions;
    public int PhasesTracked => WorkoutStreakDays;
    public string CyclesTrackedText => CyclesTracked == 1 ? "1 cycle" : $"{CyclesTracked} cycles";
    public string CurrentPhaseLabel => CurrentPhase.ToString();
    public bool HasProfileImage => IsExistingProfileImage(ProfileImagePath);
    public bool HasNoProfileImage => !HasProfileImage;
    public string ProfileImageSource => HasProfileImage ? ProfileImagePath : string.Empty;

    public void Invalidate() => _loadGate.MarkStale();

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand LogoutCommand { get; }
    public AsyncRelayCommand<PreferenceItem> OpenPreferenceCommand { get; }
    public bool IsPageLoading => IsBusy && !_loadGate.HasLoaded;

    private async Task LoadDataCoreAsync()
    {
        IsBusy = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var cycleService = scope.ServiceProvider.GetRequiredService<ICycleService>();
            var progressSummaryService = scope.ServiceProvider.GetRequiredService<IProgressSummaryService>();

            var userId = await DataLoadScheduler.RunAsync(authService.GetCurrentUserIdAsync);
            var user = await DataLoadScheduler.RunAsync(() => userRepository.GetByIdAsync(userId));
            var profile = await DataLoadScheduler.RunAsync(() => userRepository.GetProfileAsync(userId));

            if (user is not null)
            {
                Email = user.Email;
                MemberSince = $"Member since {user.CreatedAt:MMM yyyy}";
            }

            if (profile is not null)
            {
                Name = profile.Name;
                Goal = profile.Goal;
                CycleDays = profile.CycleLength;
                ProfileImagePath = profile.ProfileImagePath;
                OnPropertyChanged(nameof(UserInitial));
                OnPropertyChanged(nameof(UserName));
            }

            var progress = await DataLoadScheduler.RunAsync(() =>
                progressSummaryService.GetSummaryAsync(userId, DateTime.Today));
            CompletedSessions = progress.CompletedWorkoutSessions;
            WorkoutStreakDays = progress.WorkoutStreakDays;
            NutritionStreakDays = progress.NutritionStreakDays;

            var prediction = await DataLoadScheduler.RunAsync(() => cycleService.GetPredictionAsync(userId));
            CurrentPhase = prediction.CurrentPhase;
            var cycleHistory = await DataLoadScheduler.RunAsync(() => cycleService.GetCycleHistoryAsync(userId));
            CyclesTracked = cycleHistory.Count;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LogoutAsync()
    {
        IsBusy = true;
        try
        {
            _preloadService.Value.InvalidateAll();

            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            await authService.LogoutAsync();
            await _navigateToLoginAsync();
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
        using var scope = _scopeFactory.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var userId = await authService.GetCurrentUserIdAsync();
        var profile = await userRepository.GetProfileAsync(userId);
        if (profile is null)
            return;

        profile.ProfileImagePath = string.IsNullOrWhiteSpace(imagePath) ? string.Empty : imagePath.Trim();
        profile.UpdatedAt = DateTime.UtcNow;
        await userRepository.UpdateProfileAsync(profile);
        ProfileImagePath = profile.ProfileImagePath;
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

    private static bool IsExistingProfileImage(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }
}
