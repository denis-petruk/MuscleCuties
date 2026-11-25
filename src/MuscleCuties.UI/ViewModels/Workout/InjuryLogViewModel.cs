using System.Diagnostics.CodeAnalysis;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.CheckInAndInjury;

namespace MuscleCuties.UI.ViewModels.Workout;

/// <summary>One selectable answer to the worsening-movement question; the label is a localization key.</summary>
public sealed record InjuryWorseningOption(
    MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger Trigger,
    string Label);

/// <summary>The injury log: active and cleared entries, the add/edit form with its required answers, and the</summary>
/// plan and page refreshes an injury change forces.</summary>
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
    private bool? _editingClinicianAdvisedAvoid;
    private bool _clinicianAdviceTouched;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isHistoryExpanded;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isPlanRefreshError;
    [ObservableProperty] private bool _isPageRefreshError;
    [ObservableProperty] private int _newPainLevel;

    [ObservableProperty] private WorkoutInjurySite _selectedSiteFlag;
    [ObservableProperty] private WorkoutInjuryStatus? _selectedStatus;

    [ObservableProperty] private bool _showAddForm;

    // F01: unanswered, yes and no are three distinct answers. Unanswered is preserved as null; it
    // is never downgraded to an explicit "no" just because the old switch could only show two states.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClinicianAdvisedAvoid), nameof(HasClinicianAdviceAnswer),
        nameof(IsClinicianAdviceYes), nameof(IsClinicianAdviceNo), nameof(IsClinicianAdviceUnanswered),
        nameof(ShowsSafetyGuidance))]
    private bool? _selectedClinicianAdvice;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowsSafetyGuidance))]
    private InjuryWorseningOption? _selectedWorseningOption;
    [ObservableProperty] private DateTime _sinceDateValue = DateTime.Today;

    /// <summary>Takes the injury use case and the navigation callbacks.</summary>
    public InjuryLogViewModel(
        IWorkoutInjuryUseCase injuries,
        Func<Task> navigateBackAsync,
        Func<Task>? injuriesChangedAsync = null)
    {
        _injuries = injuries;
        _injuriesChangedAsync = injuriesChangedAsync ?? (() => Task.CompletedTask);

        AvailableSites = new List<InjuryFlagOption>
        {
            new() { Flag = WorkoutInjurySite.Shoulder, Label = UiTextCatalog.Get("Injury_Site_Shoulder") },
            new() { Flag = WorkoutInjurySite.Neck, Label = UiTextCatalog.Get("Injury_Site_Neck") },
            new() { Flag = WorkoutInjurySite.LowBack, Label = UiTextCatalog.Get("Injury_Site_LowBack") },
            new() { Flag = WorkoutInjurySite.Hip, Label = UiTextCatalog.Get("Injury_Site_Hip") },
            new() { Flag = WorkoutInjurySite.Knee, Label = UiTextCatalog.Get("Injury_Site_Knee") },
            new() { Flag = WorkoutInjurySite.Ankle, Label = UiTextCatalog.Get("Injury_Site_Ankle") },
            new() { Flag = WorkoutInjurySite.Foot, Label = UiTextCatalog.Get("Injury_Site_Foot") },
            new() { Flag = WorkoutInjurySite.Wrist, Label = UiTextCatalog.Get("Injury_Site_Wrist") }
        };

        AvailableStatuses = new List<InjuryStatusOption>
        {
            new() { Status = WorkoutInjuryStatus.Acute, Label = UiTextCatalog.Get("Injury_Status_Acute") },
            new() { Status = WorkoutInjuryStatus.Recovering, Label = UiTextCatalog.Get("Injury_Status_Recovering") },
            new() { Status = WorkoutInjuryStatus.Cleared, Label = UiTextCatalog.Get("Injury_Status_Cleared") }
        };

        WorseningTriggerOptions = new List<InjuryWorseningOption>
        {
            new(MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.NotAnswered,
                "Injury_Log_WorseningTrigger_NotAnswered"),
            new(MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.None,
                "Injury_Log_WorseningTrigger_None"),
            new(MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.OverheadReach,
                "Injury_Log_WorseningTrigger_OverheadReach"),
            new(MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.DeepKneeFlexion,
                "Injury_Log_WorseningTrigger_DeepKneeFlexion"),
            new(MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.SpinalLoading,
                "Injury_Log_WorseningTrigger_SpinalLoading"),
            new(MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.ImpactAndGait,
                "Injury_Log_WorseningTrigger_ImpactAndGait"),
            new(MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.GripAndWristLoad,
                "Injury_Log_WorseningTrigger_GripAndWristLoad")
        };
        SelectedWorseningOption = WorseningTriggerOptions[0];

        // F01: three visible answers, one of which is deliberately unanswered (null).
        ClinicianAdviceOptions = new List<InjuryAdviceOption>
        {
            new() { Value = null, LabelKey = "Injury_Log_ClinicianAdvice_Unanswered", IsSelected = true },
            new() { Value = true, LabelKey = "Injury_Log_ClinicianAdvice_Yes" },
            new() { Value = false, LabelKey = "Injury_Log_ClinicianAdvice_No" }
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
        SelectClinicianAdviceOptionCommand = new RelayCommand<InjuryAdviceOption>(SelectClinicianAdviceOption);
        ToggleHistoryCommand = new RelayCommand(() => IsHistoryExpanded = !IsHistoryExpanded);
        RetryPageRefreshCommand = new AsyncRelayCommand(RetryPageRefreshAsync, () => !IsBusy);
    }

    /// <summary>True when an injury is being edited.</summary>
    public bool IsEditing => EditingInjuryId is not null;
    /// <summary>Heading for the form, worded for editing or for a new entry.</summary>
    public string FormTitle => UiTextCatalog.Get(IsEditing ? "Injury_Log_FormEdit" : "Injury_Log_FormNew");
    /// <summary>The chosen pain level as words rather than a number.</summary>
    public string PainLabelText => PainLabelFor(NewPainLevel);

    /// <summary>The worsening triggers to choose from.</summary>
    public IReadOnlyList<InjuryWorseningOption> WorseningTriggerOptions { get; }
    /// <summary>The clinician advice answers.</summary>
    public IReadOnlyList<InjuryAdviceOption> ClinicianAdviceOptions { get; }

    /// <summary>
    ///     Owner rule: recorded worsening symptoms or a clinician restriction are safety concerns.
    ///     The stop-and-seek-care guidance appears whenever either answer is affirmative.
    /// </summary>
    public bool ShowsSafetyGuidance =>
        MuscleCuties.Core.Domain.Workout.InjuryObservationRule.IndicatesSafetyConcern(
            SelectedWorseningOption?.Trigger ??
            MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.NotAnswered,
            ClinicianAdvisedAvoid);
    /// <summary>True when injuries have cleared.</summary>
    public bool HasClearedInjuries => ClearedInjuries.Count > 0;
    /// <summary>True when injuries are active.</summary>
    public bool HasActiveInjuries => ActiveInjuries.Count > 0;

    /// <summary>True when only cleared injuries remain.</summary>
    public bool HasNoActiveInjuries => _hasLoaded && !IsBusy && !IsLoadError &&
                                       ActiveInjuries.Count == 0 && ClearedInjuries.Count > 0;

    /// <summary>True when the user logged no injuries.</summary>
    public bool HasNoInjuries => _hasLoaded && !IsBusy && !IsLoadError &&
                                 ActiveInjuries.Count == 0 && ClearedInjuries.Count == 0;

    /// <summary>True when the empty state is shown.</summary>
    public bool ShowEmptyState => _hasLoaded && !IsBusy && !IsLoadError &&
                                  !ShowAddForm && ActiveInjuries.Count == 0;

    /// <summary>Empty-state heading that distinguishes a user who never logged an injury from one whose injuries have</summary>
    /// all cleared.</summary>
    public string EmptyStateTitle => UiTextCatalog.Get(ClearedInjuries.Count == 0
        ? "Injury_Log_EmptyNone_Title"
        : "Injury_Log_EmptyCleared_Title");

    /// <summary>The empty-state body, matching the same two cases.</summary>
    public string EmptyStateDetail => UiTextCatalog.Get(ClearedInjuries.Count == 0
        ? "Injury_Log_EmptyNone_Detail"
        : "Injury_Log_EmptyCleared_Detail");

    /// <summary>True when an error is shown.</summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorText);
    public bool ClinicianAdvisedAvoid
    {
        get => SelectedClinicianAdvice == true;
        set
        {
            SelectedClinicianAdvice = value;
            _clinicianAdviceTouched = true;
        }
    }
    /// <summary>True when the advice question is answered.</summary>
    public bool HasClinicianAdviceAnswer => SelectedClinicianAdvice is not null;
    /// <summary>True when the advice answer is yes.</summary>
    public bool IsClinicianAdviceYes => SelectedClinicianAdvice == true;
    /// <summary>True when the advice answer is no.</summary>
    public bool IsClinicianAdviceNo => SelectedClinicianAdvice == false;
    /// <summary>True when the advice answer is missing.</summary>
    public bool IsClinicianAdviceUnanswered => SelectedClinicianAdvice is null;

    /// <summary>Save needs a site, a status and a start date that is not in the future.</summary>
    public bool CanSaveInjury => !IsBusy && SelectedSiteFlag != WorkoutInjurySite.None && SelectedStatus is not null &&
                                 SinceDateValue.Date <= DateTime.Today;

    /// <summary>True when the form still needs an answer before saving.</summary>
    public bool ShowSaveInjuryPrerequisiteHint => !CanSaveInjury && !IsBusy;

    /// <summary>Names the answers still missing, combining both when neither is given, and stays empty once saving is</summary>
    /// possible.</summary>
    public string SaveInjuryPrerequisiteHint => (SelectedSiteFlag == WorkoutInjurySite.None, SelectedStatus is null) switch
    {
        (true, true) => UiTextCatalog.Get("Injury_Log_SavePrerequisiteBoth"),
        (true, false) => UiTextCatalog.Get("Injury_Log_SavePrerequisiteSite"),
        (false, true) => UiTextCatalog.Get("Injury_Log_SavePrerequisiteStatus"),
        _ => string.Empty
    };

    /// <summary>Today's date.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in Injury XAML / view model contract.")]
    public DateTime Today => DateTime.Today;

    /// <summary>The injury sites to choose from.</summary>
    public List<InjuryFlagOption> AvailableSites { get; }
    /// <summary>The injury statuses to choose from.</summary>
    public List<InjuryStatusOption> AvailableStatuses { get; }

    /// <summary>Loads the injuries.</summary>
    public AsyncRelayCommand LoadCommand { get; }
    /// <summary>Shows or hides the add form.</summary>
    public RelayCommand ToggleAddFormCommand { get; }
    /// <summary>Saves the injury.</summary>
    public AsyncRelayCommand SaveInjuryCommand { get; }
    /// <summary>Deletes the injury.</summary>
    public AsyncRelayCommand<int> DeleteInjuryCommand { get; }
    /// <summary>Marks the injury as cleared.</summary>
    public AsyncRelayCommand<InjuryLogItem> MarkClearedCommand { get; }
    /// <summary>Returns to the previous page.</summary>
    public AsyncRelayCommand NavigateBackCommand { get; }
    /// <summary>Opens the injury for editing.</summary>
    public RelayCommand<InjuryLogItem> EditInjuryCommand { get; }
    /// <summary>Leaves the edit form.</summary>
    public RelayCommand CancelEditCommand { get; }
    /// <summary>Selects the injury site.</summary>
    public RelayCommand<InjuryFlagOption> SelectSiteCommand { get; }
    /// <summary>Selects the injury status.</summary>
    public RelayCommand<InjuryStatusOption> SelectStatusCommand { get; }
    /// <summary>Selects the clinician advice answer.</summary>
    public RelayCommand<InjuryAdviceOption> SelectClinicianAdviceOptionCommand { get; }
    /// <summary>Shows or hides the cleared history.</summary>
    public RelayCommand ToggleHistoryCommand { get; }
    /// <summary>Retries the page refresh.</summary>
    public AsyncRelayCommand RetryPageRefreshCommand { get; }

    /// <summary>True while the page loads.</summary>
    public bool IsPageLoading => IsBusy;
    /// <summary>Loads the injuries.</summary>
    public AsyncRelayCommand LoadDataCommand => LoadCommand;

    /// <summary>Refreshes the page state when busy changes.</summary>
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
        RetryPageRefreshCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Refreshes the form when the site changes.</summary>
    partial void OnSelectedSiteFlagChanged(WorkoutInjurySite value)
    {
        RefreshSaveState();
    }

    /// <summary>Refreshes the form when the status changes.</summary>
    partial void OnSelectedStatusChanged(WorkoutInjuryStatus? value)
    {
        RefreshSaveState();
    }

    /// <summary>Applies the tapped yes/no answer; a cleared selection is ignored so the question cannot be un-answered.</summary>
    private void SelectClinicianAdviceOption(InjuryAdviceOption? option)
    {
        if (option is null)
            return;

        SelectedClinicianAdvice = option.Value;
        _clinicianAdviceTouched = true;
    }

    // Keeps the three segmented cells in step with the answer, including a restored edit or a
    // reset back to unanswered. The selected cell is what VoiceOver announces, so the visual and
    // the accessible state cannot diverge.
    /// <summary>Refreshes the form when the advice answer changes.</summary>
    partial void OnSelectedClinicianAdviceChanged(bool? value)
    {
        foreach (var option in ClinicianAdviceOptions)
            option.IsSelected = option.Value == value;
    }

    /// <summary>Refreshes the form when the start date changes.</summary>
    partial void OnSinceDateValueChanged(DateTime value)
    {
        RefreshSaveState();
    }

    /// <summary>Re-evaluates the save button and its hint after any field that can make the form saveable changes.</summary>
    private void RefreshSaveState()
    {
        OnPropertyChanged(nameof(CanSaveInjury));
        OnPropertyChanged(nameof(ShowSaveInjuryPrerequisiteHint));
        OnPropertyChanged(nameof(SaveInjuryPrerequisiteHint));
        SaveInjuryCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Refreshes the form when the pain level changes.</summary>
    partial void OnNewPainLevelChanged(int value)
    {
        OnPropertyChanged(nameof(PainLabelText));
    }

    /// <summary>Refreshes the edit mode when the injury changes.</summary>
    partial void OnEditingInjuryIdChanged(int? value)
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(FormTitle));
    }

    /// <summary>Refreshes the empty state when the form opens.</summary>
    partial void OnShowAddFormChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    /// <summary>Loads the page from scratch, abandoning any edit in progress so a stale draft cannot be saved onto the</summary>
    /// reloaded list.</summary>
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
            ErrorText = "Ui_Workout_Error_InjuryLoad";
            Trace.WriteLine($"[InjuryLog] Load failed ({ex.GetType().Name}).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Fills the active and cleared lists and re-publishes the empty-state copy, which depends on both.</summary>
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
                IsActive = isActive,
                IsPainReassessmentDue = isActive && log.PainReassessmentDueAtUtc is { } due && due <= DateTimeOffset.UtcNow,
                WorseningTrigger = log.WorseningTrigger,
                ClinicianAdvisedAvoid = log.ClinicianAdvisedAvoid
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

    /// <summary>Creates or updates the entry, then refreshes the plan because an injury changes what training is safe.</summary>
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
                DateOnly.FromDateTime(SinceDateValue),
                SelectedWorseningOption?.Trigger ??
                    MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.NotAnswered,
                _clinicianAdviceTouched
                    ? SelectedClinicianAdvice
                    : _editingClinicianAdvisedAvoid));
            CancelEdit();
            _pendingChangeRefresh = true;
            await RefreshAfterChangeAsync();
        }
        catch (ActiveWorkoutPreventsInjuryException)
        {
            ErrorText = UiTextCatalog.Get("Injury_Log_ActiveWorkoutPreventsInjury");
        }
        catch (Exception ex)
        {
            ErrorText = "Ui_Workout_Error_InjurySave";
            Trace.WriteLine($"[InjuryLog] Save failed ({ex.GetType().Name}).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Runs an injury action, reporting its failure with the given message.</summary>
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
        catch (ActiveWorkoutPreventsInjuryException)
        {
            ErrorText = UiTextCatalog.Get("Injury_Log_ActiveWorkoutPreventsInjury");
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

    /// <summary>Deletes an entry through the shared action path, which owns the busy state and the failure wording.</summary>
    private async Task DeleteInjuryAsync(int id)
    {
        await ExecuteInjuryActionAsync(id, UiTextCatalog.Get("Injury_Log_DeleteFailed"), "Delete",
            () => _injuries.DeleteInjuryAsync(id));
    }

    /// <summary>Moves an active injury to the cleared list, ignoring entries that are already cleared or a busy form.</summary>
    private async Task MarkClearedAsync(InjuryLogItem? item)
    {
        if (item is not { IsActive: true } || IsBusy)
            return;

        await ExecuteInjuryActionAsync(item.Id, UiTextCatalog.Get("Injury_Log_ClearFailed"), "Clear",
            () => _injuries.ClearInjuryAsync(item.Id));
    }

    /// <summary>Opens an empty form, dropping whatever the previous form held.</summary>
    private void BeginAdd()
    {
        CancelEdit();
        ShowAddForm = true;
    }

    /// <summary>Opens the form filled with an existing entry, which is what makes saving update rather than add.</summary>
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

        // Restore the safety answers so editing an injury never silently downgrades them to
        // "not answered" while the user is only changing the pain level.
        SelectedClinicianAdvice = item.ClinicianAdvisedAvoid;
        _editingClinicianAdvisedAvoid = item.ClinicianAdvisedAvoid;
        _clinicianAdviceTouched = false;
        SelectedWorseningOption =
            WorseningTriggerOptions.FirstOrDefault(option => option.Trigger == item.WorseningTrigger)
            ?? WorseningTriggerOptions[0];

        ShowAddForm = true;
    }

    /// <summary>
    ///     Resets the add/edit form to a blank state. Called when the hosting
    ///     modal or page opens so that stale edit state never carries over.
    /// </summary>
    /// <summary>
    ///     Opens the log from either host page: clears the previous draft, tells the page to show the sheet, then loads
    ///     the current injuries unless a load is already running.
    /// </summary>
    public async Task OpenModalAsync(Action showModal)
    {
        ResetForm();
        showModal();
        if (!IsBusy)
            await LoadCommand.ExecuteAsync(null);
    }

    /// <summary>Returns the form to its closed, empty state when the sheet is dismissed.</summary>
    public void ResetForm()
    {
        CancelEdit();
    }

    /// <summary>Clears the edit identity, the fields and every save or refresh error, so a cancelled edit leaves nothing</summary>
    /// of itself on screen.</summary>
    private void CancelEdit()
    {
        // A cancelled edit must not leave the previous save/reconcile error on screen.
        ErrorText = string.Empty;
        IsLoadError = false;
        IsPlanRefreshError = false;
        IsPageRefreshError = false;
        EditingInjuryId = null;
        _editingClinicianAdvisedAvoid = null;

        SelectedSiteFlag = default;
        foreach (var site in AvailableSites)
            site.IsSelected = false;

        SelectedStatus = null;
        foreach (var status in AvailableStatuses)
            status.IsSelected = false;

        NewPainLevel = 0;
        SinceDateValue = DateTime.Today;
        SelectedClinicianAdvice = null;
        _clinicianAdviceTouched = false;
        SelectedWorseningOption = WorseningTriggerOptions.Count > 0 ? WorseningTriggerOptions[0] : null;

        ShowAddForm = false;
    }

    /// <summary>Selects one site and deselects the rest, keeping the single-choice list consistent with the bound flag.</summary>
    private void SelectSite(InjuryFlagOption? selected)
    {
        if (selected is null) return;

        foreach (var site in AvailableSites)
            site.IsSelected = site == selected;

        SelectedSiteFlag = selected.Flag;
    }

    /// <summary>The same single-choice handling for the status list.</summary>
    private void SelectStatus(InjuryStatusOption? selected)
    {
        if (selected is null) return;

        foreach (var status in AvailableStatuses)
            status.IsSelected = status == selected;

        SelectedStatus = selected.Status;
    }

    /// <summary>Refreshes the plan and the page after a change, keeping whichever half failed flagged so the user can</summary>
    /// retry it without losing the change.</summary>
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

    /// <summary>Retries the page half of a failed refresh, and clears the pending marker once both halves are clean.</summary>
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

    /// <summary>Maps a pain level to its wording, with zero reading as no pain.</summary>
    private static string PainLabelFor(int level)
    {
        var key = level switch
        {
            1 => "Injury_Pain_1",
            2 => "Injury_Pain_2",
            3 => "Injury_Pain_3",
            4 => "Injury_Pain_4",
            5 => "Injury_Pain_5",
            _ => "Injury_Pain_0"
        };
        return UiTextCatalog.Get(key);
    }

    /// <summary>A site's English name, used where the value has to leave the app as text rather than as a catalog key.</summary>
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
