using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Workout.Injury;

namespace MuscleCuties.UI.ViewModels.Workout;

public partial class InjuryLogViewModel : ObservableObject, IPageLoadAware
{
    private const string ReloadAfterChangeFailedTextKey = "Injury_Log_ReloadAfterChangeFailed";
    private const string PlanRefreshFailedTextKey = "Injury_Log_PlanRefreshFailed";
    private const string PageRefreshFailedTextKey = "Injury_Log_PageRefreshFailed";
    private readonly IWorkoutInjuryUseCase _injuries;
    private readonly Func<Task> _injuriesChangedAsync;

    [ObservableProperty] private ObservableCollection<InjuryLogItem> _activeInjuries = new();
    [ObservableProperty] private ObservableCollection<InjuryLogItem> _clearedInjuries = new();
    [ObservableProperty] private int? _editingInjuryId;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorText = string.Empty;

    private bool _hasLoaded;
    private bool _pendingChangeRefresh;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isHistoryExpanded;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isPlanRefreshError;
    [ObservableProperty] private bool _isPageRefreshError;
    [ObservableProperty] private int _newPainLevel;

    [ObservableProperty] private WorkoutInjurySite _selectedSiteFlag;
    [ObservableProperty] private WorkoutInjuryStatus? _selectedStatus;

    [ObservableProperty] private bool _showAddForm;
    [ObservableProperty] private DateTime _sinceDateValue = DateTime.Today;

    public InjuryLogViewModel(
        IWorkoutInjuryUseCase injuries,
        Func<Task> navigateBackAsync,
        Func<Task>? injuriesChangedAsync = null)
    {
        _injuries = injuries;
        _injuriesChangedAsync = injuriesChangedAsync ?? (() => Task.CompletedTask);

        AvailableSites = new List<InjuryFlagOption>
        {
            new() { Flag = WorkoutInjurySite.Shoulder, Label = "Shoulder" },
            new() { Flag = WorkoutInjurySite.Neck, Label = "Neck" },
            new() { Flag = WorkoutInjurySite.LowBack, Label = "Low back" },
            new() { Flag = WorkoutInjurySite.Hip, Label = "Hip" },
            new() { Flag = WorkoutInjurySite.Knee, Label = "Knee" },
            new() { Flag = WorkoutInjurySite.Ankle, Label = "Ankle" },
            new() { Flag = WorkoutInjurySite.Foot, Label = "Foot" },
            new() { Flag = WorkoutInjurySite.Wrist, Label = "Wrist" }
        };

        AvailableStatuses = new List<InjuryStatusOption>
        {
            new() { Status = WorkoutInjuryStatus.Acute, Label = "Acute" },
            new() { Status = WorkoutInjuryStatus.Recovering, Label = "Recovering" },
            new() { Status = WorkoutInjuryStatus.Cleared, Label = "Cleared" }
        };

        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        ToggleAddFormCommand = new RelayCommand(BeginAdd, () => !IsBusy);
        SaveInjuryCommand = new AsyncRelayCommand(SaveInjuryAsync, () => CanSaveInjury);
        DeleteInjuryCommand = new AsyncRelayCommand<int>(DeleteInjuryAsync, _ => !IsBusy);
        MarkClearedCommand =
            new AsyncRelayCommand<InjuryLogItem>(MarkClearedAsync, item => !IsBusy && item?.IsActive == true);
        NavigateBackCommand = new AsyncRelayCommand(navigateBackAsync);
        EditInjuryCommand = new RelayCommand<InjuryLogItem>(EditInjury, _ => !IsBusy);
        CancelEditCommand = new RelayCommand(CancelEdit);
        SelectSiteCommand = new RelayCommand<InjuryFlagOption>(SelectSite);
        SelectStatusCommand = new RelayCommand<InjuryStatusOption>(SelectStatus);
        ToggleHistoryCommand = new RelayCommand(() => IsHistoryExpanded = !IsHistoryExpanded);
        RetryPlanRefreshCommand = new AsyncRelayCommand(RetryPlanRefreshAsync, () => !IsBusy);
        RetryPageRefreshCommand = new AsyncRelayCommand(RetryPageRefreshAsync, () => !IsBusy);
    }

