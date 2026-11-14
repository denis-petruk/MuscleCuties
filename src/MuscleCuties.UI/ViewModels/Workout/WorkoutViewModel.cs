using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Pages.Profile;
using MuscleCuties.UI.Pages.Workout;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Api;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Overview;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UI.ViewModels.Workout;

public partial class WorkoutViewModel : ObservableObject, IPageLoadAware, IDisposable
{
    private const int FirstWeekIndex = 0;
    private const int LastWeekIndex = 3;
    private const int MissingWorkoutDayId = 0;
    private const int NoScheduledDays = 0;
    private const int SingleActivityCount = 1;

    private readonly IWorkoutInjuryUseCase _injuries;
    private readonly IStrengthStyleOfferClient? _styleOffers;
    private readonly Func<StrengthStyleOfferDto, Task<WorkoutActivityDto?>>? _chooseStyleAsync;
    private readonly Func<WorkoutActivityDto, Task>? _openStylePreviewAsync;
    private StrengthStyleOfferDto? _styleOffer;
    [ObservableProperty] private bool _hasStrengthStyleOffer;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasStyleOfferMessage))]
    private string? _styleOfferMessage;
    public bool HasStyleOfferMessage => !string.IsNullOrWhiteSpace(StyleOfferMessage);
    private readonly AppPageLoadGate _loadGate = new(AppPageLoadGate.PageFreshnessWindow);
    private readonly Func<string, Task> _navigateAsync;
    private readonly Func<int, Task> _openWorkoutSessionAsync;
    public event Func<int, Task>? SessionActivitiesRequested;
    private readonly IWorkoutOverviewUseCase _overview;

    [ObservableProperty] private string? _activePlanName;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasInProgressWorkout))]
    private WorkoutListEntry? _inProgressWorkout;
    private MuscleCuties.UseCases.Features.Workout.Overview.Contracts.WorkoutOverviewResponse? _lastOverview;
    private bool _hasLoadedOverview;
    private List<WorkoutItem> _allWorkouts = new();
    [ObservableProperty] private ObservableCollection<string> _blockedCardioNames = new();
    [ObservableProperty] private ObservableCollection<string> _blockedExerciseNames = new();
    [ObservableProperty] private string _currentPhaseName = string.Empty;
    private IReadOnlyList<ScheduleDayItem> _datedSchedule = [];
    private IReadOnlyList<WorkoutDayExercisePreview> _dayPreviews = [];
    private WorkoutItem? _featuredWorkout;
    [ObservableProperty] private string _featuredWorkoutActionText =
        UiTextCatalog.Get("Dashboard_Workout_StartWorkout");
    [ObservableProperty] private bool _featuredWorkoutHasAction;
    [ObservableProperty] private bool _featuredWorkoutIsEditAction;
    [ObservableProperty] private bool _featuredWorkoutIsRestDay = true;

    [ObservableProperty] private Color _featuredWorkoutActivityBackground =
        WorkoutPresentationPalette.Background("STRENGTH");

    [ObservableProperty] private ObservableCollection<WorkoutActivitySection> _featuredWorkoutActivitySections = [];

    [ObservableProperty] private Color _featuredWorkoutActivityTextColor =
        WorkoutPresentationPalette.Text("STRENGTH");

    [ObservableProperty] private string _featuredWorkoutBadgeText =
        UiTextCatalog.Get("Dashboard_Workout_TodayRecovery");
    [ObservableProperty] private string _featuredWorkoutDurationText =
        UiTextCatalog.Get("Dashboard_Workout_Flexible");
    [ObservableProperty] private ObservableCollection<string> _featuredWorkoutExercisePreviews = [];
    [ObservableProperty] private string _featuredWorkoutExercisesCount = "0";
    [ObservableProperty] private bool _featuredWorkoutHasMultipleActivities;
    [ObservableProperty] private string _featuredWorkoutIntensity =
        UiTextCatalog.Get("Dashboard_Workout_IntensityLight");
    [ObservableProperty] private string _featuredWorkoutSubtitle =
        UiTextCatalog.Get("Dashboard_Workout_RecoverySubtitle");
    // C2: the rest-day copy is authored by the server; the client never substitutes its own.
    [ObservableProperty] private string _featuredWorkoutTitle = string.Empty;
    [ObservableProperty] private ObservableCollection<FilterChipItem> _filters = new();

    // The week picker uses the same chip item and the same shared card selection state as the
    // filters below it, so the screen has one selection language instead of two.
    [ObservableProperty] private ObservableCollection<FilterChipItem> _weeks = new();

    [ObservableProperty] private bool _hasActiveInjuries;
    [ObservableProperty] private string _injuryBannerSummary = string.Empty;
    private InjuryLogViewModel? _injuryLogVm;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;

    [ObservableProperty] private bool _isInjuryBannerExpanded;

    [ObservableProperty] private bool _isInjuryModalVisible;
    [ObservableProperty] private bool _isSessionActivitiesVisible;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isPlanStale = true;
    [ObservableProperty] private ObservableCollection<string> _rehabExerciseNames = new();
    [ObservableProperty] private ObservableCollection<ScheduleDayItem> _scheduleDays = [];
    [ObservableProperty] private ScheduleDayItem? _selectedScheduleDay;
    [ObservableProperty] private int _selectedWeekIndex;
    [ObservableProperty] private bool _isDayScopeOnly;
    [ObservableProperty] private bool _hasPlannedSessions;
    [ObservableProperty] private bool _hasCompletedTodaySession;
    [ObservableProperty] private bool _hasPendingReviewFlags;

    [ObservableProperty] private string _weekTitle = "This week's plan";
    [ObservableProperty] private ObservableCollection<WorkoutItem> _workouts = new();

    [ObservableProperty] private bool _isRescheduleModalVisible;
    [ObservableProperty] private bool _isRescheduleBusy;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasRescheduleStatusMessage))]
    private string _rescheduleStatusMessage = string.Empty;
    public bool HasRescheduleStatusMessage => !string.IsNullOrWhiteSpace(RescheduleStatusMessage);
    [ObservableProperty] private WorkoutItem? _reschedulingWorkout;
    [ObservableProperty] private string _reschedulingWorkoutTitle = string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasAvailableRescheduleDates), nameof(CanConfirmReschedule))]
    private ObservableCollection<RescheduleDateOption> _availableRescheduleDates = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanConfirmReschedule))]
    private RescheduleDateOption? _selectedRescheduleDate;
    public bool HasAvailableRescheduleDates => AvailableRescheduleDates.Count > 0;
    public bool CanConfirmReschedule => SelectedRescheduleDate is not null && !IsRescheduleBusy;

    public WorkoutViewModel(
        IWorkoutOverviewUseCase overview,
        IWorkoutInjuryUseCase injuries,
        Func<string, Task>? navigateAsync = null,
        Func<int, Task>? openWorkoutSessionAsync = null,
        IStrengthStyleOfferClient? styleOffers = null,
        Func<StrengthStyleOfferDto, Task<WorkoutActivityDto?>>? chooseStyleAsync = null,
        Func<WorkoutActivityDto, Task>? openStylePreviewAsync = null)
    {
        _overview = overview;
        _injuries = injuries;
        _styleOffers = styleOffers;
        _chooseStyleAsync = chooseStyleAsync;
        _openStylePreviewAsync = openStylePreviewAsync;
        _navigateAsync = navigateAsync ?? (_ => Task.CompletedTask);
        _openWorkoutSessionAsync = openWorkoutSessionAsync ?? (_ => Task.CompletedTask);
        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync, this));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        EmptyWorkoutsActionCommand = new AsyncRelayCommand(HandleEmptyWorkoutsActionAsync, CanUseEmptyWorkoutsAction);
        GeneratePlanCommand = new AsyncRelayCommand(GeneratePlanAsync, () => !IsBusy);
        OpenHistoryCommand = new AsyncRelayCommand(() => _navigateAsync(nameof(WorkoutHistoryPage)));
        ResumeInProgressWorkoutCommand = new AsyncRelayCommand(ResumeInProgressWorkoutAsync);
        OpenWorkoutCommand = new AsyncRelayCommand<WorkoutItem>(OpenWorkoutAsync);
        ToggleSessionOptionalCommand = new AsyncRelayCommand<WorkoutItem>(ToggleSessionOptionalAsync);
        ToggleEasierAlternativeCommand = new AsyncRelayCommand<WorkoutItem>(ToggleEasierAlternativeAsync);
        SelectFilterCommand = new RelayCommand<FilterChipItem>(SelectFilter);
        StartFeaturedWorkoutCommand = new AsyncRelayCommand(OpenFeaturedWorkoutAsync);
        SelectWeekCommand = new RelayCommand<int>(SelectWeek);
        SelectWeekChipCommand = new RelayCommand<FilterChipItem>(SelectWeekChip);
        SelectScheduleDayCommand = new RelayCommand<ScheduleDayItem>(SelectScheduleDay);
        ToggleInjuryBannerCommand = new RelayCommand(() => IsInjuryBannerExpanded = !IsInjuryBannerExpanded);
        NavigateToInjuryLogCommand = new AsyncRelayCommand(OpenInjuryModalAsync);
        StartRescheduleCommand = new RelayCommand<WorkoutItem>(StartReschedule);
        SelectRescheduleDateCommand = new RelayCommand<RescheduleDateOption>(SelectRescheduleDate);
        ConfirmRescheduleCommand = new AsyncRelayCommand(ConfirmRescheduleAsync, () => CanConfirmReschedule);
        CloseRescheduleModalCommand = new RelayCommand(CloseRescheduleModal);
        Filters = new ObservableCollection<FilterChipItem>
        {
            new() { Column = 0, Label = UiTextCatalog.Get("Workout_Filter_All"), IsSelected = true },
            new() { Column = 1, Label = UiTextCatalog.Get("Workout_Filter_Strength") },
            new() { Column = 2, Label = UiTextCatalog.Get("Workout_Filter_Cardio") },
            new() { Column = 3, Label = UiTextCatalog.Get("Workout_Filter_Recovery") }
        };
        Weeks = new ObservableCollection<FilterChipItem>
        {
            new() { Column = 0, Index = FirstWeekIndex, Label = UiTextCatalog.Get("Workout_Page_Week1"), IsSelected = true },
            new() { Column = 1, Index = FirstWeekIndex + 1, Label = UiTextCatalog.Get("Workout_Page_Week2") },
            new() { Column = 2, Index = FirstWeekIndex + 2, Label = UiTextCatalog.Get("Workout_Page_Week3") },
            new() { Column = 3, Index = FirstWeekIndex + 3, Label = UiTextCatalog.Get("Workout_Page_Week4") }
        };
    }

    private void SyncWeekChips(int weekIndex)
    {
        foreach (var week in Weeks)
            week.IsSelected = week.Index == weekIndex;
    }

    public bool HasBlockedExercises => BlockedExerciseNames.Count > 0;
    public bool HasRehabExercises => RehabExerciseNames.Count > 0;
    public bool HasBlockedCardio => BlockedCardioNames.Count > 0;
    public bool HasKnownCyclePhase => !string.IsNullOrWhiteSpace(CurrentPhaseName) &&
                                      !CurrentPhaseName.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

    partial void OnCurrentPhaseNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasKnownCyclePhase));
    }

    public bool IsTabBarVisible => !IsInjuryModalVisible && !IsSessionActivitiesVisible && !IsRescheduleModalVisible;

    partial void OnIsInjuryModalVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(IsTabBarVisible));

    partial void OnIsSessionActivitiesVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(IsTabBarVisible));

    partial void OnIsRescheduleModalVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(IsTabBarVisible));

    partial void OnInProgressWorkoutChanged(WorkoutListEntry? value) =>
        ApplyFeaturedWorkout(SelectedScheduleDay);

    public InjuryLogViewModel InjuryLogVm => _injuryLogVm ??= new InjuryLogViewModel(
        _injuries, () => Task.CompletedTask, RefreshAfterInjuryChangeAsync);

    public bool HasNoPlan => string.IsNullOrEmpty(ActivePlanName);
    public bool HasInProgressWorkout => InProgressWorkout is { IsInProgress: true, WorkoutDayId: > MissingWorkoutDayId };
    public bool ShowNoPlanPrompt => _hasLoadedOverview && HasNoPlan && HasNoWorkouts;
    public bool HasWorkouts => !IsBusy && Workouts.Count > 0;
    public bool HasNoWorkouts => !IsBusy && !IsLoadError && Workouts.Count == 0;
    public string ActivePlanTitle => ActivePlanName ?? "No active plan";

    public string PlanSummaryText => _lastOverview is null
        ? "No sessions scheduled yet"
        : MuscleCuties.Infrastructure.Workout.Overview.PlanSummaryFormatter.Format(
            _lastOverview, SelectedWeekIndex);

    public string EmptyWorkoutsTitle => HasNoPlan
        ? "Workout_Generation_PromptNoPlanTitle"
        : SelectedFilterLabel == "All"
            ? "No workouts scheduled"
            : $"No {SelectedFilterLabel.ToLowerInvariant()} workouts";

    public string EmptyWorkoutsSubtitle => HasNoPlan
        ? "Workout_Generation_PromptNoPlanSubtitle"
        : "Try another filter or show the full week.";

    public string EmptyWorkoutsButtonText => HasNoPlan ? "Workout_Generation_PromptGeneratePlanButton" : "Show All";
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound in workout page XAML.")]
    public bool EmptyWorkoutsButtonVisible => true;

    private string SelectedFilterLabel => Filters.FirstOrDefault(f => f.IsSelected)?.Label ?? "All";
    public AsyncRelayCommand EmptyWorkoutsActionCommand { get; }
    public AsyncRelayCommand GeneratePlanCommand { get; }
    public AsyncRelayCommand OpenHistoryCommand { get; }
    public AsyncRelayCommand ResumeInProgressWorkoutCommand { get; }
    public AsyncRelayCommand<WorkoutItem> OpenWorkoutCommand { get; }
    public AsyncRelayCommand<WorkoutItem> ToggleSessionOptionalCommand { get; }
    public AsyncRelayCommand<WorkoutItem> ToggleEasierAlternativeCommand { get; }
    public RelayCommand<FilterChipItem> SelectFilterCommand { get; }
    public AsyncRelayCommand StartFeaturedWorkoutCommand { get; }
    public RelayCommand<int> SelectWeekCommand { get; }
    public RelayCommand<FilterChipItem> SelectWeekChipCommand { get; }
    public RelayCommand<ScheduleDayItem> SelectScheduleDayCommand { get; }
    public RelayCommand ToggleInjuryBannerCommand { get; }
    public AsyncRelayCommand NavigateToInjuryLogCommand { get; }
    public RelayCommand<WorkoutItem> StartRescheduleCommand { get; }
    public RelayCommand<RescheduleDateOption> SelectRescheduleDateCommand { get; }
    public AsyncRelayCommand ConfirmRescheduleCommand { get; }
    public RelayCommand CloseRescheduleModalCommand { get; }

    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    // The spinner shows only while a load is actually running. A stale plan is refreshed on the next appearance;
    // showing a spinner for staleness alone could stay on screen when no reload starts (F04).
    public bool IsPageLoading => IsBusy;

    [RelayCommand]
    private async Task OpenInjuryModalAsync()
    {
        InjuryLogVm.ResetForm();
        IsInjuryModalVisible = true;
        if (!InjuryLogVm.IsBusy)
            await InjuryLogVm.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void CloseInjuryModal()
    {
        _injuryLogVm?.ResetForm();
        IsInjuryModalVisible = false;
    }

    private async Task RefreshAfterInjuryChangeAsync()
    {
        HasActiveInjuries = InjuryLogVm.HasActiveInjuries;
        Invalidate();
        await _loadGate.RunAsync(LoadDataCoreAsync, this, true);
    }

    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            IsRefreshing = false;
            return;
        }

        try
        {
            await _loadGate.RunAsync(LoadDataCoreAsync, this, true);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public void Invalidate()
    {
        _loadGate.MarkStale();
    }

    private async Task LoadDataCoreAsync()
    {
        var weekToRestore = SelectedWeekIndex;
        IsBusy = true;
        var loadStarted = Stopwatch.GetTimestamp();
        try
        {
            await LoadStrengthStyleOfferAsync();
            var summary = await _overview.LoadAsync(new WorkoutOverviewRequest(
                DateOnly.FromDateTime(DateTime.Today)));
            _lastOverview = summary;
            _hasLoadedOverview = true;
            WriteLoadStage("overview", loadStarted);
            CurrentPhaseName = summary.PhaseName;
            ActivePlanName = summary.PlanName;
            InProgressWorkout = summary.InProgressWorkout;
            _allWorkouts = summary.Workouts.Select(item => new WorkoutItem
            {
                WorkoutDayId = item.WorkoutDayId,
                Tag = item.Tag,
                DayLabel = item.DayLabel,
                Title = item.Title,
                Duration = item.Duration,
                ExerciseCount = item.ExerciseCount,
                ActivityCount = item.ActivityCount,
                DetailsText = item.DetailsText,
                IsRestDay = item.IsRestDay,
                IsCompleted = item.IsCompleted,
                IsOptional = item.IsOptional,
                IsEasierAlternative = item.IsEasierAlternative,
                SessionProgressText = item.IsCompleted
                    ? "Workout_Overview_Status_Completed"
                    : "Workout_Overview_Status_Upcoming",
                ActivityBackground = WorkoutPresentationPalette.Background(item.Tag),
                ActivityTextColor = WorkoutPresentationPalette.Text(item.Tag)
            }).ToList();
            _dayPreviews = summary.ExercisePreviews;
            _datedSchedule = summary.Schedule.Select(day =>
            {
                var sessions = day.Sessions is { Count: > 0 }
                    ? day.Sessions.OrderBy(s => s.DayOrder ?? 0).Select(s => new ScheduleSessionChip(
                        s.WorkoutDayId,
                        s.Tag,
                        WorkoutPresentationPalette.Background(s.Tag),
                        s.IsCompleted,
                        s.DayOrder,
                        s.Title,
                        s.IsEasierAlternative)).ToList()
                    : (day.WorkoutDayId > 0
                        ? new List<ScheduleSessionChip>
                        {
                            new(
                                day.WorkoutDayId,
                                day.Tag,
                                WorkoutPresentationPalette.Background(day.Tag),
                                day.IsCompleted,
                                1,
                                day.Title,
                                day.IsEasierAlternative)
                        }
                        : new List<ScheduleSessionChip>
                        {
                            new(
                                0,
                                day.Tag,
                                WorkoutPresentationPalette.Background(day.Tag),
                                day.IsCompleted,
                                1,
                                day.Title,
                                false)
                        });

                return new ScheduleDayItem
                {
                    Date = day.Date.ToDateTime(TimeOnly.MinValue),
                    WeekIndex = day.WeekIndex,
                    DayLabel = day.DayLabel,
                    WorkoutTag = day.Tag,
                    WorkoutTitle = day.Title,
                    WorkoutDayId = day.WorkoutDayId,
                    CompanionWorkoutDayId = day.CompanionWorkoutDayId,
                    SessionWorkoutDayIds = day.Sessions?.Select(s => s.WorkoutDayId).ToList()
                        ?? (day.CompanionWorkoutDayId is { } companion ? [day.WorkoutDayId, companion] : [day.WorkoutDayId]),
                    Sessions = sessions,
                    HasReviewFlag = day.IsEasierAlternative || sessions.Any(s => s.HasReviewFlag),
                    IsToday = day.IsToday,
                    IsCompleted = day.IsCompleted,
                    IsRestDay = day.IsRestDay,
                    AccentColor = WorkoutPresentationPalette.Background(day.Tag),
                    AccentTextColor = WorkoutPresentationPalette.Text(day.Tag)
                };
            }).ToArray();
            // Publish the plan before any advisory call so a secondary failure cannot hide it.
            SelectWeek(weekToRestore);
            await LoadInjuryBannerAsync();
            WriteLoadStage("injury banner", loadStarted);
            WriteLoadStage("view state", loadStarted);
            // NotifyWorkoutStateProperties() is called by OnWorkoutsChanged (via ApplyFilter)
            // and by OnActivePlanChanged. No explicit call needed here.
        }
        finally
        {
            IsBusy = false;
            IsPlanStale = false;
        }
    }

    private static void WriteLoadStage(string stage, long started)
    {
        if (Environment.GetEnvironmentVariable("MUSCLECUTIES_PERF_CONSOLE") != "1")
            return;

        Console.WriteLine($"[Performance][WorkoutViewModel] {stage} reached at {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms.");
    }

    private void SelectFilter(FilterChipItem? item)
    {
        if (item is null) return;
        foreach (var f in Filters)
            f.IsSelected = false;
        item.IsSelected = true;
        ApplyFilter();
    }

    partial void OnIsDayScopeOnlyChanged(bool value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    public void ToggleDayScope()
    {
        IsDayScopeOnly = !IsDayScopeOnly;
    }

    [RelayCommand]
    public void SetDayScope(bool dayOnly)
    {
        IsDayScopeOnly = dayOnly;
    }

    private void ApplyFilter()
    {
        var selected = Filters.FirstOrDefault(f => f.IsSelected);
        var selectedWeekIds = GetSelectedWeekWorkoutIds();
        var hasWeekSchedule = _datedSchedule.Any(day => day.WeekIndex == SelectedWeekIndex);
        var weekWorkouts = hasWeekSchedule
            ? _allWorkouts.Where(workout => selectedWeekIds.Contains(workout.WorkoutDayId))
            : _allWorkouts.AsEnumerable();

        if (IsDayScopeOnly && SelectedScheduleDay is { } day)
        {
            var dayIds = day.SessionWorkoutDayIds.Count > 0
                ? day.SessionWorkoutDayIds
                : (day.CompanionWorkoutDayId is { } comp
                    ? new[] { day.WorkoutDayId, comp }
                    : [day.WorkoutDayId]);

            weekWorkouts = weekWorkouts.Where(w => dayIds.Contains(w.WorkoutDayId) || string.Equals(w.DayLabel, day.DayLabel, StringComparison.OrdinalIgnoreCase));
        }

        if (selected is null || selected.Label == "All")
            Workouts = new ObservableCollection<WorkoutItem>(weekWorkouts.Where(IsVisibleWorkoutItem));
        else if (selected.Label == "Recovery")
            Workouts = new ObservableCollection<WorkoutItem>(weekWorkouts.Where(IsRecoveryWorkoutItem));
        else
            Workouts = new ObservableCollection<WorkoutItem>(
                weekWorkouts.Where(w => !w.IsRestDay && string.Equals(
                    w.Tag,
                    selected.Label.ToUpperInvariant(),
                    StringComparison.OrdinalIgnoreCase)));
        // NotifyWorkoutStateProperties() is already called by OnWorkoutsChanged; do not call again.
    }

    private void SelectWeekChip(FilterChipItem? chip)
    {
        if (chip is null) return;
        SelectWeek(chip.Index);
    }

    private void SelectWeek(int weekIndex)
    {
        if (weekIndex < FirstWeekIndex || weekIndex > LastWeekIndex) return;
        SelectedWeekIndex = weekIndex;
        SyncWeekChips(weekIndex);
        ScheduleDays = new ObservableCollection<ScheduleDayItem>(
            _datedSchedule.Where(day => day.WeekIndex == weekIndex));
        UpdateSessionProgressForSelectedWeek();
        SelectScheduleDay(ScheduleDays.FirstOrDefault(day => day.IsToday) ?? ScheduleDays.FirstOrDefault());
        ApplyFilter();
    }

    private void UpdateSessionProgressForSelectedWeek()
    {
        var today = DateTime.Today.Date;
        foreach (var workout in _allWorkouts)
        {
            var scheduledDay = ScheduleDays.FirstOrDefault(day =>
                day.WorkoutDayId == workout.WorkoutDayId ||
                day.CompanionWorkoutDayId == workout.WorkoutDayId ||
                day.SessionWorkoutDayIds.Contains(workout.WorkoutDayId));
            if (scheduledDay is null)
                continue;

            workout.SessionProgressText = workout.IsCompleted
                ? "Workout_Overview_Status_Completed"
                : scheduledDay.Date.Date < today
                    ? "Workout_Overview_Status_NotLogged"
                    : scheduledDay.Date.Date == today
                        ? "Workout_Overview_Status_Today"
                        : "Workout_Overview_Status_Upcoming";
        }

        UpdateOverviewStatusTransitions();
    }

    private void UpdateOverviewStatusTransitions()
    {
        HasPlannedSessions = _datedSchedule.Any(day => !day.IsRestDay && day.WorkoutDayId > MissingWorkoutDayId)
            || _allWorkouts.Any(w => !w.IsRestDay);

        var todayLabel = FormatDayLabel(DateTime.Today.DayOfWeek);
        var todayWorkouts = _allWorkouts
            .Where(w => string.Equals(w.DayLabel, todayLabel, StringComparison.OrdinalIgnoreCase) && !w.IsRestDay)
            .ToList();

        var todayScheduledDay = _datedSchedule.FirstOrDefault(d => d.IsToday);

        HasCompletedTodaySession = (todayWorkouts.Count > 0 && todayWorkouts.Any(w => w.IsCompleted))
            || (todayScheduledDay is { IsCompleted: true })
            || (todayScheduledDay?.Sessions.Any(s => s.IsCompleted) == true);

        HasPendingReviewFlags = HasActiveInjuries
            || _allWorkouts.Any(w => w.IsEasierAlternative)
            || _datedSchedule.Any(d => d.HasReviewFlag);
    }

    public void MarkSessionCompleted(int workoutDayId)
    {
        var workout = _allWorkouts.FirstOrDefault(w => w.WorkoutDayId == workoutDayId);
        if (workout is not null)
        {
            workout.IsCompleted = true;
            workout.SessionProgressText = "Workout_Overview_Status_Completed";
        }

        foreach (var day in _datedSchedule)
        {
            if (day.WorkoutDayId == workoutDayId || day.SessionWorkoutDayIds.Contains(workoutDayId))
            {
                day.Sessions = day.Sessions.Select(s => s.WorkoutDayId == workoutDayId
                    ? s with { IsCompleted = true }
                    : s).ToList();
                day.IsCompleted = day.Sessions.All(s => s.IsCompleted);
            }
        }

        UpdateSessionProgressForSelectedWeek();
        ApplyFeaturedWorkout(SelectedScheduleDay);
        NotifyWorkoutStateProperties();
    }

    private HashSet<int> GetSelectedWeekWorkoutIds()
    {
        return _datedSchedule
            .Where(day => day.WeekIndex == SelectedWeekIndex && day.WorkoutDayId > MissingWorkoutDayId)
            .SelectMany(day => day.SessionWorkoutDayIds.Count > 0
                ? day.SessionWorkoutDayIds
                : (day.CompanionWorkoutDayId is { } companionWorkoutDayId
                    ? new[] { day.WorkoutDayId, companionWorkoutDayId }
                    : [day.WorkoutDayId]))
            .ToHashSet();
    }

    public void SelectScheduleDay(ScheduleDayItem? day)
    {
        if (day is null) return;
        SelectedScheduleDay = day;
        foreach (var candidate in ScheduleDays)
            candidate.IsSelected = ReferenceEquals(candidate, day);
        ApplyFeaturedWorkout(day);
    }

    private void ApplyFeaturedWorkout(ScheduleDayItem? selectedDay = null)
    {
        var todayLabel = selectedDay?.DayLabel ?? FormatDayLabel(DateTime.Today.DayOfWeek);
        var todayWorkouts = _allWorkouts.Where(workout => workout.DayLabel == todayLabel).ToList();
        var featuredWorkout = selectedDay is { WorkoutDayId: > 0 }
            ? _allWorkouts.FirstOrDefault(workout => workout.WorkoutDayId == selectedDay.WorkoutDayId)
            : null;
        featuredWorkout ??= todayWorkouts.FirstOrDefault(workout => !workout.IsCompleted)
                            ?? todayWorkouts.FirstOrDefault()
                            ?? _allWorkouts.FirstOrDefault();

        if (selectedDay is { IsRestDay: true })
        {
            _featuredWorkout = null;
            FeaturedWorkoutIsRestDay = true;
            FeaturedWorkoutHasAction = false;
            FeaturedWorkoutIsEditAction = false;
            FeaturedWorkoutBadgeText = UiTextCatalog.Format(
                "Workout_Featured_RestBadgeFormat",
                UiTextCatalog.TranslateRuntimeText(selectedDay.DayLabel));
            FeaturedWorkoutTitle = selectedDay.WorkoutTitle;
            FeaturedWorkoutSubtitle = UiTextCatalog.Get("Dashboard_Workout_RecoverySubtitle");
            FeaturedWorkoutDurationText = UiTextCatalog.Get("Dashboard_Workout_RestDay");
            FeaturedWorkoutExercisesCount = "0";
            FeaturedWorkoutIntensity = UiTextCatalog.Get("Dashboard_Workout_IntensityNone");
            FeaturedWorkoutActionText = string.Empty;
            FeaturedWorkoutActivityBackground = WorkoutPresentationPalette.Background("REST");
            FeaturedWorkoutActivityTextColor = WorkoutPresentationPalette.Text("REST");
            FeaturedWorkoutActivitySections = [];
            FeaturedWorkoutExercisePreviews = [];
            FeaturedWorkoutHasMultipleActivities = false;
            return;
        }

        if (featuredWorkout is null)
        {
            _featuredWorkout = null;
            FeaturedWorkoutIsRestDay = true;
            FeaturedWorkoutHasAction = false;
            FeaturedWorkoutIsEditAction = false;
            FeaturedWorkoutBadgeText = UiTextCatalog.Get("Dashboard_Workout_TodayRecovery");
            FeaturedWorkoutTitle = HasNoPlan
                ? UiTextCatalog.Get("Dashboard_Prompt_NoActivePlan")
                : UiTextCatalog.Get("Workout_Page_PlaceholderTitle");
            FeaturedWorkoutSubtitle = HasNoPlan
                ? UiTextCatalog.Get("Dashboard_Prompt_NoPlanBody")
                : UiTextCatalog.Get("Dashboard_Workout_RecoverySubtitle");
            FeaturedWorkoutDurationText = UiTextCatalog.Get("Dashboard_Workout_RestDay");
            FeaturedWorkoutExercisesCount = "0";
            FeaturedWorkoutIntensity = UiTextCatalog.Get("Dashboard_Workout_IntensityLight");
            FeaturedWorkoutActionText = UiTextCatalog.Get("Dashboard_Workout_StartWorkout");
            FeaturedWorkoutActivityBackground =
                WorkoutPresentationPalette.Background("REST");
            FeaturedWorkoutActivityTextColor =
                WorkoutPresentationPalette.Text("REST");
            FeaturedWorkoutActivitySections = [];
            FeaturedWorkoutExercisePreviews = [];
            FeaturedWorkoutHasMultipleActivities = false;
            return;
        }

        var isFeaturedInProgress = HasInProgressWorkout &&
            featuredWorkout.WorkoutDayId == InProgressWorkout?.WorkoutDayId;

        _featuredWorkout = featuredWorkout;
        FeaturedWorkoutIsRestDay = featuredWorkout.IsRestDay;
        FeaturedWorkoutHasAction = !featuredWorkout.IsRestDay &&
            (!HasInProgressWorkout || featuredWorkout.IsCompleted || isFeaturedInProgress);
        FeaturedWorkoutIsEditAction = featuredWorkout.IsCompleted;
        FeaturedWorkoutActivityBackground = featuredWorkout.ActivityBackground;
        FeaturedWorkoutActivityTextColor = featuredWorkout.ActivityTextColor;
        var featuredDayLabel = selectedDay is { IsToday: false }
            ? UiTextCatalog.TranslateRuntimeText(selectedDay.DayLabel)
            : UiTextCatalog.Get("Common_Today");
        FeaturedWorkoutBadgeText = featuredWorkout.IsCompleted
            ? UiTextCatalog.Get("Workout_Featured_CompletedBadge")
            : UiTextCatalog.Format(
                "Workout_Featured_DayActivityFormat",
                featuredDayLabel,
                LocalizedActivityTag(featuredWorkout.Tag));
        FeaturedWorkoutTitle = featuredWorkout.Title;
        FeaturedWorkoutSubtitle = featuredWorkout.DetailsText;
        FeaturedWorkoutDurationText = featuredWorkout.Duration;
        FeaturedWorkoutExercisesCount = featuredWorkout.ExerciseCount.ToString(CultureInfo.InvariantCulture);
        FeaturedWorkoutIntensity = BuildFeaturedIntensity(featuredWorkout.Tag);
        FeaturedWorkoutActionText = featuredWorkout.IsCompleted
            ? UiTextCatalog.Get("Workout_Featured_EditWorkout")
            : featuredWorkout.IsRestDay
                ? UiTextCatalog.Get("Workout_Featured_LogRestDay")
                : isFeaturedInProgress
                    ? UiTextCatalog.Get("Workout_Overview_ResumeWorkout")
                    : UiTextCatalog.Get("Dashboard_Workout_StartWorkout");

        var sections = BuildFeaturedActivitySections(featuredWorkout);
        FeaturedWorkoutActivitySections = new ObservableCollection<WorkoutActivitySection>(sections);
        FeaturedWorkoutHasMultipleActivities = sections.Length > SingleActivityCount;
        FeaturedWorkoutExercisePreviews = new ObservableCollection<string>(
            _dayPreviews.FirstOrDefault(day => day.WorkoutDayId == featuredWorkout.WorkoutDayId)?.Names ?? []);
    }

    private async Task HandleEmptyWorkoutsActionAsync()
    {
        if (HasNoPlan)
        {
            await GeneratePlanAsync();
            return;
        }

        var allFilter = Filters.FirstOrDefault(f => f.Label == "All");
        if (allFilter is not null)
            SelectFilter(allFilter);
    }

    private async Task LoadStrengthStyleOfferAsync()
    {
        if (_styleOffers is null) return;
        try
        {
            _styleOffer = (await _styleOffers.GetCurrentAsync()).Offer;
            HasStrengthStyleOffer = _styleOffer is not null;
        }
        catch (Exception)
        {
            _styleOffer = null;
            HasStrengthStyleOffer = false;
        }
    }

    [RelayCommand]
    private async Task ExploreStrengthStyleAsync()
    {
        if (_styleOffer is null || _styleOffers is null || _chooseStyleAsync is null ||
            _openStylePreviewAsync is null)
            return;
        var offer = _styleOffer;
        StyleOfferMessage = null;
        try
        {
            var chosenStyle = await _chooseStyleAsync(offer);
            if (chosenStyle is null || !offer.AlternativeStyleIds.Contains((int)chosenStyle.Value))
                return;
            await _openStylePreviewAsync(chosenStyle.Value);
            var result = await _styleOffers.DismissAsync(offer.Id);
            if (result.Dismissed) { _styleOffer = null; HasStrengthStyleOffer = false; }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Workout] Strength style offer unavailable ({ex.GetType().Name}).");
            StyleOfferMessage = ApiFailurePresenter.FromException(ex, "Workout_StyleOffer_ActionFailed").MessageText;
        }
    }

    [RelayCommand]
    private async Task DismissStrengthStyleAsync()
    {
        if (_styleOffers is null || _styleOffer is null) return;
        StyleOfferMessage = null;
        try
        {
            var result = await _styleOffers.DismissAsync(_styleOffer.Id);
            if (result.Dismissed) { _styleOffer = null; HasStrengthStyleOffer = false; }
            else await LoadStrengthStyleOfferAsync();
        }
        catch (Exception ex)
        {
            StyleOfferMessage = ApiFailurePresenter.FromException(ex, "Workout_StyleOffer_ActionFailed").MessageText;
        }
    }
    private async Task GeneratePlanAsync()
    {
        await _navigateAsync(nameof(ProfileWorkoutPreferencesPage));
    }

    private bool CanUseEmptyWorkoutsAction()
    {
        return !IsBusy;
    }

    private WorkoutActivitySection[] BuildFeaturedActivitySections(WorkoutItem featuredWorkout)
    {
        var day = _dayPreviews.FirstOrDefault(d => d.WorkoutDayId == featuredWorkout.WorkoutDayId);
        if (day is null || featuredWorkout.IsRestDay)
            return [];

        return day.Activities.Select(activity => new WorkoutActivitySection(
            activity.Tag, activity.Title, activity.ExerciseCount,
            WorkoutPresentationPalette.Background(activity.Tag),
            WorkoutPresentationPalette.Text(activity.Tag))).ToArray();
    }

    private static string LocalizedActivityTag(string tag)
    {
        return tag switch
        {
            "CARDIO" => UiTextCatalog.Get("Workout_Featured_TagCardio"),
            "RECOVERY" => UiTextCatalog.Get("Workout_Featured_TagRecovery"),
            "REST" => UiTextCatalog.Get("Workout_Featured_TagRest"),
            "STRENGTH" => UiTextCatalog.Get("Workout_Featured_TagStrength"),
            _ => tag.ToLowerInvariant()
        };
    }

    private async Task OpenFeaturedWorkoutAsync()
    {
        if (HasInProgressWorkout && _featuredWorkout?.WorkoutDayId == InProgressWorkout?.WorkoutDayId)
        {
            await ResumeInProgressWorkoutAsync();
            return;
        }

        if (_featuredWorkout is not null)
            await OpenWorkoutAsync(_featuredWorkout);
    }

    private async Task ResumeInProgressWorkoutAsync()
    {
        if (InProgressWorkout is not { IsInProgress: true, WorkoutDayId: > MissingWorkoutDayId } workout)
            return;
        if (SessionActivitiesRequested is { } handler)
            await handler(workout.WorkoutDayId);
        else
            await _openWorkoutSessionAsync(workout.WorkoutDayId);
    }

    private async Task OpenWorkoutAsync(WorkoutItem? workout)
    {
        if (workout is null || workout.WorkoutDayId <= 0)
            return;

        if (SessionActivitiesRequested is { } handler)
            await handler(workout.WorkoutDayId);
        else
            await _openWorkoutSessionAsync(workout.WorkoutDayId);
    }

    private async Task ToggleSessionOptionalAsync(WorkoutItem? workout)
    {
        if (workout is null || workout.WorkoutDayId <= 0 || workout.IsCompleted || workout.IsRestDay) return;
        var wasOptional = workout.IsOptional;
        var wasEasierAlternative = workout.IsEasierAlternative;
        workout.IsOptional = !wasOptional;
        if (wasOptional) workout.IsEasierAlternative = false;
        OnPropertyChanged(nameof(Workouts));
        try
        {
            await _overview.SetSessionOptionalAsync(new SetWorkoutSessionOptionalRequest(
                workout.WorkoutDayId, workout.IsOptional, workout.IsEasierAlternative));
        }
        catch
        {
            workout.IsOptional = wasOptional;
            workout.IsEasierAlternative = wasEasierAlternative;
            OnPropertyChanged(nameof(Workouts));
            throw;
        }
    }

    private async Task ToggleEasierAlternativeAsync(WorkoutItem? workout)
    {
        if (workout is null || !workout.CanMarkEasierAlternative) return;
        var previous = workout.IsEasierAlternative;
        workout.IsEasierAlternative = !previous;
        try
        {
            await _overview.SetSessionOptionalAsync(new SetWorkoutSessionOptionalRequest(
                workout.WorkoutDayId, IsOptional: true, IsEasierAlternative: workout.IsEasierAlternative));
        }
        catch
        {
            workout.IsEasierAlternative = previous;
            throw;
        }
    }

    public void StartReschedule(WorkoutItem? workout)
    {
        // Beta 1.0.0 gates rescheduling to 1.1: the entry point stays closed even if a caller
        // reaches the command while the presentation hides it.
        if (!MuscleCuties.Core.Domain.Workout.BetaWorkoutScope.ReschedulingEnabled) return;
        if (workout is null || workout.WorkoutDayId <= 0) return;

        RescheduleStatusMessage = string.Empty;
        ReschedulingWorkout = workout;
        ReschedulingWorkoutTitle = workout.Title;

        if (workout.IsCompleted)
        {
            RescheduleStatusMessage = UiTextCatalog.Get("Workout_Reschedule_Error_Completed");
            AvailableRescheduleDates = [];
            IsRescheduleModalVisible = true;
            return;
        }

        if (InProgressWorkout is { IsInProgress: true } inProgress && inProgress.WorkoutDayId == workout.WorkoutDayId)
        {
            RescheduleStatusMessage = UiTextCatalog.Get("Workout_Reschedule_Error_InProgress");
            AvailableRescheduleDates = [];
            IsRescheduleModalVisible = true;
            return;
        }

        var activeWeekSchedule = _datedSchedule.Where(day => day.WeekIndex == SelectedWeekIndex).ToList();
        var emptyDays = activeWeekSchedule
            .Where(day => day.IsRestDay && (day.Sessions == null || day.Sessions.All(s => s.WorkoutDayId <= 0)))
            .Select(day => new RescheduleDateOption(
                DateOnly.FromDateTime(day.Date),
                day.DayLabel,
                day.Date.ToString("dd MMM", CultureInfo.InvariantCulture)))
            .ToList();

        AvailableRescheduleDates = new ObservableCollection<RescheduleDateOption>(emptyDays);
        if (emptyDays.Count == 0)
        {
            RescheduleStatusMessage = UiTextCatalog.Get("Workout_Reschedule_NoEmptyDates");
            SelectedRescheduleDate = null;
        }
        else
        {
            SelectRescheduleDate(emptyDays[0]);
        }

        IsRescheduleModalVisible = true;
    }

    public void SelectRescheduleDate(RescheduleDateOption? option)
    {
        if (option is null) return;
        foreach (var opt in AvailableRescheduleDates)
            opt.IsSelected = opt == option;
        SelectedRescheduleDate = option;
        ConfirmRescheduleCommand.NotifyCanExecuteChanged();
    }

    public void CloseRescheduleModal()
    {
        IsRescheduleModalVisible = false;
        ReschedulingWorkout = null;
        SelectedRescheduleDate = null;
        RescheduleStatusMessage = string.Empty;
        ConfirmRescheduleCommand.NotifyCanExecuteChanged();
    }

    public async Task ConfirmRescheduleAsync()
    {
        if (!MuscleCuties.Core.Domain.Workout.BetaWorkoutScope.ReschedulingEnabled)
        {
            CloseRescheduleModal();
            return;
        }

        if (ReschedulingWorkout is null || SelectedRescheduleDate is null || IsRescheduleBusy)
            return;

        // Optimistic concurrency verification:
        // 1. Check if the session is currently in progress
        if (InProgressWorkout is { IsInProgress: true } inProgress &&
            inProgress.WorkoutDayId == ReschedulingWorkout.WorkoutDayId)
        {
            RescheduleStatusMessage = UiTextCatalog.Get("Workout_Reschedule_Error_InProgress");
            return;
        }

        // 2. Check if destination date is already occupied
        var targetDate = SelectedRescheduleDate.Date;
        var occupied = _datedSchedule.Any(day =>
            DateOnly.FromDateTime(day.Date) == targetDate && !day.IsRestDay && day.WorkoutDayId > 0);
        if (occupied)
        {
            RescheduleStatusMessage = UiTextCatalog.Get("Workout_Reschedule_Error_Occupied");
            return;
        }

        IsRescheduleBusy = true;
        ConfirmRescheduleCommand.NotifyCanExecuteChanged();
        try
        {
            await _overview.RescheduleAsync(new RescheduleWorkoutDayRequest(
                ReschedulingWorkout.WorkoutDayId,
                targetDate));

            IsRescheduleModalVisible = false;
            ReschedulingWorkout = null;
            SelectedRescheduleDate = null;
            RescheduleStatusMessage = string.Empty;

            await _loadGate.RunAsync(LoadDataCoreAsync, this, true);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Workout] Reschedule failed: {ex.Message}");
            RescheduleStatusMessage = ApiFailurePresenter.FromException(ex, "Workout_Reschedule_Failed").MessageText;
        }
        finally
        {
            IsRescheduleBusy = false;
            ConfirmRescheduleCommand.NotifyCanExecuteChanged();
        }
    }

    private static bool IsVisibleWorkoutItem(WorkoutItem workout)
    {
        return !workout.IsRestDay;
    }

    private static bool IsRecoveryWorkoutItem(WorkoutItem workout)
    {
        return !workout.IsRestDay && workout.Tag.Contains("RECOVERY", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildFeaturedIntensity(string tag)
    {
        if (tag.Contains("RECOVERY", StringComparison.OrdinalIgnoreCase))
            return UiTextCatalog.Get("Dashboard_Workout_IntensityLight");

        if (tag.Contains("REST", StringComparison.OrdinalIgnoreCase))
            return UiTextCatalog.Get("Dashboard_Workout_IntensityNone");

        if (tag.Contains("CARDIO", StringComparison.OrdinalIgnoreCase))
            return UiTextCatalog.Get("Dashboard_Workout_IntensitySteady");

        // The tag carries the activity type, not the planned load. "Heavy" was a false claim
        // on general-strength and beginner plans, so strength work reports a neutral effort.
        return UiTextCatalog.Get("Dashboard_Workout_IntensityModerate");
    }

    private static string FormatDayLabel(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Sunday => "SUN",
            DayOfWeek.Monday => "MON",
            DayOfWeek.Tuesday => "TUE",
            DayOfWeek.Wednesday => "WED",
            DayOfWeek.Thursday => "THU",
            DayOfWeek.Friday => "FRI",
            DayOfWeek.Saturday => "SAT",
            _ => string.Empty
        };
    }

    private void NotifyWorkoutStateProperties()
    {
        UpdateOverviewStatusTransitions();
        OnPropertyChanged(nameof(HasNoPlan));
        OnPropertyChanged(nameof(ShowNoPlanPrompt));
        OnPropertyChanged(nameof(HasWorkouts));
        OnPropertyChanged(nameof(HasNoWorkouts));
        OnPropertyChanged(nameof(ActivePlanTitle));
        OnPropertyChanged(nameof(PlanSummaryText));
        OnPropertyChanged(nameof(EmptyWorkoutsTitle));
        OnPropertyChanged(nameof(EmptyWorkoutsSubtitle));
        OnPropertyChanged(nameof(EmptyWorkoutsButtonText));
        OnPropertyChanged(nameof(EmptyWorkoutsButtonVisible));
        EmptyWorkoutsActionCommand.NotifyCanExecuteChanged();
        GeneratePlanCommand.NotifyCanExecuteChanged();
    }

    partial void OnActivePlanNameChanged(string? value)
    {
        NotifyWorkoutStateProperties();
    }

    partial void OnWorkoutsChanged(ObservableCollection<WorkoutItem> value)
    {
        NotifyWorkoutStateProperties();
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyWorkoutStateProperties();
    }

    partial void OnIsLoadErrorChanged(bool value)
    {
        NotifyWorkoutStateProperties();
    }

    partial void OnHasActiveInjuriesChanged(bool value)
    {
        UpdateOverviewStatusTransitions();
    }

    private async Task LoadInjuryBannerAsync()
    {
        // The safety banner is advisory. A failure here must never hide an otherwise usable plan.
        try
        {
            var impact = await _injuries.GetImpactAsync();
            HasActiveInjuries = impact.HasActiveInjuries;
            InjuryBannerSummary = impact.Summary;
            BlockedExerciseNames = new ObservableCollection<string>(impact.BlockedExercises);
            RehabExerciseNames = new ObservableCollection<string>(impact.RehabExercises);
            BlockedCardioNames = new ObservableCollection<string>(impact.BlockedActivities);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Workout] Injury banner unavailable ({ex.GetType().Name}).");
            HasActiveInjuries = false;
            InjuryBannerSummary = string.Empty;
            BlockedExerciseNames = new ObservableCollection<string>();
            RehabExerciseNames = new ObservableCollection<string>();
            BlockedCardioNames = new ObservableCollection<string>();
        }

        NotifyInjuryBannerProperties();
    }

    private void NotifyInjuryBannerProperties()
    {
        OnPropertyChanged(nameof(HasBlockedExercises));
        OnPropertyChanged(nameof(HasRehabExercises));
        OnPropertyChanged(nameof(HasBlockedCardio));
    }
}

