using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class InjuryLogViewModel : ObservableObject, IPageLoadAware
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Func<Task> _injuriesChangedAsync;
    private bool _hasLoaded;

    [ObservableProperty] private InjuryFlag _selectedSiteFlag;
    [ObservableProperty] private InjuryStatus? _selectedStatus;
    [ObservableProperty] private int _newPainLevel;
    [ObservableProperty] private DateTime _sinceDateValue = DateTime.Today;

    [ObservableProperty] private ObservableCollection<InjuryLogItem> _activeInjuries = new();
    [ObservableProperty] private ObservableCollection<InjuryLogItem> _clearedInjuries = new();

    [ObservableProperty] private bool _showAddForm;
    [ObservableProperty] private int? _editingInjuryId;
    [ObservableProperty] private bool _isHistoryExpanded;
    [ObservableProperty] private bool _isEducationExpanded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isPlanRefreshError;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorText = string.Empty;

    public bool IsPageLoading => IsBusy;
    public AsyncRelayCommand LoadDataCommand => LoadCommand;

    public bool IsEditing => EditingInjuryId is not null;
    public string FormTitle => IsEditing ? "Edit injury" : "New injury";
    public string PainLabelText => PainLabelFor(NewPainLevel);
    public bool HasClearedInjuries => ClearedInjuries.Count > 0;
    public bool HasActiveInjuries => ActiveInjuries.Count > 0;
    public bool HasNoActiveInjuries => _hasLoaded && !IsBusy && !IsLoadError &&
                                       ActiveInjuries.Count == 0 && ClearedInjuries.Count > 0;
    public bool HasNoInjuries => _hasLoaded && !IsBusy && !IsLoadError &&
                                 ActiveInjuries.Count == 0 && ClearedInjuries.Count == 0;
    public bool HasError => !string.IsNullOrEmpty(ErrorText);
    public bool CanSaveInjury => !IsBusy && SelectedSiteFlag != InjuryFlag.None && SelectedStatus is not null &&
                                 SinceDateValue.Date <= DateTime.Today;
    public DateTime Today => DateTime.Today;

    public List<InjuryFlagOption> AvailableSites { get; }
    public List<InjuryStatusOption> AvailableStatuses { get; }

    public AsyncRelayCommand LoadCommand { get; }
    public RelayCommand ToggleAddFormCommand { get; }
    public AsyncRelayCommand SaveInjuryCommand { get; }
    public AsyncRelayCommand<int> DeleteInjuryCommand { get; }
    public AsyncRelayCommand<InjuryLogItem> MarkClearedCommand { get; }
    public AsyncRelayCommand NavigateBackCommand { get; }
    public RelayCommand<InjuryLogItem> EditInjuryCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand<InjuryFlagOption> SelectSiteCommand { get; }
    public RelayCommand<InjuryStatusOption> SelectStatusCommand { get; }
    public RelayCommand ToggleHistoryCommand { get; }
    public RelayCommand ToggleEducationCommand { get; }
    public AsyncRelayCommand RetryPlanRefreshCommand { get; }

    public InjuryLogViewModel(
        IServiceScopeFactory scopeFactory,
        Func<Task> navigateBackAsync,
        Func<Task>? injuriesChangedAsync = null)
    {
        _scopeFactory = scopeFactory;
        _injuriesChangedAsync = injuriesChangedAsync ?? (() => Task.CompletedTask);

        AvailableSites = new List<InjuryFlagOption>
        {
            new() { Flag = InjuryFlag.Shoulder, Label = "Shoulder" },
            new() { Flag = InjuryFlag.Neck, Label = "Neck" },
            new() { Flag = InjuryFlag.LowBack, Label = "Low back" },
            new() { Flag = InjuryFlag.Hip, Label = "Hip" },
            new() { Flag = InjuryFlag.Knee, Label = "Knee" },
            new() { Flag = InjuryFlag.Ankle, Label = "Ankle" },
            new() { Flag = InjuryFlag.Metatarsal, Label = "Foot" },
            new() { Flag = InjuryFlag.Wrist, Label = "Wrist" },
        };

        AvailableStatuses = new List<InjuryStatusOption>
        {
            new() { Status = InjuryStatus.Acute, Label = "Acute" },
            new() { Status = InjuryStatus.Recovering, Label = "Recovering" },
            new() { Status = InjuryStatus.Cleared, Label = "Cleared" },
        };

        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        ToggleAddFormCommand = new RelayCommand(BeginAdd, () => !IsBusy);
        SaveInjuryCommand = new AsyncRelayCommand(SaveInjuryAsync, () => CanSaveInjury);
        DeleteInjuryCommand = new AsyncRelayCommand<int>(DeleteInjuryAsync, _ => !IsBusy);
        MarkClearedCommand = new AsyncRelayCommand<InjuryLogItem>(MarkClearedAsync, item => !IsBusy && item?.IsActive == true);
        NavigateBackCommand = new AsyncRelayCommand(navigateBackAsync);
        EditInjuryCommand = new RelayCommand<InjuryLogItem>(EditInjury, _ => !IsBusy);
        CancelEditCommand = new RelayCommand(CancelEdit);
        SelectSiteCommand = new RelayCommand<InjuryFlagOption>(SelectSite);
        SelectStatusCommand = new RelayCommand<InjuryStatusOption>(SelectStatus);
        ToggleHistoryCommand = new RelayCommand(() => IsHistoryExpanded = !IsHistoryExpanded);
        ToggleEducationCommand = new RelayCommand(() => IsEducationExpanded = !IsEducationExpanded);
        RetryPlanRefreshCommand = new AsyncRelayCommand(RetryPlanRefreshAsync, () => !IsBusy);
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPageLoading));
        OnPropertyChanged(nameof(HasNoInjuries));
        OnPropertyChanged(nameof(HasNoActiveInjuries));
        RefreshSaveState();
        LoadCommand.NotifyCanExecuteChanged();
        ToggleAddFormCommand.NotifyCanExecuteChanged();
        DeleteInjuryCommand.NotifyCanExecuteChanged();
        MarkClearedCommand.NotifyCanExecuteChanged();
        EditInjuryCommand.NotifyCanExecuteChanged();
        RetryPlanRefreshCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSiteFlagChanged(InjuryFlag value) => RefreshSaveState();
    partial void OnSelectedStatusChanged(InjuryStatus? value) => RefreshSaveState();
    partial void OnSinceDateValueChanged(DateTime value) => RefreshSaveState();

    private void RefreshSaveState()
    {
        OnPropertyChanged(nameof(CanSaveInjury));
        SaveInjuryCommand.NotifyCanExecuteChanged();
    }

    partial void OnNewPainLevelChanged(int value) => OnPropertyChanged(nameof(PainLabelText));

    partial void OnEditingInjuryIdChanged(int? value)
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(FormTitle));
    }

    private async Task LoadAsync()
    {
        CancelEdit();
        IsBusy = true;
        IsLoadError = false;
        ErrorText = string.Empty;
        try
        {
            if (IsPlanRefreshError)
                await RefreshAfterChangeAsync();
            else
                await ReloadInjuriesAsync();
        }
        catch (Exception)
        {
            IsLoadError = true;
            ErrorText = "Couldn't load your injury log. Try again.";
            Trace.WriteLine("[InjuryLog] Load failed.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadInjuriesAsync()
    {
        var allLogs = await DataLoadScheduler.RunAsync(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var injuryService = scope.ServiceProvider.GetRequiredService<IWorkoutInjuryService>();
            var userId = await authService.GetCurrentUserIdAsync();
            return await injuryService.GetAllAsync(userId);
        });

        var active = new List<InjuryLogItem>();
        var cleared = new List<InjuryLogItem>();

        foreach (var log in allLogs)
        {
            var siteFlag = (InjuryFlag)log.SiteFlag;
            var status = Enum.TryParse<InjuryStatus>(log.Status, true, out var parsedStatus) &&
                         Enum.IsDefined(parsedStatus)
                ? parsedStatus
                : InjuryStatus.Acute;
            var isActive = status != InjuryStatus.Cleared;

            var item = new InjuryLogItem
            {
                Id = log.Id,
                SiteFlag = siteFlag,
                SiteName = DisplayNameFor(siteFlag),
                Status = status,
                StatusName = status.ToString(),
                Pain = log.Pain,
                PainLabel = PainLabelFor(log.Pain),
                Since = log.Since,
                SinceText = log.Since.ToString("MMM d, yyyy"),
                IsActive = isActive,
            };

            if (isActive)
                active.Add(item);
            else
                cleared.Add(item);
        }

        ActiveInjuries = new ObservableCollection<InjuryLogItem>(active);
        ClearedInjuries = new ObservableCollection<InjuryLogItem>(cleared);
        OnPropertyChanged(nameof(HasActiveInjuries));
        OnPropertyChanged(nameof(HasClearedInjuries));
        _hasLoaded = true;
        IsLoadError = false;
        OnPropertyChanged(nameof(HasNoInjuries));
        OnPropertyChanged(nameof(HasNoActiveInjuries));
    }

    private async Task SaveInjuryAsync()
    {
        var status = SelectedStatus;
        if (!CanSaveInjury || status is null)
            return;

        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            var log = new WorkoutInjuryLog
            {
                Id = EditingInjuryId ?? 0,
                SiteFlag = (int)SelectedSiteFlag,
                Status = status.Value.ToString(),
                Pain = NewPainLevel,
                Since = DateOnly.FromDateTime(SinceDateValue),
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
            };

            await DataLoadScheduler.RunAsync(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                log.UserId = await scope.ServiceProvider.GetRequiredService<IAuthService>().GetCurrentUserIdAsync();
                await scope.ServiceProvider.GetRequiredService<IWorkoutInjuryService>().SaveAsync(log);
            });
            CancelEdit();
            await RefreshAfterChangeAsync();
        }
        catch (Exception)
        {
            ErrorText = "Couldn't save this injury. Your changes are still here; try again.";
            Trace.WriteLine("[InjuryLog] Save failed.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteInjuryAsync(int id)
    {
        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            await DataLoadScheduler.RunAsync(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var userId = await scope.ServiceProvider.GetRequiredService<IAuthService>().GetCurrentUserIdAsync();
                await scope.ServiceProvider.GetRequiredService<IWorkoutInjuryService>().DeleteAsync(userId, id);
            });
            if (EditingInjuryId == id)
                CancelEdit();
            await RefreshAfterChangeAsync();
        }
        catch (Exception)
        {
            ErrorText = "Couldn't delete this injury. Try again.";
            Trace.WriteLine("[InjuryLog] Delete failed.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task MarkClearedAsync(InjuryLogItem? item)
    {
        if (item is not { IsActive: true } || IsBusy)
            return;

        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            await DataLoadScheduler.RunAsync(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var userId = await scope.ServiceProvider.GetRequiredService<IAuthService>().GetCurrentUserIdAsync();
                await scope.ServiceProvider.GetRequiredService<IWorkoutInjuryService>().SaveAsync(new WorkoutInjuryLog
                {
                    Id = item.Id,
                    UserId = userId,
                    SiteFlag = (int)item.SiteFlag,
                    Status = InjuryStatus.Cleared.ToString(),
                    Pain = item.Pain,
                    Since = item.Since,
                    Date = DateOnly.FromDateTime(DateTime.UtcNow),
                });
            });
            if (EditingInjuryId == item.Id)
                CancelEdit();
            await RefreshAfterChangeAsync();
        }
        catch (Exception)
        {
            ErrorText = "Couldn't clear this injury. Try again.";
            Trace.WriteLine("[InjuryLog] Clear failed.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BeginAdd()
    {
        CancelEdit();
        ShowAddForm = true;
    }

    private void EditInjury(InjuryLogItem? item)
    {
        if (item is null) return;

        EditingInjuryId = item.Id;

        SelectedSiteFlag = item.SiteFlag;
        foreach (var site in AvailableSites)
            site.IsSelected = site.Flag == item.SiteFlag;

        SelectedStatus = item.Status;
        foreach (var status in AvailableStatuses)
            status.IsSelected = status.Status == item.Status;

        NewPainLevel = item.Pain;
        SinceDateValue = item.Since.ToDateTime(TimeOnly.MinValue);
        ShowAddForm = true;
    }

    /// <summary>
    /// Resets the add/edit form to a blank state. Called when the hosting
    /// modal or page opens so that stale edit state never carries over.
    /// </summary>
    public void ResetForm() => CancelEdit();

    private void CancelEdit()
    {
        EditingInjuryId = null;

        SelectedSiteFlag = default;
        foreach (var site in AvailableSites)
            site.IsSelected = false;

        SelectedStatus = null;
        foreach (var status in AvailableStatuses)
            status.IsSelected = false;

        NewPainLevel = 0;
        SinceDateValue = DateTime.Today;

        ShowAddForm = false;
    }

    private void SelectSite(InjuryFlagOption? selected)
    {
        if (selected is null) return;

        foreach (var site in AvailableSites)
            site.IsSelected = site == selected;

        SelectedSiteFlag = selected.Flag;
    }

    private void SelectStatus(InjuryStatusOption? selected)
    {
        if (selected is null) return;

        foreach (var status in AvailableStatuses)
            status.IsSelected = status == selected;

        SelectedStatus = selected.Status;
    }

    private async Task RefreshAfterChangeAsync()
    {
        IsPlanRefreshError = false;
        ErrorText = string.Empty;
        try
        {
            await ReloadInjuriesAsync();
            await DataLoadScheduler.RunAsync(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
                var cycleService = scope.ServiceProvider.GetRequiredService<ICycleService>();
                var workoutService = scope.ServiceProvider.GetRequiredService<IWorkoutService>();
                var userId = await authService.GetCurrentUserIdAsync();
                var phase = await cycleService.GetCurrentPhaseAsync(userId);
                await workoutService.RegenerateActivePlanAsync(userId, phase);
            });
        }
        catch (Exception)
        {
            IsPlanRefreshError = true;
            ErrorText = "Your injury log was saved, but the plan couldn't refresh. Try again before training.";
            Trace.WriteLine("[InjuryLog] Plan refresh failed.");
        }

        try
        {
            using var refreshScope = _scopeFactory.CreateScope();
            var preloadService = refreshScope.ServiceProvider.GetRequiredService<IAppPreloadService>();
            preloadService.InvalidateWorkout();
            preloadService.InvalidateDashboard();
            await _injuriesChangedAsync();
        }
        catch (Exception)
        {
            IsPlanRefreshError = true;
            ErrorText = "Your injury log was saved, but the page couldn't refresh. Try again.";
            Trace.WriteLine("[InjuryLog] Page refresh failed.");
        }
    }

    private async Task RetryPlanRefreshAsync()
    {
        IsBusy = true;
        try
        {
            await RefreshAfterChangeAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string PainLabelFor(int level) => level switch
    {
        0 => "None", 1 => "Minimal", 2 => "Mild",
        3 => "Moderate", 4 => "Strong", 5 => "Severe", _ => "None"
    };

    internal static string DisplayNameFor(InjuryFlag flag) => flag switch
    {
        InjuryFlag.Metatarsal => "Foot",
        InjuryFlag.LowBack => "Low back",
        _ => flag.ToString()
    };
}