    public bool IsEditing => EditingInjuryId is not null;
    public string FormTitle => IsEditing ? "Edit injury" : "New injury";
    public string PainLabelText => PainLabelFor(NewPainLevel);
    public bool HasClearedInjuries => ClearedInjuries.Count > 0;
    public bool HasActiveInjuries => ActiveInjuries.Count > 0;

    public bool HasNoActiveInjuries => _hasLoaded && !IsBusy && !IsLoadError &&
                                       ActiveInjuries.Count == 0 && ClearedInjuries.Count > 0;

    public bool HasNoInjuries => _hasLoaded && !IsBusy && !IsLoadError &&
                                 ActiveInjuries.Count == 0 && ClearedInjuries.Count == 0;

    public bool ShowEmptyState => _hasLoaded && !IsBusy && !IsLoadError &&
                                  !ShowAddForm && ActiveInjuries.Count == 0;

    public string EmptyStateTitle => ClearedInjuries.Count == 0
        ? "No injuries logged"
        : "No active injuries";

    public string EmptyStateDetail => ClearedInjuries.Count == 0
        ? "Your plan has no injury restrictions"
        : "Cleared injuries are saved in History below.";

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    public bool CanSaveInjury => !IsBusy && SelectedSiteFlag != WorkoutInjurySite.None && SelectedStatus is not null &&
                                 SinceDateValue.Date <= DateTime.Today;

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in Injury XAML / view model contract.")]
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
    public AsyncRelayCommand RetryPlanRefreshCommand { get; }
    public AsyncRelayCommand RetryPageRefreshCommand { get; }

