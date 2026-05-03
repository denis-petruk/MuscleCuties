using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Core.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly Action _navigateToLogin;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private UserGoal _goal;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _memberSince = string.Empty;
    [ObservableProperty] private int _cycleDays = 28;
    // TODO(frontend-blocked): _preferences is hardcoded with 6 non-functional items; wire or remove after ProfilePage redesign
    [ObservableProperty] private ObservableCollection<PreferenceItem> _preferences = new();

    public string UserInitial => Name.Length > 0 ? Name[0].ToString().ToUpper() : "?";
    public string UserName => Name;
    // TODO(frontend-blocked): SessionCount is hardcoded 0, bound to ProfilePage.xaml:59
    public int SessionCount => 0;
    // TODO(frontend-blocked): PhasesTracked is hardcoded 0, bound to ProfilePage.xaml:97
    public int PhasesTracked => 0;

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand LogoutCommand { get; }

    public ProfileViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        Action navigateToLogin)
    {
        _authService = authService;
        _userRepository = userRepository;
        _navigateToLogin = navigateToLogin;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        LogoutCommand = new AsyncRelayCommand(LogoutAsync);
        Preferences = new ObservableCollection<PreferenceItem>
        {
            new PreferenceItem { Title = "Personal Info" },
            new PreferenceItem { Title = "Nutrition Settings" },
            new PreferenceItem { Title = "Notification Preferences" },
            new PreferenceItem { Title = "Units & Display" },
            new PreferenceItem { Title = "Privacy" },
            new PreferenceItem { Title = "Help & Support" }
        };
    }

    private async Task LoadDataAsync()
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

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(UserInitial));
        OnPropertyChanged(nameof(UserName));
    }
}
