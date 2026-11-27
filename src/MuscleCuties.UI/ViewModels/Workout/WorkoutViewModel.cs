using System.Diagnostics.CodeAnalysis;
using Microsoft.Maui.Graphics;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Pages.Profile;
using MuscleCuties.UI.Pages.Workout;
using MuscleCuties.UI.Services.Api;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.Workout.Overview;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

/// <summary>The train tab: the active plan's week, the schedule strip, the featured workout and the filters over</summary>
/// them, plus the injury banner that qualifies what the plan will ask for.</summary>
public partial class WorkoutViewModel : ObservableObject, IPageLoadAware, IDisposable
{
    private const int FirstWeekIndex = 0;
    private const int LastWeekIndex = 3;
    private const int MissingWorkoutDayId = 0;
    private const int SingleActivityCount = 1;

    private readonly IWorkoutInjuryUseCase _injuries;
    private readonly IStrengthStyleOfferClient? _styleOffers;
    private readonly Func<StrengthStyleOfferDto, Task<WorkoutActivityDto?>>? _chooseStyleAsync;
    private readonly Func<WorkoutActivityDto, Task>? _openStylePreviewAsync;
    private StrengthStyleOfferDto? _styleOffer;
    [ObservableProperty] private bool _hasStrengthStyleOffer;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStyleOfferMessage))]
    private string? _styleOfferMessage;
    /// <summary>True when a style offer message is shown.</summary>
    public bool HasStyleOfferMessage => !string.IsNullOrWhiteSpace(StyleOfferMessage);
    private readonly AppPageLoadGate _loadGate = new(AppPageLoadGate.PageFreshnessWindow);
    private readonly Func<string, Task> _navigateAsync;
    private readonly Func<int, Task> _openWorkoutSessionAsync;
    /// <summary>Raised when a workout with several activities must choose one before starting; the page shows the</summary>
    /// picker and starts the chosen session.</summary>
    public event Func<int, Task>? SessionActivitiesRequested;
    private readonly IWorkoutOverviewUseCase _overview;

    [ObservableProperty] private string? _activePlanName;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInProgressWorkout))]
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
    [ObservableProperty]
    private string _featuredWorkoutActionText =
        UiTextCatalog.Get("Dashboard_Workout_StartWorkout");
    [ObservableProperty] private bool _featuredWorkoutHasAction;
    [ObservableProperty] private bool _featuredWorkoutIsEditAction;
    [ObservableProperty] private bool _featuredWorkoutIsRestDay = true;

    [ObservableProperty]
    private Color _featuredWorkoutActivityBackground =
        WorkoutPresentationPalette.Background("STRENGTH");

    [ObservableProperty] private ObservableCollection<WorkoutActivitySection> _featuredWorkoutActivitySections = [];

    [ObservableProperty]
    private Color _featuredWorkoutActivityTextColor =
        WorkoutPresentationPalette.Text("STRENGTH");

    [ObservableProperty]
    private string _featuredWorkoutBadgeText =
        UiTextCatalog.Get("Dashboard_Workout_TodayRecovery");
    [ObservableProperty]
    private string _featuredWorkoutDurationText =
        UiTextCatalog.Get("Dashboard_Workout_Flexible");
    [ObservableProperty] private ObservableCollection<string> _featuredWorkoutExercisePreviews = [];
    [ObservableProperty] private string _featuredWorkoutExercisesCount = "0";
    [ObservableProperty] private bool _featuredWorkoutHasMultipleActivities;
    [ObservableProperty]
    private string _featuredWorkoutIntensity =
        UiTextCatalog.Get("Dashboard_Workout_IntensityLight");
    [ObservableProperty]
    private string _featuredWorkoutSubtitle =
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;

    [ObservableProperty] private bool _isInjuryBannerExpanded;

    [ObservableProperty] private bool _isInjuryModalVisible;
    [ObservableProperty] private bool _isSessionActivitiesVisible;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isPlanStale = true;
    [ObservableProperty] private ObservableCollection<string> _rehabExerciseNames = new();
    [ObservableProperty] private ObservableCollection<ScheduleDayItem> _scheduleDays = [];
    [ObservableProperty] private ScheduleDayItem? _selectedScheduleDay;
    [ObservableProperty] private int _selectedWeekIndex;
    [ObservableProperty] private bool _hasPlannedSessions;
    [ObservableProperty] private bool _hasCompletedTodaySession;
    [ObservableProperty] private bool _hasPendingReviewFlags;

    [ObservableProperty] private string _weekTitle = "This week's plan";
    [ObservableProperty] private ObservableCollection<WorkoutItem> _workouts = new();

    [ObservableProperty] private bool _isRescheduleModalVisible;
    [ObservableProperty] private bool _isRescheduleBusy;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRescheduleStatusMessage))]
    private string _rescheduleStatusMessage = string.Empty;
    /// <summary>True when a reschedule status is shown.</summary>
    public bool HasRescheduleStatusMessage => !string.IsNullOrWhiteSpace(RescheduleStatusMessage);
    [ObservableProperty] private WorkoutItem? _reschedulingWorkout;
    [ObservableProperty] private string _reschedulingWorkoutTitle = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvailableRescheduleDates), nameof(CanConfirmReschedule))]
    private ObservableCollection<RescheduleDateOption> _availableRescheduleDates = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirmReschedule))]
    private RescheduleDateOption? _selectedRescheduleDate;
    /// <summary>True when there are dates to reschedule to.</summary>
    public bool HasAvailableRescheduleDates => AvailableRescheduleDates.Count > 0;
    /// <summary>True when the reschedule can be confirmed.</summary>
    public bool CanConfirmReschedule => SelectedRescheduleDate is not null && !IsRescheduleBusy;

    /// <summary>Takes the use cases and the navigation callbacks.</summary>
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
            new() { Column = 0, Tag = WorkoutListFilterRule.AllTag, Label = UiTextCatalog.Get("Workout_Filter_All"), IsSelected = true },
            new() { Column = 1, Tag = WorkoutListFilterRule.StrengthTag, Label = UiTextCatalog.Get("Workout_Filter_Strength") },
            new() { Column = 2, Tag = WorkoutListFilterRule.CardioTag, Label = UiTextCatalog.Get("Workout_Filter_Cardio") },
            new() { Column = 3, Tag = WorkoutListFilterRule.RecoveryTag, Label = UiTextCatalog.Get("Workout_Filter_Recovery") }
        };
        Weeks = new ObservableCollection<FilterChipItem>
        {
            new() { Column = 0, Index = FirstWeekIndex, Label = UiTextCatalog.Get("Workout_Page_Week1"), IsSelected = true },
            new() { Column = 1, Index = FirstWeekIndex + 1, Label = UiTextCatalog.Get("Workout_Page_Week2") },
            new() { Column = 2, Index = FirstWeekIndex + 2, Label = UiTextCatalog.Get("Workout_Page_Week3") },
            new() { Column = 3, Index = FirstWeekIndex + 3, Label = UiTextCatalog.Get("Workout_Page_Week4") }
        };
        var initialWeekStart = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        _datedSchedule = Enumerable.Range(0, 7).Select(offset =>
        {
            var date = initialWeekStart.AddDays(offset);
            var isToday = date.Date == DateTime.Today.Date;
            return new ScheduleDayItem
            {
                Date = date,
                WeekIndex = FirstWeekIndex,
                DayLabel = FormatDayLabel(date.DayOfWeek),
                WorkoutTag = "REST",
                WorkoutTitle = string.Empty,
                Sessions = [new ScheduleSessionChip(0, "REST", WorkoutPresentationPalette.Background("REST"), false, 1, string.Empty, false)],
                IsToday = isToday,
                IsRestDay = true,
                IsSelected = isToday,
                AccentColor = WorkoutPresentationPalette.Background("REST"),
                AccentTextColor = WorkoutPresentationPalette.Text("REST")
            };
        }).ToArray();
        ScheduleDays = new ObservableCollection<ScheduleDayItem>(_datedSchedule);
    }

    /// <summary>Marks the chip for the selected week, so the strip and the schedule cannot disagree.</summary>
    private void SyncWeekChips(int weekIndex)
    {
        foreach (var week in Weeks)
            week.IsSelected = week.Index == weekIndex;
    }

    /// <summary>True when injuries block exercises.</summary>
    public bool HasBlockedExercises => BlockedExerciseNames.Count > 0;
    /// <summary>True when rehab exercises are listed.</summary>
    public bool HasRehabExercises => RehabExerciseNames.Count > 0;
    /// <summary>True when injuries block cardio.</summary>
    public bool HasBlockedCardio => BlockedCardioNames.Count > 0;
    /// <summary>False while the phase is unknown, which the card shows as no phase rather than as an empty label.</summary>
    public bool HasKnownCyclePhase => !string.IsNullOrWhiteSpace(CurrentPhaseName) &&
                                      !CurrentPhaseName.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

    /// <summary>Refreshes the phase guidance when the name changes.</summary>
    partial void OnCurrentPhaseNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasKnownCyclePhase));
    }

    /// <summary>True when the tab bar is shown.</summary>
    public bool IsTabBarVisible => !IsInjuryModalVisible && !IsSessionActivitiesVisible && !IsRescheduleModalVisible;

    /// <summary>Refreshes the tab bar when the injury modal opens.</summary>
    partial void OnIsInjuryModalVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(IsTabBarVisible));

    /// <summary>Refreshes the tab bar when the activity list opens.</summary>
    partial void OnIsSessionActivitiesVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(IsTabBarVisible));

    /// <summary>Refreshes the tab bar when the reschedule modal opens.</summary>
    partial void OnIsRescheduleModalVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(IsTabBarVisible));

    /// <summary>Refreshes the resume state when the in-progress workout changes.</summary>
    partial void OnInProgressWorkoutChanged(WorkoutListEntry? value) =>
        ApplyFeaturedWorkout(SelectedScheduleDay);

    /// <summary>The injury log this page shares, created once with a callback that refreshes the plan after an injury</summary>
    /// changes.</summary>
    public InjuryLogViewModel InjuryLogVm => _injuryLogVm ??= new InjuryLogViewModel(
        _injuries, () => Task.CompletedTask, RefreshAfterInjuryChangeAsync);

    /// <summary>True when no plan is active.</summary>
    public bool HasNoPlan => string.IsNullOrEmpty(ActivePlanName);
    /// <summary>True when a workout is in progress.</summary>
    public bool HasInProgressWorkout => InProgressWorkout is { IsInProgress: true, WorkoutDayId: > MissingWorkoutDayId };
    /// <summary>True when the empty plan prompt is shown.</summary>
    public bool ShowNoPlanPrompt => _hasLoadedOverview && HasNoPlan && HasNoWorkouts;
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>True when the schedule has days.</summary>
    public bool HasScheduleDays => _hasLoadedOverview && !IsLoadError && ScheduleDays.Count > 0;
    /// <summary>
    /// A plan always offers its weeks. The chip row must not follow the selected week's days: a
    /// week without dated days would hide the only control that leads back to a week with sessions.
    /// </summary>
    public bool HasPlanWeeks => _hasLoadedOverview && !IsBusy && !HasNoPlan;
    /// <summary>True when the plan has no scheduled days.</summary>
    public bool HasNoScheduleDays => _hasLoadedOverview && !IsBusy && !HasNoPlan && !HasScheduleDays;
    /// <summary>True when the week has workouts.</summary>
    public bool HasWorkouts => !IsBusy && Workouts.Count > 0;
    /// <summary>True when the week has no workouts.</summary>
    public bool HasNoWorkouts => !IsBusy && !IsLoadError && Workouts.Count == 0;
    /// <summary>
    /// The empty card belongs to the page flow, not to the collection's EmptyView, which is not
    /// drawn inside this header-bearing list and left a blank page under the chips.
    /// </summary>
    public bool ShowEmptyWorkouts => _hasLoadedOverview && HasNoWorkouts && !ShowNoPlanPrompt;
    /// <summary>The plan's name, or the no-plan wording when the user has none yet.</summary>
    public string ActivePlanTitle => ActivePlanName ?? "No active plan";

    /// <summary>The selected week in one line, falling back to the empty-week wording before anything is loaded.</summary>
    public string PlanSummaryText => _lastOverview is null
        ? UiTextCatalog.Get("Workout_Page_PlanSummaryNoSessions")
        : FormatPlanSummary(_lastOverview, SelectedWeekIndex);

    /// <summary>
    /// The overview's week in one localized line. The infrastructure formatter builds English
    /// grammar, which the catalog can only partially replace; counting here keeps every part of the
    /// sentence in the user's language and gives the parts one separator.
    /// </summary>
    private static string FormatPlanSummary(WorkoutOverviewResponse overview, int weekIndex)
    {
        var weekSchedule = overview.Schedule.Where(day => day.WeekIndex == weekIndex).ToArray();
        var scheduleIds = weekSchedule.Where(day => !day.IsRestDay)
            .Select(day => day.WorkoutDayId)
            .ToHashSet();
        var weekWorkouts = weekSchedule.Length > 0
            ? overview.Workouts.Where(workout => scheduleIds.Contains(workout.WorkoutDayId)).ToArray()
            : overview.Workouts.ToArray();

        var trainingDays = weekSchedule.Length > 0
            ? weekSchedule.Count(day => !day.IsRestDay)
            : weekWorkouts.Where(workout => !workout.IsRestDay)
                .Select(workout => workout.DayLabel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        if (trainingDays == 0)
            return UiTextCatalog.Get("Workout_Page_PlanSummaryNoSessions");

        var restDays = weekSchedule.Length > 0
            ? weekSchedule.Count(day => day.IsRestDay)
            : weekWorkouts.Where(workout => workout.IsRestDay)
                .Select(workout => workout.DayLabel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        var activities = weekWorkouts.Where(workout => !workout.IsRestDay)
            .Sum(workout => workout.ActivityCount);

        var parts = new List<string>
        {
            UiTextCatalog.Format("Workout_Page_PlanSummaryTrainingDays", trainingDays),
            UiTextCatalog.Format("Workout_Page_PlanSummaryRestDays", restDays)
        };
        if (activities > trainingDays)
            parts.Add(UiTextCatalog.Format("Workout_Page_PlanSummaryActivities", activities));

        return string.Join(" · ", parts);
    }

    /// <summary>Heading for the empty list, which differs between having no plan at all and having filtered everything</summary>
    /// out of a plan that exists.</summary>
    public string EmptyWorkoutsTitle => HasNoPlan
        ? "Workout_Generation_PromptNoPlanTitle"
        : "Workout_Page_EmptyFilterTitle";

    /// <summary>The matching body for those two cases.</summary>
    public string EmptyWorkoutsSubtitle => HasNoPlan
        ? "Workout_Generation_PromptNoPlanSubtitle"
        : "Workout_Page_EmptyFilterSubtitle";

    /// <summary>The empty list's button: create a plan, or clear the filter.</summary>
    public string EmptyWorkoutsButtonText => HasNoPlan
        ? "Workout_Generation_PromptGeneratePlanButton"
        : "Workout_Page_EmptyFilterShowAll";
    /// <summary>Always true, as the empty state always offers the button.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound in workout page XAML.")]
    public bool EmptyWorkoutsButtonVisible => true;

    /// <summary>Runs the empty state's action.</summary>
    public AsyncRelayCommand EmptyWorkoutsActionCommand { get; }
    /// <summary>Opens the plan generation.</summary>
    public AsyncRelayCommand GeneratePlanCommand { get; }
    /// <summary>Opens the workout history.</summary>
    public AsyncRelayCommand OpenHistoryCommand { get; }
    /// <summary>Resumes the workout in progress.</summary>
    public AsyncRelayCommand ResumeInProgressWorkoutCommand { get; }
    /// <summary>Opens a workout day.</summary>
    public AsyncRelayCommand<WorkoutItem> OpenWorkoutCommand { get; }
    /// <summary>Marks a session optional or required.</summary>
    public AsyncRelayCommand<WorkoutItem> ToggleSessionOptionalCommand { get; }
    /// <summary>Marks a session as an easier alternative.</summary>
    public AsyncRelayCommand<WorkoutItem> ToggleEasierAlternativeCommand { get; }
    /// <summary>Selects a filter chip.</summary>
    public RelayCommand<FilterChipItem> SelectFilterCommand { get; }
    /// <summary>Starts the featured workout.</summary>
    public AsyncRelayCommand StartFeaturedWorkoutCommand { get; }
    /// <summary>Selects a week.</summary>
    public RelayCommand<int> SelectWeekCommand { get; }
    /// <summary>Selects a week chip.</summary>
    public RelayCommand<FilterChipItem> SelectWeekChipCommand { get; }
    /// <summary>Selects a schedule day.</summary>
    public RelayCommand<ScheduleDayItem> SelectScheduleDayCommand { get; }
    /// <summary>Shows or hides the injury banner.</summary>
    public RelayCommand ToggleInjuryBannerCommand { get; }
    /// <summary>Opens the injury log.</summary>
    public AsyncRelayCommand NavigateToInjuryLogCommand { get; }
    /// <summary>Starts rescheduling a workout.</summary>
    public RelayCommand<WorkoutItem> StartRescheduleCommand { get; }
    /// <summary>Selects the date to reschedule to.</summary>
    public RelayCommand<RescheduleDateOption> SelectRescheduleDateCommand { get; }
    /// <summary>Confirms the reschedule.</summary>
    public AsyncRelayCommand ConfirmRescheduleCommand { get; }
    /// <summary>Closes the reschedule modal.</summary>
    public RelayCommand CloseRescheduleModalCommand { get; }

    /// <summary>Releases the view model's subscriptions.</summary>
    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Loads the overview.</summary>
    public AsyncRelayCommand LoadDataCommand { get; }
    /// <summary>Reloads the overview.</summary>
    public AsyncRelayCommand RefreshCommand { get; }
    // The spinner shows only while a load is actually running. A stale plan is refreshed on the next appearance;
    // showing a spinner for staleness alone could stay on screen when no reload starts (F04).
    /// <summary>True while the page loads.</summary>
    public bool IsPageLoading => IsBusy;

    /// <summary>Opens the injury modal.</summary>
    [RelayCommand]
    private async Task OpenInjuryModalAsync()
    {
        await InjuryLogVm.OpenModalAsync(() => IsInjuryModalVisible = true);
    }

    /// <summary>Closes the injury modal.</summary>
    [RelayCommand]
    private void CloseInjuryModal()
    {
        _injuryLogVm?.ResetForm();
        IsInjuryModalVisible = false;
    }

    /// <summary>Refreshes the overview after an injury change.</summary>
    private Task RefreshAfterInjuryChangeAsync() =>
        _loadGate.RefreshAfterInjuryChangeAsync(
            InjuryLogVm.HasActiveInjuries,
            hasActiveInjuries => HasActiveInjuries = hasActiveInjuries,
            LoadDataCoreAsync,
            this);

    /// <summary>Pull-to-refresh, ignored while the page is already loading so the refresh spinner cannot spin forever.</summary>
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

    /// <summary>Marks the data stale so the next appearance reloads it.</summary>
    public void Invalidate()
    {
        _loadGate.MarkStale();
    }

    /// <summary>The load: overview, injury impact and style offer, restoring the week the user was looking at.</summary>
    private async Task LoadDataCoreAsync()
    {
        var weekToRestore = SelectedWeekIndex;
        IsBusy = true;
        IsLoadError = false;
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
                        WorkoutPresentationPalette.Text(s.Tag),
                        s.IsCompleted,
                        s.DayOrder,
                        s.Title,
                        s.IsEasierAlternative)).ToList()
                    : (!day.IsRestDay && day.WorkoutDayId > 0
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
                        : []);

                return new ScheduleDayItem
                {
                    Date = day.Date.ToDateTime(TimeOnly.MinValue),
                    WeekIndex = day.WeekIndex,
                    DayLabel = day.DayLabel,
                    WorkoutTag = day.Tag,
                    WorkoutTitle = day.Title,
                    AccessibilityDescription = BuildScheduleDayAccessibility(day),
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
        catch (Exception ex)
        {
            // A failed overview must not masquerade as an empty week. Keep the old plan on screen
            // and offer a retry instead of presenting "no workouts scheduled" as a real state.
            IsLoadError = true;
            Trace.WriteLine($"[Workout] Overview load failed ({ex.GetType().Name}).");
        }
        finally
        {
            IsBusy = false;
            IsPlanStale = false;
        }
    }

    /// <summary>Writes one load stage's elapsed time when performance logging is on.</summary>
    private static void WriteLoadStage(string stage, long started)
    {
        if (Environment.GetEnvironmentVariable("MUSCLECUTIES_PERF_CONSOLE") != "1")
            return;

        Console.WriteLine($"[Performance][WorkoutViewModel] {stage} reached at {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms.");
    }

    /// <summary>Makes the tapped chip the only selected one and reapplies the filter.</summary>
    private void SelectFilter(FilterChipItem? item)
    {
        if (item is null) return;
        foreach (var f in Filters)
            f.IsSelected = false;
        item.IsSelected = true;
        ApplyFilter();
    }

    /// <summary>Rebuilds the visible workouts from the selected week and tag, so the list always reflects both.</summary>
    private void ApplyFilter()
    {
        var selectedFilterTag = Filters.FirstOrDefault(f => f.IsSelected)?.Tag;
        var selectedWeekIds = GetSelectedWeekWorkoutIds();
        var hasWeekSchedule = _datedSchedule.Any(day => day.WeekIndex == SelectedWeekIndex);
        var weekWorkouts = hasWeekSchedule
            ? _allWorkouts.Where(workout => selectedWeekIds.Contains(workout.WorkoutDayId))
            : _allWorkouts.AsEnumerable();

        Workouts = new ObservableCollection<WorkoutItem>(weekWorkouts.Where(workout =>
            WorkoutListFilterRule.IsVisible(workout.IsRestDay, workout.Tag, selectedFilterTag)));
        // NotifyWorkoutStateProperties() is already called by OnWorkoutsChanged; do not call again.
    }

    /// <summary>Switches week from the strip.</summary>
    private void SelectWeekChip(FilterChipItem? chip)
    {
        if (chip is null) return;
        SelectWeek(chip.Index);
    }

    /// <summary>Switches to a week inside the plan's four, re-chips the strip, rebuilds the schedule and keeps the day</summary>
    /// selection on today when that week contains it.</summary>
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

    /// <summary>Re-reads each session's completion from the schedule for the selected week, so a finished session stops</summary>
    /// looking outstanding after the week changes.</summary>
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

    /// <summary>Recomputes the overview's derived flags — planned sessions, pending review, whether today has training —</summary>
    /// after the schedule or the injury state changed.</summary>
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

    /// <summary>Marks a session done locally after the session page reports it, and updates everything derived from it</summary>
    /// without a reload.</summary>
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

    /// <summary>The workout ids in the selected week, including the companion session of a two-a-day, so filtering by week</summary>
    /// keeps both halves.</summary>
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

    /// <summary>Selects one day in the strip and features what that day trains.</summary>
    public void SelectScheduleDay(ScheduleDayItem? day)
    {
        if (day is null) return;
        SelectedScheduleDay = day;
        foreach (var candidate in ScheduleDays)
            candidate.IsSelected = ReferenceEquals(candidate, day);
        ApplyFeaturedWorkout(day);
    }

    /// <summary>Builds the featured card for the selected day, falling back to today's training and then to the week's</summary>
    /// first session when the day has none.</summary>
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

    /// <summary>The empty list's action: send the user to create a plan when there is none, otherwise clear the filter.</summary>
    private async Task HandleEmptyWorkoutsActionAsync()
    {
        if (HasNoPlan)
        {
            await GeneratePlanAsync();
            return;
        }

        var allFilter = Filters.FirstOrDefault(f => f.Tag == WorkoutListFilterRule.AllTag);
        if (allFilter is not null)
            SelectFilter(allFilter);
    }

    /// <summary>Loads the optional style offer; a failure becomes a message on the card rather than an error page,</summary>
    /// because the plan itself is still usable.</summary>
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

    /// <summary>Applies another strength style.</summary>
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

    /// <summary>Dismisses the strength style offer.</summary>
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
    /// <summary>Sends the user to the preferences screen, which is where a plan is actually generated.</summary>
    private async Task GeneratePlanAsync()
    {
        await _navigateAsync(nameof(ProfileWorkoutPreferencesPage));
    }

    /// <summary>The empty-state action is available whenever the page is not loading.</summary>
    private bool CanUseEmptyWorkoutsAction()
    {
        return !IsBusy;
    }

    /// <summary>Splits the featured workout into its activities with their colours and counts; a rest day or an unloaded day</summary>
    /// has none.</summary>
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

    /// <summary>The tag in the reader's language, falling back to the lower-cased tag for an activity the catalog does</summary>
    /// not name.</summary>
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

    /// <summary>Opens the featured workout, resuming it instead of restarting when it is the session already in progress.</summary>
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

    /// <summary>Continues the session in progress through the page's activity picker, or starts it directly when nothing is</summary>
    /// listening.</summary>
    private async Task ResumeInProgressWorkoutAsync()
    {
        if (InProgressWorkout is not { IsInProgress: true, WorkoutDayId: > MissingWorkoutDayId } workout)
            return;
        if (SessionActivitiesRequested is { } handler)
            await handler(workout.WorkoutDayId);
        else
            await _openWorkoutSessionAsync(workout.WorkoutDayId);
    }

    /// <summary>Starts a session the same way, ignoring an entry with no day behind it.</summary>
    private async Task OpenWorkoutAsync(WorkoutItem? workout)
    {
        if (workout is null || workout.WorkoutDayId <= 0)
            return;

        if (SessionActivitiesRequested is { } handler)
            await handler(workout.WorkoutDayId);
        else
            await _openWorkoutSessionAsync(workout.WorkoutDayId);
    }

    /// <summary>Marks a session optional or required again, restoring the previous state when the server rejects the</summary>
    /// change so the card never shows a setting that was not saved.</summary>
    private async Task ToggleSessionOptionalAsync(WorkoutItem? workout)
    {
        if (workout is null || workout.WorkoutDayId <= 0 || workout.IsCompleted || workout.IsRestDay) return;
        StatusMessage = string.Empty;
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
        catch (Exception ex)
        {
            Trace.WriteLine($"[WorkoutViewModel] Toggle optional failed ({ex.GetType().Name}).");
            workout.IsOptional = wasOptional;
            workout.IsEasierAlternative = wasEasierAlternative;
            OnPropertyChanged(nameof(Workouts));
            StatusMessage = Services.Api.ApiFailurePresenter.FromException(ex, "Workout_Overview_UpdateFailed").MessageText;
        }
    }

    /// <summary>The same optimistic toggle for the easier-alternative flag.</summary>
    private async Task ToggleEasierAlternativeAsync(WorkoutItem? workout)
    {
        if (workout is null || !workout.CanMarkEasierAlternative) return;
        StatusMessage = string.Empty;
        var previous = workout.IsEasierAlternative;
        workout.IsEasierAlternative = !previous;
        try
        {
            await _overview.SetSessionOptionalAsync(new SetWorkoutSessionOptionalRequest(
                workout.WorkoutDayId, IsOptional: true, IsEasierAlternative: workout.IsEasierAlternative));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[WorkoutViewModel] Toggle easier alternative failed ({ex.GetType().Name}).");
            workout.IsEasierAlternative = previous;
            StatusMessage = Services.Api.ApiFailurePresenter.FromException(ex, "Workout_Overview_UpdateFailed").MessageText;
        }
    }

    /// <summary>Opens the reschedule sheet. Rescheduling is closed for this release, so this refuses even if a caller</summary>
    /// reaches the command behind the hidden entry point.</summary>
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
                DisplayFormat.DayMonth(day.Date)))
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

    /// <summary>Picks the new date and re-evaluates whether the move can be confirmed.</summary>
    public void SelectRescheduleDate(RescheduleDateOption? option)
    {
        if (option is null) return;
        foreach (var opt in AvailableRescheduleDates)
            opt.IsSelected = opt == option;
        SelectedRescheduleDate = option;
        ConfirmRescheduleCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Closes the sheet and forgets the workout, the date and any message it was showing.</summary>
    public void CloseRescheduleModal()
    {
        IsRescheduleModalVisible = false;
        ReschedulingWorkout = null;
        SelectedRescheduleDate = null;
        RescheduleStatusMessage = string.Empty;
        ConfirmRescheduleCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Moves the session to the chosen date, closing the sheet when rescheduling is unavailable rather than</summary>
    /// leaving a dead confirm button.</summary>
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

    /// <summary>The effort wording for the featured card. A strength tag reports a neutral effort, because the tag names</summary>
    /// the activity type and not how heavy the session is.</summary>
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

    /// <summary>The spoken description of a day in the strip: its weekday, date, training and that it is today.</summary>
    private static string BuildScheduleDayAccessibility(WorkoutScheduleEntry day)
    {
        var description = UiTextCatalog.Format(
            "Workout_Page_ScheduleDayAccessibility",
            UiTextCatalog.TranslateRuntimeText(day.DayLabel),
            DisplayFormat.DayNumberPadded(day.Date.ToDateTime(TimeOnly.MinValue)),
            UiTextCatalog.TranslateRuntimeText(day.Title));

        return day.IsToday
            ? UiTextCatalog.Format(
                "Workout_Page_ScheduleDayTodayAccessibility",
                description,
                UiTextCatalog.Get("Common_Today"))
            : description;
    }

    /// <summary>A weekday in the reader's language.</summary>
    private static string FormatDayLabel(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Sunday => UiTextCatalog.Get("Workout_Schedule_DaySunday"),
            DayOfWeek.Monday => UiTextCatalog.Get("Workout_Schedule_DayMonday"),
            DayOfWeek.Tuesday => UiTextCatalog.Get("Workout_Schedule_DayTuesday"),
            DayOfWeek.Wednesday => UiTextCatalog.Get("Workout_Schedule_DayWednesday"),
            DayOfWeek.Thursday => UiTextCatalog.Get("Workout_Schedule_DayThursday"),
            DayOfWeek.Friday => UiTextCatalog.Get("Workout_Schedule_DayFriday"),
            DayOfWeek.Saturday => UiTextCatalog.Get("Workout_Schedule_DaySaturday"),
            _ => string.Empty
        };
    }

    /// <summary>Raises the workout state properties.</summary>
    private void NotifyWorkoutStateProperties()
    {
        UpdateOverviewStatusTransitions();
        OnPropertyChanged(nameof(HasNoPlan));
        OnPropertyChanged(nameof(ShowNoPlanPrompt));
        OnPropertyChanged(nameof(HasWorkouts));
        OnPropertyChanged(nameof(HasNoWorkouts));
        OnPropertyChanged(nameof(HasScheduleDays));
        OnPropertyChanged(nameof(HasNoScheduleDays));
        OnPropertyChanged(nameof(ActivePlanTitle));
        OnPropertyChanged(nameof(PlanSummaryText));
        OnPropertyChanged(nameof(HasPlanWeeks));
        OnPropertyChanged(nameof(ShowEmptyWorkouts));
        OnPropertyChanged(nameof(EmptyWorkoutsTitle));
        OnPropertyChanged(nameof(EmptyWorkoutsSubtitle));
        OnPropertyChanged(nameof(EmptyWorkoutsButtonText));
        OnPropertyChanged(nameof(EmptyWorkoutsButtonVisible));
        EmptyWorkoutsActionCommand.NotifyCanExecuteChanged();
        GeneratePlanCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Refreshes the plan flags when the name changes.</summary>
    partial void OnActivePlanNameChanged(string? value)
    {
        NotifyWorkoutStateProperties();
    }

    /// <summary>Refreshes the workout flags when the list changes.</summary>
    partial void OnWorkoutsChanged(ObservableCollection<WorkoutItem> value)
    {
        NotifyWorkoutStateProperties();
    }

    /// <summary>Refreshes the schedule flags when the days change.</summary>
    partial void OnScheduleDaysChanged(ObservableCollection<ScheduleDayItem> value)
    {
        OnPropertyChanged(nameof(HasScheduleDays));
        OnPropertyChanged(nameof(HasNoScheduleDays));
    }

    /// <summary>Refreshes the command states when busy changes.</summary>
    partial void OnIsBusyChanged(bool value)
    {
        NotifyWorkoutStateProperties();
    }

    /// <summary>Refreshes the state flags when the load fails.</summary>
    partial void OnIsLoadErrorChanged(bool value)
    {
        NotifyWorkoutStateProperties();
    }

    /// <summary>Refreshes the injury banner when injuries change.</summary>
    partial void OnHasActiveInjuriesChanged(bool value)
    {
        UpdateOverviewStatusTransitions();
    }

    /// <summary>Loads what the user's injuries do to the plan. The banner is advisory, so a failure leaves the plan usable</summary>
    /// instead of failing the page.</summary>
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

    /// <summary>Raises the injury banner properties.</summary>
    private void NotifyInjuryBannerProperties()
    {
        OnPropertyChanged(nameof(HasBlockedExercises));
        OnPropertyChanged(nameof(HasRehabExercises));
        OnPropertyChanged(nameof(HasBlockedCardio));
    }
}

