using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.UI.Profile;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfileViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly Action _navigateToLogin;
    private readonly Action<string> _navigateToPreference;
    private readonly ViewModelLoadGate _loadGate = new(TimeSpan.FromSeconds(30));

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private UserGoal _goal;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _memberSince = string.Empty;
    [ObservableProperty] private int _cycleDays = 28;
    [ObservableProperty] private ObservableCollection<PreferenceItem> _preferences = new();

    public string UserInitial => Name.Length > 0 ? Name[0].ToString().ToUpper() : "?";
    public string UserName => Name;
    public int SessionCount => 0;
    public int PhasesTracked => 0;

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand LogoutCommand { get; }
    public RelayCommand<PreferenceItem> OpenPreferenceCommand { get; }

    public ProfileViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        Action navigateToLogin,
        Action<string>? navigateToPreference = null)
    {
        _authService = authService;
        _userRepository = userRepository;
        _navigateToLogin = navigateToLogin;
        _navigateToPreference = navigateToPreference ?? (_ => { });
        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync));
        LogoutCommand = new AsyncRelayCommand(LogoutAsync);
        OpenPreferenceCommand = new RelayCommand<PreferenceItem>(OpenPreference);
        Preferences = new ObservableCollection<PreferenceItem>
        {
            new()
            {
                Title = "Personal Info",
                Subtitle = "Name, email, body metrics, cycle, training basics",
                Route = "ProfilePersonalInfoPage"
            },
            new()
            {
                Title = "Nutrition Settings",
                Subtitle = "Dietary preferences, nutrition goal, custom macro and micro targets",
                Route = "ProfileNutritionSettingsPage"
            },
            new()
            {
                Title = "Workout Preferences",
                Subtitle = "Strength, cardio, climbing, yoga, recovery, and favorite movement styles",
                Route = "ProfileWorkoutPreferencesPage"
            },
            new()
            {
                Title = "Feedback",
                Subtitle = "Send private beta feedback to the handsome, jacked developer",
                Route = "ProfileFeedbackPage"
            },
            new()
            {
                Title = "Units & Display",
                Subtitle = "Metric or imperial defaults for body, food, and training",
                Route = "ProfileUnitsDisplayPage"
            },
            new()
            {
                Title = "Privacy",
                Subtitle = "Private beta, no medical advice, no sharing beyond the handsome, jacked developer",
                Route = "ProfilePrivacyPage"
            }
        };
    }

    private async Task LoadDataCoreAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var user = await _userRepository.GetByIdAsync(userId);
            var profile = await _userRepository.GetProfileAsync(userId);

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
                OnPropertyChanged(nameof(UserInitial));
                OnPropertyChanged(nameof(UserName));
            }
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
            await _authService.LogoutAsync();
            _navigateToLogin();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenPreference(PreferenceItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Route))
            return;

        _navigateToPreference(item.Route);
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(UserInitial));
        OnPropertyChanged(nameof(UserName));
    }
}
