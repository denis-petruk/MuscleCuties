using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Workout.Injury;

namespace MuscleCuties.UI.ViewModels.Workout;

public partial class InjuryLogViewModel : ObservableObject, IPageLoadAware
{
    private readonly IWorkoutInjuryUseCase _injuries;
    private readonly Func<Task> _injuriesChangedAsync;

    [ObservableProperty] private ObservableCollection<InjuryLogItem> _activeInjuries = new();
    [ObservableProperty] private ObservableCollection<InjuryLogItem> _clearedInjuries = new();
    [ObservableProperty] private int? _editingInjuryId;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorText = string.Empty;

    private bool _hasLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isHistoryExpanded;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isPlanRefreshError;
    [ObservableProperty] private int _newPainLevel;

    [ObservableProperty] private InjuryFlag _selectedSiteFlag;
    [ObservableProperty] private InjuryStatus? _selectedStatus;

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
            new() { Flag = InjuryFlag.Shoulder, Label = "Shoulder" },
            new() { Flag = InjuryFlag.Neck, Label = "Neck" },
            new() { Flag = InjuryFlag.LowBack, Label = "Low back" },
            new() { Flag = InjuryFlag.Hip, Label = "Hip" },
            new() { Flag = InjuryFlag.Knee, Label = "Knee" },
            new() { Flag = InjuryFlag.Ankle, Label = "Ankle" },
            new() { Flag = InjuryFlag.Metatarsal, Label = "Foot" },
            new() { Flag = InjuryFlag.Wrist, Label = "Wrist" }
        };

        AvailableStatuses = new List<InjuryStatusOption>
        {
            new() { Status = InjuryStatus.Acute, Label = "Acute" },
            new() { Status = InjuryStatus.Recovering, Label = "Recovering" },
            new() { Status = InjuryStatus.Cleared, Label = "Cleared" }
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

    public bool CanSaveInjury => !IsBusy && SelectedSiteFlag != InjuryFlag.None && SelectedStatus is not null &&
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
    }

    partial void OnSelectedSiteFlagChanged(InjuryFlag value)
    {
        RefreshSaveState();
    }

    partial void OnSelectedStatusChanged(InjuryStatus? value)
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
            if (IsPlanRefreshError)
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
            var siteFlag = (InjuryFlag)log.Site;
            var status = (InjuryStatus)log.Status;
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
                EditingInjuryId ?? 0, (WorkoutInjurySite)SelectedSiteFlag,
                (WorkoutInjuryStatus)status.Value, NewPainLevel,
                DateOnly.FromDateTime(SinceDateValue)));
            CancelEdit();
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
        ErrorText = string.Empty;
        try
        {
            await ReloadInjuriesAsync();
            await _injuries.RefreshPlanAfterInjuryChangeAsync();
        }
        catch (Exception ex)
        {
            IsPlanRefreshError = true;
            ErrorText = "Your injury log was saved, but the plan couldn't refresh. Try again before training.";
            Trace.WriteLine($"[InjuryLog] Plan refresh failed ({ex.GetType().Name}).");
        }

        try
        {
            await _injuriesChangedAsync();
        }
        catch (Exception ex)
        {
            IsPlanRefreshError = true;
            ErrorText = "Your injury log was saved, but the page couldn't refresh. Try again.";
            Trace.WriteLine($"[InjuryLog] Page refresh failed ({ex.GetType().Name}).");
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

    internal static string DisplayNameFor(InjuryFlag flag)
    {
        return flag switch
        {
            InjuryFlag.Metatarsal => "Foot",
            InjuryFlag.LowBack => "Low back",
            _ => flag.ToString()
        };
    }
}
