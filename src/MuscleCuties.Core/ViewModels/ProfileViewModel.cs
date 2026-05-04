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
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICycleRepository _cycleRepository;
    private readonly Action _navigateToLogin;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private UserGoal _goal;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _memberSince = string.Empty;
    [ObservableProperty] private int _cycleDays = 28;
    [ObservableProperty] private ObservableCollection<PreferenceItem> _preferences = new();
    [ObservableProperty] private int _sessionCount;
    [ObservableProperty] private int _phasesTracked;

    public string UserInitial => Name.Length > 0 ? Name[0].ToString().ToUpper() : "?";
    public string UserName => Name;

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand LogoutCommand { get; }

    public ProfileViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        IWorkoutRepository workoutRepository,
        ICycleRepository cycleRepository,
        Action navigateToLogin)
    {
        _authService = authService;
        _userRepository = userRepository;
        _workoutRepository = workoutRepository;
        _cycleRepository = cycleRepository;
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

            SessionCount = await _workoutRepository.GetWorkoutLogCountAsync(userId);

            var cycles = await _cycleRepository.GetCycleHistoryAsync(userId);
            PhasesTracked = cycles.Count * 4;
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
