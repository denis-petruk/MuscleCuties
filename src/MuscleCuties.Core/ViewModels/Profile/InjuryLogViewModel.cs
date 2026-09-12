using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Repositories.Workout.Planning;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class InjuryLogViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IWorkoutInjuryRepository _injuryRepo;
    private readonly Func<Task> _navigateBackAsync;

    [ObservableProperty] private ObservableCollection<InjuryLogItem> _injuries = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _showAddForm;

    [ObservableProperty] private string _selectedSite = "Knee";
    [ObservableProperty] private string _selectedStatus = "Acute";
    [ObservableProperty] private int _newPainLevel = 1;

    [ObservableProperty] private ObservableCollection<string> _blockedExerciseNames = new();
    [ObservableProperty] private ObservableCollection<string> _rehabExerciseNames = new();
    [ObservableProperty] private ObservableCollection<string> _blockedCardioNames = new();

    public bool HasBlockedExercises => BlockedExerciseNames.Count > 0;
    public bool HasRehabExercises => RehabExerciseNames.Count > 0;
    public bool HasBlockedCardio => BlockedCardioNames.Count > 0;

    public List<string> AvailableSites { get; } =
        ["Metatarsal", "Knee", "Ankle", "Shoulder", "LowBack", "Wrist"];

    public List<string> AvailableStatuses { get; } =
        ["Acute", "Recovering", "Cleared"];

    public AsyncRelayCommand LoadCommand { get; }
    public RelayCommand ToggleAddFormCommand { get; }
    public AsyncRelayCommand AddInjuryCommand { get; }
    public AsyncRelayCommand<int> DeleteInjuryCommand { get; }
    public AsyncRelayCommand NavigateBackCommand { get; }

    public InjuryLogViewModel(
        IAuthService authService,
        IWorkoutInjuryRepository injuryRepo,
        Func<Task> navigateBackAsync)
    {
        _authService = authService;
        _injuryRepo = injuryRepo;
        _navigateBackAsync = navigateBackAsync;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        ToggleAddFormCommand = new RelayCommand(() => ShowAddForm = !ShowAddForm);
        AddInjuryCommand = new AsyncRelayCommand(AddInjuryAsync);
        DeleteInjuryCommand = new AsyncRelayCommand<int>(DeleteInjuryAsync);
        NavigateBackCommand = new AsyncRelayCommand(_navigateBackAsync);
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var allLogs = await _injuryRepo.GetAllAsync(userId);

            var items = allLogs.Select(log => new InjuryLogItem
            {
                Id = log.Id,
                SiteName = log.Site,
                StatusName = log.Status,
                Pain = log.Pain,
                SinceText = log.Since.ToString("MMM d, yyyy"),
                IsActive = log.Status != nameof(InjuryStatus.Cleared)
            }).ToList();

            Injuries = new ObservableCollection<InjuryLogItem>(items);

            var activeInjuries = allLogs
                .Where(log => log.Status != nameof(InjuryStatus.Cleared))
                .Select(log => new Injury(
                    Enum.Parse<InjurySite>(log.Site),
                    Enum.Parse<InjuryStatus>(log.Status),
                    log.Since))
                .ToList();

            var flags = WorkoutInjuryRules.ToFlags(activeInjuries);
            var (blocked, rehab) = await _injuryRepo.GetExerciseImpactAsync(flags);
            var blockedCardio = WorkoutInjuryRules.GetBlockedActivities(activeInjuries)
                .Select(activity => activity.ToString())
                .OrderBy(name => name)
                .ToList();

            BlockedExerciseNames = new ObservableCollection<string>(blocked);
            RehabExerciseNames = new ObservableCollection<string>(rehab);
            BlockedCardioNames = new ObservableCollection<string>(blockedCardio);

            OnPropertyChanged(nameof(HasBlockedExercises));
            OnPropertyChanged(nameof(HasRehabExercises));
            OnPropertyChanged(nameof(HasBlockedCardio));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AddInjuryAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var log = new WorkoutInjuryLog
            {
                UserId = userId,
                Site = SelectedSite,
                Status = SelectedStatus,
                Pain = NewPainLevel,
                Since = DateOnly.FromDateTime(DateTime.UtcNow),
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                CreatedAt = DateTime.UtcNow
            };

            await _injuryRepo.SaveAsync(log);
            ShowAddForm = false;
            SelectedSite = "Knee";
            SelectedStatus = "Acute";
            NewPainLevel = 1;
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteInjuryAsync(int id)
    {
        IsBusy = true;
        try
        {
            await _injuryRepo.DeleteAsync(id);
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