    public bool IsPageLoading => IsBusy;
    public AsyncRelayCommand LoadDataCommand => LoadCommand;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPageLoading));
        OnPropertyChanged(nameof(HasNoInjuries));
        OnPropertyChanged(nameof(HasNoActiveInjuries));
        OnPropertyChanged(nameof(ShowEmptyState));
        RefreshSaveState();
        LoadCommand.NotifyCanExecuteChanged();
        ToggleAddFormCommand.NotifyCanExecuteChanged();
        DeleteInjuryCommand.NotifyCanExecuteChanged();
        MarkClearedCommand.NotifyCanExecuteChanged();
        EditInjuryCommand.NotifyCanExecuteChanged();
        RetryPlanRefreshCommand.NotifyCanExecuteChanged();
        RetryPageRefreshCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSiteFlagChanged(WorkoutInjurySite value)
    {
        RefreshSaveState();
    }

    partial void OnSelectedStatusChanged(WorkoutInjuryStatus? value)
    {
        RefreshSaveState();
    }

    partial void OnSinceDateValueChanged(DateTime value)
    {
        RefreshSaveState();
    }

    private void RefreshSaveState()
    {
        OnPropertyChanged(nameof(CanSaveInjury));
        SaveInjuryCommand.NotifyCanExecuteChanged();
    }

    partial void OnNewPainLevelChanged(int value)
    {
        OnPropertyChanged(nameof(PainLabelText));
    }

    partial void OnEditingInjuryIdChanged(int? value)
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(FormTitle));
    }

    partial void OnShowAddFormChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private async Task LoadAsync()
    {
        CancelEdit();
        IsBusy = true;
        IsLoadError = false;
        ErrorText = string.Empty;
        try
        {
            if (_pendingChangeRefresh)
                await RefreshAfterChangeAsync();
            else
                await ReloadInjuriesAsync();
        }
        catch (Exception ex)
        {
            IsLoadError = true;
            ErrorText = "Couldn't load your injury log. Try again.";
            Trace.WriteLine($"[InjuryLog] Load failed ({ex.GetType().Name}).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadInjuriesAsync()
    {
        var allLogs = await _injuries.GetAllInjuriesAsync();

        var active = new List<InjuryLogItem>();
        var cleared = new List<InjuryLogItem>();

        foreach (var log in allLogs)
        {
            var siteFlag = log.Site;
            var status = log.Status;
            var isActive = log.IsActive;

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
                SinceText = log.Since.ToString("d", CultureInfo.CurrentCulture),
                IsActive = isActive
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
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDetail));
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
            await _injuries.SaveInjuryAsync(new SaveWorkoutInjuryInput(
                EditingInjuryId ?? 0, SelectedSiteFlag,
                status.Value, NewPainLevel,
                DateOnly.FromDateTime(SinceDateValue)));
            CancelEdit();
            _pendingChangeRefresh = true;
            await RefreshAfterChangeAsync();
        }
        catch (Exception ex)
        {
            ErrorText = "Couldn't save this injury. Your changes are still here; try again.";
            Trace.WriteLine($"[InjuryLog] Save failed ({ex.GetType().Name}).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteInjuryActionAsync(int injuryId, string errorMessage, string operationName,
        Func<Task> actionAsync)
    {
        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            await actionAsync();
            if (EditingInjuryId == injuryId)
                CancelEdit();
            _pendingChangeRefresh = true;
            await RefreshAfterChangeAsync();
        }
        catch (Exception ex)
        {
            ErrorText = errorMessage;
            Trace.WriteLine($"[InjuryLog] {operationName} failed ({ex.GetType().Name}).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteInjuryAsync(int id)
    {
        await ExecuteInjuryActionAsync(id, "Couldn't delete this injury. Try again.", "Delete",
            () => _injuries.DeleteInjuryAsync(id));
    }

    private async Task MarkClearedAsync(InjuryLogItem? item)
    {
        if (item is not { IsActive: true } || IsBusy)
            return;

        await ExecuteInjuryActionAsync(item.Id, "Couldn't clear this injury. Try again.", "Clear",
            () => _injuries.ClearInjuryAsync(item.Id));
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
    ///     Resets the add/edit form to a blank state. Called when the hosting
    ///     modal or page opens so that stale edit state never carries over.
    /// </summary>
    public void ResetForm()
    {
        CancelEdit();
    }

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
        IsPageRefreshError = false;
        IsLoadError = false;
        ErrorText = string.Empty;
        try
        {
            await ReloadInjuriesAsync();
        }
        catch (Exception ex)
        {
            ActiveInjuries = new ObservableCollection<InjuryLogItem>();
            ClearedInjuries = new ObservableCollection<InjuryLogItem>();
            OnPropertyChanged(nameof(HasActiveInjuries));
            OnPropertyChanged(nameof(HasClearedInjuries));
            IsLoadError = true;
            ErrorText = UiTextCatalog.Get(ReloadAfterChangeFailedTextKey);
            Trace.WriteLine($"[InjuryLog] Reload after change failed ({ex.GetType().Name}).");
            return;
        }

        try
        {
            await _injuries.RefreshPlanAfterInjuryChangeAsync();
        }
        catch (Exception ex)
        {
            IsPlanRefreshError = true;
            ErrorText = UiTextCatalog.Get(PlanRefreshFailedTextKey);
            Trace.WriteLine($"[InjuryLog] Plan refresh failed ({ex.GetType().Name}).");
        }

        try
        {
            await _injuriesChangedAsync();
        }
        catch (Exception ex)
        {
            IsPageRefreshError = true;
            if (!IsPlanRefreshError)
                ErrorText = UiTextCatalog.Get(PageRefreshFailedTextKey);
            Trace.WriteLine($"[InjuryLog] Page refresh failed ({ex.GetType().Name}).");
        }

        if (!IsPlanRefreshError && !IsPageRefreshError)
            _pendingChangeRefresh = false;
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

    private async Task RetryPageRefreshAsync()
    {
        IsBusy = true;
        try
        {
            await _injuriesChangedAsync();
            IsPageRefreshError = false;
            if (!IsPlanRefreshError)
            {
                ErrorText = string.Empty;
                _pendingChangeRefresh = false;
            }
        }
        catch (Exception ex)
        {
            IsPageRefreshError = true;
            if (!IsPlanRefreshError)
                ErrorText = UiTextCatalog.Get(PageRefreshFailedTextKey);
            Trace.WriteLine($"[InjuryLog] Page refresh retry failed ({ex.GetType().Name}).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string PainLabelFor(int level)
    {
        return level switch
        {
            0 => "None",
            1 => "Minimal",
            2 => "Mild",
            3 => "Moderate",
            4 => "Strong",
            5 => "Severe",
            _ => "None"
        };
    }

    internal static string DisplayNameFor(WorkoutInjurySite site)
    {
        return site switch
        {
            WorkoutInjurySite.Foot => "Foot",
            WorkoutInjurySite.LowBack => "Low back",
            _ => site.ToString()
        };
    }
}
