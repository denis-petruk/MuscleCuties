using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.Workout.Overview;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Workout;

public partial class WorkoutViewModel : ObservableObject, IPageLoadAware, IDisposable
{
    private const int FirstWeekIndex = 0;
    private const int LastWeekIndex = 3;
    private const int MissingWorkoutDayId = 0;
    private const int NoScheduledDays = 0;
    private const int SingleActivityCount = 1;

    private readonly IWorkoutInjuryUseCase _injuries;
    private readonly AppPageLoadGate _loadGate = new(AppPageLoadGate.PageFreshnessWindow);
    private readonly Func<string, Task> _navigateAsync;
    private readonly Func<int, Task> _openWorkoutSessionAsync;
    public event Func<int, Task>? SessionActivitiesRequested;
    private readonly IWorkoutOverviewUseCase _overview;

    [ObservableProperty] private string? _activePlanName;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasInProgressWorkout))]
    private WorkoutListEntry? _inProgressWorkout;
    private bool _hasLoadedOverview;
    private List<WorkoutItem> _allWorkouts = new();
    [ObservableProperty] private ObservableCollection<string> _blockedCardioNames = new();
    [ObservableProperty] private ObservableCollection<string> _blockedExerciseNames = new();
    [ObservableProperty] private string _currentPhaseName = string.Empty;
    private IReadOnlyList<ScheduleDayItem> _datedSchedule = [];
    private IReadOnlyList<WorkoutDayExercisePreview> _dayPreviews = [];
    private WorkoutItem? _featuredWorkout;
    [ObservableProperty] private string _featuredWorkoutActionText = "Start workout";
    [ObservableProperty] private bool _featuredWorkoutHasAction;
    [ObservableProperty] private bool _featuredWorkoutIsEditAction;
    [ObservableProperty] private bool _featuredWorkoutIsRestDay = true;

    [ObservableProperty] private Color _featuredWorkoutActivityBackground =
        WorkoutPresentationPalette.Background("STRENGTH");

    [ObservableProperty] private ObservableCollection<WorkoutActivitySection> _featuredWorkoutActivitySections = [];

    [ObservableProperty] private Color _featuredWorkoutActivityTextColor =
        WorkoutPresentationPalette.Text("STRENGTH");

    [ObservableProperty] private string _featuredWorkoutBadgeText = "Today recovery";
    [ObservableProperty] private string _featuredWorkoutDurationText = "Flexible";
    [ObservableProperty] private ObservableCollection<string> _featuredWorkoutExercisePreviews = [];
    [ObservableProperty] private string _featuredWorkoutExercisesCount = "0";
    [ObservableProperty] private bool _featuredWorkoutHasMultipleActivities;
    [ObservableProperty] private string _featuredWorkoutIntensity = "Light";
    [ObservableProperty] private string _featuredWorkoutSubtitle = "Recovery day";
    [ObservableProperty] private string _featuredWorkoutTitle = "Living happy life";
    [ObservableProperty] private ObservableCollection<FilterChipItem> _filters = new();

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

    [ObservableProperty] private string _weekTitle = "This week's plan";
    [ObservableProperty] private ObservableCollection<WorkoutItem> _workouts = new();

    public WorkoutViewModel(
        IWorkoutOverviewUseCase overview,
        IWorkoutInjuryUseCase injuries,
        Func<string, Task>? navigateAsync = null,
        Func<int, Task>? openWorkoutSessionAsync = null)
    {
        _overview = overview;
        _injuries = injuries;
        _navigateAsync = navigateAsync ?? (_ => Task.CompletedTask);
        _openWorkoutSessionAsync = openWorkoutSessionAsync ?? (_ => Task.CompletedTask);
        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync, this));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        EmptyWorkoutsActionCommand = new AsyncRelayCommand(HandleEmptyWorkoutsActionAsync, CanUseEmptyWorkoutsAction);
        GeneratePlanCommand = new AsyncRelayCommand(GeneratePlanAsync, () => !IsBusy);
        ResumeInProgressWorkoutCommand = new AsyncRelayCommand(ResumeInProgressWorkoutAsync);
        OpenWorkoutCommand = new AsyncRelayCommand<WorkoutItem>(OpenWorkoutAsync);
        ToggleSessionOptionalCommand = new AsyncRelayCommand<WorkoutItem>(ToggleSessionOptionalAsync);
        ToggleEasierAlternativeCommand = new AsyncRelayCommand<WorkoutItem>(ToggleEasierAlternativeAsync);
        SelectFilterCommand = new RelayCommand<FilterChipItem>(SelectFilter);
        StartFeaturedWorkoutCommand = new AsyncRelayCommand(OpenFeaturedWorkoutAsync);
        SelectWeekCommand = new RelayCommand<int>(SelectWeek);
        SelectScheduleDayCommand = new RelayCommand<ScheduleDayItem>(SelectScheduleDay);
        ToggleInjuryBannerCommand = new RelayCommand(() => IsInjuryBannerExpanded = !IsInjuryBannerExpanded);
        NavigateToInjuryLogCommand = new AsyncRelayCommand(OpenInjuryModalAsync);
        Filters = new ObservableCollection<FilterChipItem>
        {
            new() { Label = "All", IsSelected = true },
            new() { Label = "Strength" },
            new() { Label = "Cardio" },
            new() { Label = "Recovery" }
        };
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

    public bool IsTabBarVisible => !IsInjuryModalVisible && !IsSessionActivitiesVisible;

    partial void OnIsInjuryModalVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(IsTabBarVisible));

    partial void OnIsSessionActivitiesVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(IsTabBarVisible));

    public InjuryLogViewModel InjuryLogVm => _injuryLogVm ??= new InjuryLogViewModel(
        _injuries, () => Task.CompletedTask, RefreshAfterInjuryChangeAsync);

    public bool HasNoPlan => string.IsNullOrEmpty(ActivePlanName);
    public bool HasInProgressWorkout => InProgressWorkout is { IsInProgress: true, WorkoutDayId: > MissingWorkoutDayId };
    public bool ShowNoPlanPrompt => _hasLoadedOverview && !IsBusy && !IsLoadError && HasNoPlan;
    public bool HasWorkouts => !IsBusy && Workouts.Count > 0;
    public bool HasNoWorkouts => !IsBusy && !IsLoadError && Workouts.Count == 0;
    public string ActivePlanTitle => ActivePlanName ?? "No active plan";

    public string PlanSummaryText
    {
        get
        {
            var weekSchedule = _datedSchedule.Where(day => day.WeekIndex == SelectedWeekIndex).ToArray();
            var selectedWeekIds = GetSelectedWeekWorkoutIds();
            var weekWorkouts = weekSchedule.Length > NoScheduledDays
                ? _allWorkouts.Where(workout => selectedWeekIds.Contains(workout.WorkoutDayId)).ToArray()
                : _allWorkouts.ToArray();
            var trainingDayCount = weekSchedule.Length > NoScheduledDays
                ? weekSchedule.Count(day => !day.IsRestDay)
                : weekWorkouts
                .Where(workout => !workout.IsRestDay)
                .Select(workout => workout.DayLabel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            if (trainingDayCount == NoScheduledDays)
                return "No sessions scheduled yet";

            var fullRestDayCount = weekSchedule.Length > NoScheduledDays
                ? weekSchedule.Count(day => day.IsRestDay)
                : weekWorkouts
                .Where(workout => workout.IsRestDay)
                .Select(workout => workout.DayLabel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var activityCount = weekWorkouts
                .Where(workout => !workout.IsRestDay)
                .Sum(workout => workout.ActivityCount);
            var parts = new List<string>
            {
                $"{trainingDayCount} training {(trainingDayCount == 1 ? "day" : "days")}",
                $"{fullRestDayCount} full rest {(fullRestDayCount == 1 ? "day" : "days")}"
            };

            if (activityCount > trainingDayCount)
                parts.Add($"{activityCount} activities");

            return string.Join(" - ", parts);
        }
    }

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
    public AsyncRelayCommand ResumeInProgressWorkoutCommand { get; }
    public AsyncRelayCommand<WorkoutItem> OpenWorkoutCommand { get; }
    public AsyncRelayCommand<WorkoutItem> ToggleSessionOptionalCommand { get; }
    public AsyncRelayCommand<WorkoutItem> ToggleEasierAlternativeCommand { get; }
    public RelayCommand<FilterChipItem> SelectFilterCommand { get; }
    public AsyncRelayCommand StartFeaturedWorkoutCommand { get; }
    public RelayCommand<int> SelectWeekCommand { get; }
    public RelayCommand<ScheduleDayItem> SelectScheduleDayCommand { get; }
    public RelayCommand ToggleInjuryBannerCommand { get; }
    public AsyncRelayCommand NavigateToInjuryLogCommand { get; }

    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public bool IsPageLoading => IsBusy || IsPlanStale;

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
        IsPlanStale = true;
    }

    private async Task LoadDataCoreAsync()
    {
        var weekToRestore = SelectedWeekIndex;
        IsBusy = true;
        var loadStarted = Stopwatch.GetTimestamp();
        try
        {
            var summary = await _overview.LoadAsync(new WorkoutOverviewRequest(
                DateOnly.FromDateTime(DateTime.Today)));
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
                Title = item.DayOrder is { } order
                    ? string.Format(UiTextCatalog.Get("Workout_Overview_OrderedTitleFormat"), order, item.Title)
                    : item.Title,
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
            _datedSchedule = summary.Schedule.Select(day => new ScheduleDayItem
            {
                Date = day.Date.ToDateTime(TimeOnly.MinValue),
                WeekIndex = day.WeekIndex,
                DayLabel = day.DayLabel,
                WorkoutTag = day.Tag,
                WorkoutTitle = day.Title,
                WorkoutDayId = day.WorkoutDayId,
                CompanionWorkoutDayId = day.CompanionWorkoutDayId,
                IsToday = day.IsToday,
                IsCompleted = day.IsCompleted,
                IsRestDay = day.IsRestDay,
                AccentColor = WorkoutPresentationPalette.Background(day.Tag),
                AccentTextColor = WorkoutPresentationPalette.Text(day.Tag)
            }).ToArray();
            await LoadInjuryBannerAsync();
            WriteLoadStage("injury banner", loadStarted);
            SelectWeek(weekToRestore);
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

    private void ApplyFilter()
    {
        var selected = Filters.FirstOrDefault(f => f.IsSelected);
        var selectedWeekIds = GetSelectedWeekWorkoutIds();
        var hasWeekSchedule = _datedSchedule.Any(day => day.WeekIndex == SelectedWeekIndex);
        var weekWorkouts = hasWeekSchedule
            ? _allWorkouts.Where(workout => selectedWeekIds.Contains(workout.WorkoutDayId))
            : _allWorkouts.AsEnumerable();
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

    private void SelectWeek(int weekIndex)
    {
        if (weekIndex < FirstWeekIndex || weekIndex > LastWeekIndex) return;
        SelectedWeekIndex = weekIndex;
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
                day.CompanionWorkoutDayId == workout.WorkoutDayId);
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
    }

    private HashSet<int> GetSelectedWeekWorkoutIds()
    {
        return _datedSchedule
            .Where(day => day.WeekIndex == SelectedWeekIndex && day.WorkoutDayId > MissingWorkoutDayId)
            .SelectMany(day => day.CompanionWorkoutDayId is { } companionWorkoutDayId
                ? new[] { day.WorkoutDayId, companionWorkoutDayId }
                : [day.WorkoutDayId])
            .ToHashSet();
    }

    public void SelectScheduleDay(ScheduleDayItem? day)
    {
        if (day is null) return;
        SelectedScheduleDay = day;
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
            FeaturedWorkoutBadgeText = $"{selectedDay.DayLabel} rest";
            FeaturedWorkoutTitle = selectedDay.WorkoutTitle;
            FeaturedWorkoutSubtitle = "Recovery day";
            FeaturedWorkoutDurationText = "Rest day";
            FeaturedWorkoutExercisesCount = "0";
            FeaturedWorkoutIntensity = "None";
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
            FeaturedWorkoutBadgeText = "Today recovery";
            FeaturedWorkoutTitle = HasNoPlan ? "No active plan" : "Living happy life";
            FeaturedWorkoutSubtitle = HasNoPlan
                ? "Your training sessions will appear once a plan exists."
                : "Recovery day";
            FeaturedWorkoutDurationText = "Rest day";
            FeaturedWorkoutExercisesCount = "0";
            FeaturedWorkoutIntensity = "Light";
            FeaturedWorkoutActionText = "Start workout";
            FeaturedWorkoutActivityBackground =
                WorkoutPresentationPalette.Background("REST");
            FeaturedWorkoutActivityTextColor =
                WorkoutPresentationPalette.Text("REST");
            FeaturedWorkoutActivitySections = [];
            FeaturedWorkoutExercisePreviews = [];
            FeaturedWorkoutHasMultipleActivities = false;
            return;
        }

        _featuredWorkout = featuredWorkout;
        FeaturedWorkoutIsRestDay = featuredWorkout.IsRestDay;
        FeaturedWorkoutHasAction = !featuredWorkout.IsRestDay;
        FeaturedWorkoutIsEditAction = featuredWorkout.IsCompleted;
        FeaturedWorkoutActivityBackground = featuredWorkout.ActivityBackground;
        FeaturedWorkoutActivityTextColor = featuredWorkout.ActivityTextColor;
        var featuredDayLabel = selectedDay is { IsToday: false }
            ? selectedDay.DayLabel
            : "Today";
        FeaturedWorkoutBadgeText = featuredWorkout.IsCompleted
            ? "Workout completed"
            : $"{featuredDayLabel} {FormatActivityTag(featuredWorkout.Tag)}";
        FeaturedWorkoutTitle = featuredWorkout.Title;
        FeaturedWorkoutSubtitle = featuredWorkout.DetailsText;
        FeaturedWorkoutDurationText = featuredWorkout.Duration;
        FeaturedWorkoutExercisesCount = featuredWorkout.ExerciseCount.ToString(CultureInfo.InvariantCulture);
        FeaturedWorkoutIntensity = BuildFeaturedIntensity(featuredWorkout.Tag);
        FeaturedWorkoutActionText = featuredWorkout.IsCompleted
            ? "Edit workout"
            : featuredWorkout.IsRestDay
                ? "Log rest day"
                : "Start workout";

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

    private async Task GeneratePlanAsync()
    {
        await _navigateAsync("ProfileWorkoutPreferencesPage");
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

    private static string FormatActivityTag(string tag)
    {
        return tag switch
        {
            "CARDIO" => "cardio",
            "RECOVERY" => "recovery",
            "REST" => "rest",
            "STRENGTH" => "strength",
            _ => tag.ToLowerInvariant()
        };
    }

    private async Task OpenFeaturedWorkoutAsync()
    {
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
            return "Light";

        if (tag.Contains("REST", StringComparison.OrdinalIgnoreCase))
            return "None";

        if (tag.Contains("CARDIO", StringComparison.OrdinalIgnoreCase))
            return "Steady";

        return "Heavy";
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

    private async Task LoadInjuryBannerAsync()
    {
        var impact = await _injuries.GetImpactAsync();
        HasActiveInjuries = impact.HasActiveInjuries;
        InjuryBannerSummary = impact.Summary;
        BlockedExerciseNames = new ObservableCollection<string>(impact.BlockedExercises);
        RehabExerciseNames = new ObservableCollection<string>(impact.RehabExercises);
        BlockedCardioNames = new ObservableCollection<string>(impact.BlockedActivities);
        NotifyInjuryBannerProperties();
    }

    private void NotifyInjuryBannerProperties()
    {
        OnPropertyChanged(nameof(HasBlockedExercises));
        OnPropertyChanged(nameof(HasRehabExercises));
        OnPropertyChanged(nameof(HasBlockedCardio));
    }
}
