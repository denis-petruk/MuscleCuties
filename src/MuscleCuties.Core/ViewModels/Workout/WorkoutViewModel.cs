using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Cycle;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Models.Workout.Logging;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Repositories.Workout.Planning;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Core.ViewModels.Common;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.Core.ViewModels.Workout;

public partial class WorkoutViewModel : ObservableObject, IPageLoadAware
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Func<string, Task> _navigateAsync;
    private readonly ViewModelLoadGate _loadGate = new(ViewModelLoadGate.PageFreshnessWindow);
    private readonly Dictionary<int, WorkoutSessionDetail> _sessionDetailCache = new();
    private InjuryLogViewModel? _injuryLogVm;

    [ObservableProperty] private bool _isInjuryModalVisible;

    [ObservableProperty] private WorkoutPlan? _activePlan;
    private List<WorkoutItem> _allWorkouts = new();
    [ObservableProperty] private string _celebrationIconSource = CyclePhaseAssets.FollicularAnimation;
    [ObservableProperty] private int _celebrationToken;
    [ObservableProperty] private string _currentPhaseName = string.Empty;
    private WorkoutItem? _featuredWorkout;
    [ObservableProperty] private string _featuredWorkoutActionText = "Start workout";

    [ObservableProperty]
    private Color _featuredWorkoutActivityBackground =
        WorkoutActivityClassifier.GetBackground(WorkoutActivityClassifier.StrengthTag);

    [ObservableProperty]
    private Color _featuredWorkoutActivityTextColor =
        WorkoutActivityClassifier.GetTextColor(WorkoutActivityClassifier.StrengthTag);

    [ObservableProperty] private string _featuredWorkoutBadgeText = "Today recovery";
    [ObservableProperty] private string _featuredWorkoutDurationText = "Flexible";
    [ObservableProperty] private string _featuredWorkoutExercisesCount = "0";
    [ObservableProperty] private string _featuredWorkoutIntensity = "Light";
    [ObservableProperty] private string _featuredWorkoutSubtitle = "Recovery day";
    [ObservableProperty] private string _featuredWorkoutTitle = "Living happy life";
    [ObservableProperty] private ObservableCollection<WorkoutActivitySection> _featuredWorkoutActivitySections = [];
    [ObservableProperty] private bool _featuredWorkoutHasMultipleActivities;
    [ObservableProperty] private ObservableCollection<FilterChipItem> _filters = new();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isSelectedWorkoutCompleted;
    [ObservableProperty] private bool _isSelectedWorkoutRestDay;
    [ObservableProperty] private bool _isWorkoutDetailLoading;
    [ObservableProperty] private bool _isWorkoutModalVisible;

    [ObservableProperty]
    private ObservableCollection<WorkoutActivitySectionItem> _selectedWorkoutActivitySections = new();

    private int _selectedWorkoutDayId;
    [ObservableProperty] private ObservableCollection<WorkoutExerciseItem> _selectedWorkoutExercises = new();
    [ObservableProperty] private string _selectedWorkoutSubtitle = string.Empty;
    [ObservableProperty] private string _selectedWorkoutSummaryText = string.Empty;
    [ObservableProperty] private string _selectedWorkoutTitle = "Workout";
    [ObservableProperty] private string _weekTitle = "This week's plan";
    [ObservableProperty] private List<WorkoutDay> _workoutDays = new();
    [ObservableProperty] private string _workoutModalErrorText = string.Empty;
    [ObservableProperty] private string _workoutModalStatusText = string.Empty;
    [ObservableProperty] private ObservableCollection<WorkoutItem> _workouts = new();

    [ObservableProperty] private bool _hasActiveInjuries;
    [ObservableProperty] private string _injuryBannerSummary = string.Empty;
    [ObservableProperty] private bool _isInjuryBannerExpanded;
    [ObservableProperty] private ObservableCollection<string> _blockedExerciseNames = new();
    [ObservableProperty] private ObservableCollection<string> _rehabExerciseNames = new();
    [ObservableProperty] private ObservableCollection<string> _blockedCardioNames = new();

    [ObservableProperty] private bool _isSwapPanelVisible;
    [ObservableProperty] private bool _isSwapLoading;
    [ObservableProperty] private ObservableCollection<ExerciseSwapOption> _swapCandidates = new();
    private WorkoutExerciseItem? _swapTarget;

    public bool HasBlockedExercises => BlockedExerciseNames.Count > 0;
    public bool HasRehabExercises => RehabExerciseNames.Count > 0;
    public bool HasBlockedCardio => BlockedCardioNames.Count > 0;
    public InjuryLogViewModel InjuryLogVm => _injuryLogVm ??= new InjuryLogViewModel(
        _scopeFactory, () => Task.CompletedTask, RefreshAfterInjuryChangeAsync);

    [RelayCommand]
    private async Task OpenInjuryModalAsync()
    {
        IsInjuryModalVisible = true;
        if (!InjuryLogVm.IsBusy)
            await InjuryLogVm.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void CloseInjuryModal() => IsInjuryModalVisible = false;

    private async Task RefreshAfterInjuryChangeAsync()
    {
        HasActiveInjuries = InjuryLogVm.HasActiveInjuries;
        Invalidate();
        await _loadGate.RunAsync(LoadDataCoreAsync, true);
    }

    public WorkoutViewModel(IServiceScopeFactory scopeFactory, Func<string, Task>? navigateAsync = null)
    {
        _scopeFactory = scopeFactory;
        _navigateAsync = navigateAsync ?? (_ => Task.CompletedTask);
        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync));
        EmptyWorkoutsActionCommand = new AsyncRelayCommand(HandleEmptyWorkoutsActionAsync, CanUseEmptyWorkoutsAction);
        OpenWorkoutCommand = new AsyncRelayCommand<WorkoutItem>(OpenWorkoutAsync);
        SaveWorkoutSessionCommand = new AsyncRelayCommand(SaveWorkoutSessionAsync);
        LogWorkoutExerciseCommand = new AsyncRelayCommand<WorkoutExerciseItem>(LogWorkoutExerciseAsync);
        LogWorkoutActivityCommand = new AsyncRelayCommand<WorkoutActivitySectionItem>(LogWorkoutActivityAsync);
        SelectFilterCommand = new RelayCommand<FilterChipItem>(SelectFilter);
        StartFeaturedWorkoutCommand = new AsyncRelayCommand(OpenFeaturedWorkoutAsync);
        CloseWorkoutModalCommand = new RelayCommand(CloseWorkoutModal);
        OpenExerciseDetailCommand = new RelayCommand<WorkoutExerciseItem>(OpenExerciseDetail);
        ToggleInjuryBannerCommand = new RelayCommand(() => IsInjuryBannerExpanded = !IsInjuryBannerExpanded);
        NavigateToInjuryLogCommand = new AsyncRelayCommand(() => _navigateAsync("InjuryLogPage"));
        OpenSwapPanelCommand = new AsyncRelayCommand<WorkoutExerciseItem>(OpenSwapPanelAsync);
        ConfirmSwapCommand = new AsyncRelayCommand(ConfirmSwapAsync);
        CloseSwapPanelCommand = new RelayCommand(CloseSwapPanel);
        SelectSwapCandidateCommand = new RelayCommand<ExerciseSwapOption>(SelectSwapCandidate);
        Filters = new ObservableCollection<FilterChipItem>
        {
            new() { Label = "All", IsSelected = true },
            new() { Label = "Strength" },
            new() { Label = "Cardio" },
            new() { Label = "Recovery" }
        };
    }

    public void Invalidate()
    {
        _loadGate.MarkStale();
        _sessionDetailCache.Clear();
    }

    public bool HasWorkouts => !IsBusy && Workouts.Count > 0;
    public bool HasNoWorkouts => !IsBusy && Workouts.Count == 0;
    public string ActivePlanTitle => ActivePlan?.Name ?? "No active plan";

    public string PlanSummaryText
    {
        get
        {
            var trainingDayCount = _allWorkouts
                .Where(workout => !workout.IsRestDay)
                .Select(workout => workout.DayLabel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            if (trainingDayCount == 0)
                return "No sessions scheduled yet";

            var fullRestDayCount = _allWorkouts
                .Where(workout => workout.IsRestDay)
                .Select(workout => workout.DayLabel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var activityCount = _allWorkouts
                .Where(workout => !workout.IsRestDay)
                .Sum(workout => ExtractCount(workout.ActivityCountText));
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

    public string EmptyWorkoutsTitle => ActivePlan is null
        ? "No active workout plan"
        : SelectedFilterLabel == "All"
            ? "No workouts scheduled"
            : $"No {SelectedFilterLabel.ToLowerInvariant()} workouts";

    public string EmptyWorkoutsSubtitle => ActivePlan is null
        ? "Your weekly sessions will appear here once a plan exists."
        : "Try another filter or show the full week.";

    public string EmptyWorkoutsButtonText => ActivePlan is null ? "Refresh" : "Show All";
    public bool HasSelectedWorkoutExercises => !IsWorkoutDetailLoading && SelectedWorkoutExercises.Count > 0;
    public bool HasSelectedWorkoutActivities => !IsWorkoutDetailLoading && SelectedWorkoutActivitySections.Count > 0;
    public bool ShowWorkoutRestDayState => !IsWorkoutDetailLoading && IsSelectedWorkoutRestDay;

    public bool ShowWorkoutFooterAction =>
        !IsWorkoutDetailLoading &&
        (IsSelectedWorkoutRestDay || HasSelectedWorkoutActivities || HasSelectedWorkoutExercises);

    public bool HasWorkoutModalError => !string.IsNullOrWhiteSpace(WorkoutModalErrorText);
    public bool HasWorkoutModalStatus => !string.IsNullOrWhiteSpace(WorkoutModalStatusText);

    public string WorkoutLogButtonText => IsSelectedWorkoutCompleted
        ? "Save changes"
        : IsSelectedWorkoutRestDay
            ? "Log rest day"
            : "Submit workout";

    private string SelectedFilterLabel => Filters.FirstOrDefault(f => f.IsSelected)?.Label ?? "All";

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand EmptyWorkoutsActionCommand { get; }
    public AsyncRelayCommand<WorkoutItem> OpenWorkoutCommand { get; }
    public AsyncRelayCommand SaveWorkoutSessionCommand { get; }
    public AsyncRelayCommand<WorkoutExerciseItem> LogWorkoutExerciseCommand { get; }
    public AsyncRelayCommand<WorkoutActivitySectionItem> LogWorkoutActivityCommand { get; }
    public RelayCommand<FilterChipItem> SelectFilterCommand { get; }
    public AsyncRelayCommand StartFeaturedWorkoutCommand { get; }
    public RelayCommand CloseWorkoutModalCommand { get; }
    public RelayCommand<WorkoutExerciseItem> OpenExerciseDetailCommand { get; }
    public RelayCommand ToggleInjuryBannerCommand { get; }
    public AsyncRelayCommand NavigateToInjuryLogCommand { get; }
    public AsyncRelayCommand<WorkoutExerciseItem> OpenSwapPanelCommand { get; }
    public AsyncRelayCommand ConfirmSwapCommand { get; }
    public RelayCommand CloseSwapPanelCommand { get; }
    public RelayCommand<ExerciseSwapOption> SelectSwapCandidateCommand { get; }
    public bool IsPageLoading => IsBusy && !_loadGate.HasLoaded;
    public bool HasSwapCandidates => SwapCandidates.Count > 0;
    public string SwapTargetName => _swapTarget?.Name ?? string.Empty;

    private async Task LoadDataCoreAsync()
    {
        IsBusy = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var cycleService = scope.ServiceProvider.GetRequiredService<ICycleService>();
            var workoutService = scope.ServiceProvider.GetRequiredService<IWorkoutService>();

            var userId = await DataLoadScheduler.RunAsync(authService.GetCurrentUserIdAsync);
            var phase = await DataLoadScheduler.RunAsync(() => cycleService.GetCurrentPhaseAsync(userId));
            CurrentPhaseName = phase.ToString();
            var summary = await DataLoadScheduler.RunAsync(() => workoutService.GetPlanSummaryAsync(userId, phase));

            ActivePlan = summary.ActivePlan;
            WorkoutDays = summary.WorkoutDays.ToList();
            _allWorkouts = summary.Workouts.ToList();

            ApplyFeaturedWorkout();
            ApplyFilter();
            NotifyWorkoutStateProperties();

            await LoadInjuryBannerAsync(scope.ServiceProvider, userId);
        }
        finally
        {
            IsBusy = false;
        }
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
        if (selected is null || selected.Label == "All")
            Workouts = new ObservableCollection<WorkoutItem>(_allWorkouts.Where(IsVisibleWorkoutItem));
        else if (selected.Label == "Recovery")
            Workouts = new ObservableCollection<WorkoutItem>(_allWorkouts.Where(IsRecoveryWorkoutItem));
        else
            Workouts = new ObservableCollection<WorkoutItem>(
                _allWorkouts.Where(w => !w.IsRestDay && string.Equals(
                    w.Tag,
                    selected.Label.ToUpperInvariant(),
                    StringComparison.OrdinalIgnoreCase)));

        NotifyWorkoutStateProperties();
    }

    private void ApplyFeaturedWorkout()
    {
        var todayLabel = FormatDayLabel(DateTime.Today.DayOfWeek);
        var todayWorkouts = _allWorkouts.Where(workout => workout.DayLabel == todayLabel).ToList();
        var featuredWorkout = todayWorkouts.FirstOrDefault(workout => !workout.IsCompleted)
                              ?? todayWorkouts.FirstOrDefault()
                              ?? _allWorkouts.FirstOrDefault();

        if (featuredWorkout is null)
        {
            _featuredWorkout = null;
            FeaturedWorkoutBadgeText = "Today recovery";
            FeaturedWorkoutTitle = ActivePlan is null ? "No active plan" : "Living happy life";
            FeaturedWorkoutSubtitle = ActivePlan is null
                ? "Your training sessions will appear once a plan exists."
                : "Recovery day";
            FeaturedWorkoutDurationText = "Rest day";
            FeaturedWorkoutExercisesCount = "0";
            FeaturedWorkoutIntensity = "Light";
            FeaturedWorkoutActionText = "Start workout";
            FeaturedWorkoutActivityBackground =
                WorkoutActivityClassifier.GetBackground(WorkoutActivityClassifier.RestTag);
            FeaturedWorkoutActivityTextColor =
                WorkoutActivityClassifier.GetTextColor(WorkoutActivityClassifier.RestTag);
            FeaturedWorkoutActivitySections = [];
            FeaturedWorkoutHasMultipleActivities = false;
            return;
        }

        _featuredWorkout = featuredWorkout;
        FeaturedWorkoutActivityBackground = featuredWorkout.ActivityBackground;
        FeaturedWorkoutActivityTextColor = featuredWorkout.ActivityTextColor;
        FeaturedWorkoutBadgeText = featuredWorkout.IsCompleted
            ? "Workout completed"
            : $"Today {FormatActivityTag(featuredWorkout.Tag)}";
        FeaturedWorkoutTitle = featuredWorkout.Title;
        FeaturedWorkoutSubtitle = featuredWorkout.DetailsText;
        FeaturedWorkoutDurationText = featuredWorkout.Duration;
        FeaturedWorkoutExercisesCount = ExtractExerciseCount(featuredWorkout.ExerciseCountText);
        FeaturedWorkoutIntensity = BuildFeaturedIntensity(featuredWorkout.Tag);
        FeaturedWorkoutActionText = featuredWorkout.IsCompleted
            ? "Edit workout"
            : featuredWorkout.IsRestDay
                ? "Log rest day"
                : "Start workout";

        var sections = BuildFeaturedActivitySections(featuredWorkout);
        FeaturedWorkoutActivitySections = new ObservableCollection<WorkoutActivitySection>(sections);
        FeaturedWorkoutHasMultipleActivities = sections.Count > 1;
    }

    private async Task HandleEmptyWorkoutsActionAsync()
    {
        if (ActivePlan is null)
        {
            await _loadGate.RunAsync(LoadDataCoreAsync, true);
            return;
        }

        var allFilter = Filters.FirstOrDefault(f => f.Label == "All");
        if (allFilter is not null)
            SelectFilter(allFilter);
    }

    private bool CanUseEmptyWorkoutsAction()
    {
        return !IsBusy;
    }

    private IReadOnlyList<WorkoutActivitySection> BuildFeaturedActivitySections(WorkoutItem featuredWorkout)
    {
        var day = WorkoutDays.FirstOrDefault(d => d.Id == featuredWorkout.WorkoutDayId);
        if (day is null || day.WorkoutType is WorkoutType.Rest)
            return [];

        var tags = WorkoutActivityClassifier.BuildActivityTags(day);
        return tags.Select(tag =>
        {
            var count = day.WorkoutDayExercises
                .Count(ex => WorkoutActivityClassifier.ClassifyExerciseTag(ex, day.WorkoutType) == tag);
            return new WorkoutActivitySection(
                tag,
                WorkoutActivityClassifier.BuildSectionTitle(tag),
                count,
                WorkoutActivityClassifier.GetBackground(tag),
                WorkoutActivityClassifier.GetTextColor(tag));
        }).ToList();
    }

    private static string FormatActivityTag(string tag)
    {
        return tag switch
        {
            WorkoutActivityClassifier.CardioTag => "cardio",
            WorkoutActivityClassifier.RecoveryTag => "recovery",
            WorkoutActivityClassifier.RestTag => "rest",
            WorkoutActivityClassifier.StrengthTag => "strength",
            _ => tag.ToLowerInvariant()
        };
    }

    private async Task OpenFeaturedWorkoutAsync()
    {
        if (_featuredWorkout is not null)
            await OpenWorkoutAsync(_featuredWorkout);
    }

    private async Task OpenWorkoutAsync(WorkoutItem? workout)
    {
        if (workout is null || workout.WorkoutDayId <= 0)
            return;

        _selectedWorkoutDayId = workout.WorkoutDayId;
        CollapseExerciseDetails();
        WorkoutModalErrorText = string.Empty;
        WorkoutModalStatusText = string.Empty;
        SelectedWorkoutTitle = workout.Title;
        SelectedWorkoutSubtitle = workout.Tag;
        SelectedWorkoutSummaryText = string.IsNullOrWhiteSpace(workout.ActivityCountText)
            ? $"{workout.Duration} with {workout.ExerciseCountText}"
            : $"{workout.Duration} with {workout.ExerciseCountText} - {workout.ActivityCountText}";
        IsSelectedWorkoutRestDay = workout.IsRestDay;
        IsSelectedWorkoutCompleted = workout.IsCompleted;

        var hasCachedDetail = _sessionDetailCache.ContainsKey(_selectedWorkoutDayId);
        IsWorkoutDetailLoading = !hasCachedDetail;
        if (!hasCachedDetail)
        {
            SelectedWorkoutExercises.Clear();
            SelectedWorkoutActivitySections.Clear();
        }

        IsWorkoutModalVisible = true;
        await LoadWorkoutSessionDetailAsync();
    }

    private async Task LoadWorkoutSessionDetailAsync()
    {
        if (_selectedWorkoutDayId <= 0)
            return;

        if (_sessionDetailCache.TryGetValue(_selectedWorkoutDayId, out var cached))
        {
            ApplyWorkoutSessionDetail(cached);
            return;
        }

        IsWorkoutDetailLoading = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var workoutService = scope.ServiceProvider.GetRequiredService<IWorkoutService>();

            var userId = await authService.GetCurrentUserIdAsync();
            var detail = await workoutService.GetWorkoutSessionDetailAsync(userId, _selectedWorkoutDayId);
            _sessionDetailCache[detail.WorkoutDayId] = detail;
            ApplyWorkoutSessionDetail(detail);
        }
        catch
        {
            SelectedWorkoutExercises.Clear();
            SelectedWorkoutActivitySections.Clear();
            IsSelectedWorkoutRestDay = false;
            WorkoutModalErrorText = "Could not open this workout yet. Please refresh the plan and try again.";
        }
        finally
        {
            IsWorkoutDetailLoading = false;
        }
    }

    private void ApplyWorkoutSessionDetail(WorkoutSessionDetail detail)
    {
        SelectedWorkoutTitle = detail.Title;
        SelectedWorkoutSubtitle = detail.Subtitle;
        SelectedWorkoutSummaryText = detail.SummaryText;
        SelectedWorkoutExercises = new ObservableCollection<WorkoutExerciseItem>(detail.Exercises);
        SelectedWorkoutActivitySections = new ObservableCollection<WorkoutActivitySectionItem>(
            detail.Activities.Count > 0
                ? detail.Activities
                : BuildFallbackActivitySections(detail.Exercises));
        IsSelectedWorkoutRestDay = detail.IsRestDay;
        WorkoutModalErrorText = string.Empty;
    }

    private async Task SaveWorkoutSessionAsync()
    {
        if (_selectedWorkoutDayId <= 0)
            return;

        var exercises = SelectedWorkoutExercises.ToList();
        if (!IsSelectedWorkoutRestDay &&
            !ValidateWorkoutLogInputs(exercises, true))
            return;

        await SaveWorkoutLogsAsync(
            exercises,
            IsSelectedWorkoutRestDay
                ? "Rest day logged. Recovery counts too."
                : "Workout complete. Your next suggestions are updated.");
    }

    private async Task LogWorkoutExerciseAsync(WorkoutExerciseItem? exercise)
    {
        if (exercise is null)
            return;

        var action = exercise.IsLogged ? "updated" : "logged";
        await SaveWorkoutLogsAsync([exercise], $"{exercise.Name} {action}.");
    }

    private async Task LogWorkoutActivityAsync(WorkoutActivitySectionItem? activity)
    {
        if (activity is null || activity.Exercises.Count == 0)
            return;

        var action = activity.IsLogged ? "updated" : "logged";
        await SaveWorkoutLogsAsync(activity.Exercises.ToList(), $"{activity.Title} {action}.");
    }

    private async Task SaveWorkoutLogsAsync(
        IReadOnlyCollection<WorkoutExerciseItem> exercises,
        string successText)
    {
        if (_selectedWorkoutDayId <= 0 ||
            (!IsSelectedWorkoutRestDay && exercises.Count == 0))
            return;

        try
        {
            if (!IsSelectedWorkoutRestDay &&
                !ValidateWorkoutLogInputs(exercises, false))
                return;

            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var workoutService = scope.ServiceProvider.GetRequiredService<IWorkoutService>();

            var userId = await authService.GetCurrentUserIdAsync();
            var logs = exercises
                .GroupBy(exercise => exercise.WorkoutDayExerciseId)
                .Select(group => BuildLogInput(group.Last()))
                .ToList();

            await workoutService.LogWorkoutSessionAsync(userId, _selectedWorkoutDayId, logs, DateTime.Today);
            _sessionDetailCache.Remove(_selectedWorkoutDayId);
            WorkoutModalStatusText = successText;
            var sessionComplete = IsSelectedWorkoutRestDay || IsEveryExerciseLoggedAfterSave(exercises);
            IsSelectedWorkoutCompleted = sessionComplete;
            TriggerCelebration();
            WorkoutModalErrorText = string.Empty;

            if (sessionComplete)
            {
                await Task.Delay(1200);
                CloseWorkoutModal();
            }
            else
            {
                await LoadWorkoutSessionDetailAsync();
            }

            _loadGate.MarkStale();
            await _loadGate.RunAsync(LoadDataCoreAsync, true);
        }
        catch (InvalidOperationException ex)
        {
            WorkoutModalErrorText = ex.Message;
            WorkoutModalStatusText = string.Empty;
        }
        catch
        {
            WorkoutModalErrorText = "Could not save this workout yet. Please check the numbers and try again.";
            WorkoutModalStatusText = string.Empty;
        }
    }

    private bool ValidateWorkoutLogInputs(
        IReadOnlyCollection<WorkoutExerciseItem> exercises,
        bool isWholeSessionSubmit)
    {
        if (exercises.Count == 0)
        {
            WorkoutModalErrorText = "Add at least one exercise before submitting this workout.";
            WorkoutModalStatusText = string.Empty;
            return false;
        }

        var missingSetsOrReps = exercises.FirstOrDefault(exercise =>
            exercise.UsesStrengthMetrics &&
            (ParsePositiveInt(exercise.LoggedSetsText) <= 0 ||
             ParsePositiveInt(exercise.LoggedRepsText) <= 0));
        if (missingSetsOrReps is not null)
        {
            WorkoutModalErrorText = $"Add sets and reps for {missingSetsOrReps.Name} before logging it.";
            WorkoutModalStatusText = string.Empty;
            return false;
        }

        var missingWeight = exercises.FirstOrDefault(exercise =>
            exercise.UsesStrengthMetrics &&
            exercise.UsesWeight &&
            ParseOptionalFloat(exercise.LoggedWeightText) is null);
        if (missingWeight is not null)
        {
            WorkoutModalErrorText = isWholeSessionSubmit
                ? $"Add kg for {missingWeight.Name} before submitting the workout. Use 0 kg for bodyweight."
                : $"Add kg for {missingWeight.Name} before logging it. Use 0 kg for bodyweight.";
            WorkoutModalStatusText = string.Empty;
            return false;
        }

        WorkoutModalErrorText = string.Empty;
        return true;
    }

    private static WorkoutExerciseLogInput BuildLogInput(WorkoutExerciseItem item)
    {
        return new WorkoutExerciseLogInput(
            item.WorkoutDayExerciseId,
            item.ExerciseId,
            ParsePositiveInt(item.LoggedSetsText),
            ParsePositiveInt(item.LoggedRepsText),
            ParseOptionalFloat(item.LoggedWeightText),
            ParseDurationSeconds(item.LoggedDurationMinutesText),
            ParseOptionalFloat(item.LoggedDistanceKmText),
            ParsePositiveNullableInt(item.LoggedHeartRateText),
            ParsePaceSecondsPerKm(item.LoggedPaceText),
            ParsePositiveNullableInt(item.LoggedPowerWattsText),
            ParsePositiveNullableInt(item.LoggedCadenceRpmText),
            ParseEffortRating(item.LoggedEffortText));
    }

    private bool IsEveryExerciseLoggedAfterSave(IReadOnlyCollection<WorkoutExerciseItem> savedExercises)
    {
        var loggedIds = SelectedWorkoutExercises
            .Where(exercise => exercise.IsLogged)
            .Select(exercise => exercise.WorkoutDayExerciseId)
            .ToHashSet();
        foreach (var exercise in savedExercises)
            loggedIds.Add(exercise.WorkoutDayExerciseId);

        return SelectedWorkoutExercises.Count > 0 && loggedIds.Count >= SelectedWorkoutExercises.Count;
    }

    private void CloseWorkoutModal()
    {
        if (IsSwapPanelVisible)
        {
            CloseSwapPanel();
            return;
        }

        IsWorkoutModalVisible = false;
    }

    private void OpenExerciseDetail(WorkoutExerciseItem? exercise)
    {
        if (exercise is null)
            return;

        var shouldExpand = !exercise.IsExpanded;
        foreach (var item in SelectedWorkoutExercises)
            item.IsExpanded = false;

        exercise.IsExpanded = shouldExpand;
    }

    private void CollapseExerciseDetails()
    {
        foreach (var exercise in SelectedWorkoutExercises)
            exercise.IsExpanded = false;
    }

    private static string ExtractExerciseCount(string exerciseCountText)
    {
        var count = ExtractCount(exerciseCountText);
        return count > 0 ? count.ToString() : "0";
    }

    private static int ExtractCount(string text)
    {
        var firstToken = text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return int.TryParse(firstToken, out var count) ? count : 0;
    }

    private static int ParsePositiveInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsed)
            ? Math.Max(0, parsed)
            : 0;
    }

    private static int? ParsePositiveNullableInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return ParsePositiveInt(value);
    }

    private static int? ParseDurationSeconds(string? minutesText)
    {
        var minutes = ParseOptionalFloat(minutesText);
        return minutes is > 0f ? (int)Math.Round(minutes.Value * 60f) : null;
    }

    private static float? ParseOptionalFloat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var currentCultureValue))
            return Math.Max(0f, currentCultureValue);

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue))
            return Math.Max(0f, invariantValue);

        return null;
    }

    private static int? ParsePaceSecondsPerKm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parts = value.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.CurrentCulture, out var minutes) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.CurrentCulture, out var seconds))
            return Math.Max(0, minutes * 60 + seconds);

        var decimalMinutes = ParseOptionalFloat(value);
        return decimalMinutes is > 0f ? (int)Math.Round(decimalMinutes.Value * 60f) : null;
    }

    private static int? ParseEffortRating(string? value)
    {
        var effort = ParsePositiveNullableInt(value);
        return effort is >= 1 and <= 10 ? effort : null;
    }

    private static bool IsVisibleWorkoutItem(WorkoutItem workout)
    {
        return !workout.IsRestDay;
    }

    private static bool IsRecoveryWorkoutItem(WorkoutItem workout)
    {
        return !workout.IsRestDay && WorkoutActivityClassifier.IsRecoveryTag(workout.Tag);
    }

    private static string BuildFeaturedIntensity(string tag)
    {
        if (tag.Contains(WorkoutActivityClassifier.RecoveryTag, StringComparison.OrdinalIgnoreCase))
            return "Light";

        if (tag.Contains(WorkoutActivityClassifier.RestTag, StringComparison.OrdinalIgnoreCase))
            return "None";

        if (tag.Contains(WorkoutActivityClassifier.CardioTag, StringComparison.OrdinalIgnoreCase))
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

    private static IReadOnlyList<WorkoutActivitySectionItem> BuildFallbackActivitySections(
        IReadOnlyList<WorkoutExerciseItem> exercises)
    {
        if (exercises.Count == 0)
            return [];

        const string activityTag = WorkoutActivityClassifier.StrengthTag;
        return
        [
            new WorkoutActivitySectionItem
            {
                Tag = activityTag,
                Title = WorkoutActivityClassifier.BuildSectionTitle(activityTag),
                Subtitle = WorkoutActivityClassifier.BuildSectionSubtitle(activityTag),
                MetricText = $"{exercises.Count} {(exercises.Count == 1 ? "exercise" : "exercises")}",
                SummaryText = $"{exercises.Count(exercise => exercise.IsLogged)} of {exercises.Count} logged",
                ActivityBackground = WorkoutActivityClassifier.GetBackground(activityTag),
                ActivityTextColor = WorkoutActivityClassifier.GetTextColor(activityTag),
                Exercises = new ObservableCollection<WorkoutExerciseItem>(exercises)
            }
        ];
    }

    private void TriggerCelebration()
    {
        CelebrationIconSource = Enum.TryParse<CyclePhase>(CurrentPhaseName, out var phase)
            ? CyclePhaseAssets.GetVisualSource(phase)
            : CyclePhaseAssets.FollicularAnimation;
        CelebrationToken++;
    }

    private void NotifyWorkoutStateProperties()
    {
        OnPropertyChanged(nameof(HasWorkouts));
        OnPropertyChanged(nameof(HasNoWorkouts));
        OnPropertyChanged(nameof(ActivePlanTitle));
        OnPropertyChanged(nameof(PlanSummaryText));
        OnPropertyChanged(nameof(EmptyWorkoutsTitle));
        OnPropertyChanged(nameof(EmptyWorkoutsSubtitle));
        OnPropertyChanged(nameof(EmptyWorkoutsButtonText));
        EmptyWorkoutsActionCommand.NotifyCanExecuteChanged();
    }

    private void NotifyWorkoutModalProperties()
    {
        OnPropertyChanged(nameof(HasSelectedWorkoutExercises));
        OnPropertyChanged(nameof(HasSelectedWorkoutActivities));
        OnPropertyChanged(nameof(ShowWorkoutRestDayState));
        OnPropertyChanged(nameof(ShowWorkoutFooterAction));
        OnPropertyChanged(nameof(HasWorkoutModalError));
        OnPropertyChanged(nameof(HasWorkoutModalStatus));
        OnPropertyChanged(nameof(WorkoutLogButtonText));
    }

    partial void OnActivePlanChanged(WorkoutPlan? value)
    {
        NotifyWorkoutStateProperties();
    }

    partial void OnWorkoutsChanged(ObservableCollection<WorkoutItem> value)
    {
        NotifyWorkoutStateProperties();
    }

    partial void OnSelectedWorkoutExercisesChanged(ObservableCollection<WorkoutExerciseItem> value)
    {
        NotifyWorkoutModalProperties();
    }

    partial void OnSelectedWorkoutActivitySectionsChanged(ObservableCollection<WorkoutActivitySectionItem> value)
    {
        NotifyWorkoutModalProperties();
    }

    partial void OnIsWorkoutDetailLoadingChanged(bool value)
    {
        NotifyWorkoutModalProperties();
    }

    partial void OnIsSelectedWorkoutRestDayChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowWorkoutRestDayState));
        OnPropertyChanged(nameof(ShowWorkoutFooterAction));
        OnPropertyChanged(nameof(WorkoutLogButtonText));
    }

    partial void OnIsSelectedWorkoutCompletedChanged(bool value)
    {
        OnPropertyChanged(nameof(WorkoutLogButtonText));
    }

    partial void OnWorkoutModalErrorTextChanged(string value)
    {
        NotifyWorkoutModalProperties();
    }

    partial void OnWorkoutModalStatusTextChanged(string value)
    {
        NotifyWorkoutModalProperties();
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyWorkoutStateProperties();
    }

    private async Task LoadInjuryBannerAsync(IServiceProvider services, int userId)
    {
        var injuryRepo = services.GetRequiredService<IWorkoutInjuryRepository>();
        var logs = await injuryRepo.GetActiveAsync(userId);

        if (logs.Count == 0)
        {
            HasActiveInjuries = false;
            InjuryBannerSummary = string.Empty;
            BlockedExerciseNames = new ObservableCollection<string>();
            RehabExerciseNames = new ObservableCollection<string>();
            BlockedCardioNames = new ObservableCollection<string>();
            NotifyInjuryBannerProperties();
            return;
        }

        HasActiveInjuries = true;

        var injuries = logs
            .Where(log => Enum.IsDefined(typeof(InjuryFlag), log.SiteFlag) &&
                          Enum.TryParse<InjuryStatus>(log.Status, true, out _))
            .Select(log => new Injury(
                (InjuryFlag)log.SiteFlag,
                Enum.Parse<InjuryStatus>(log.Status, true),
                log.Since))
            .ToList();

        var flags = WorkoutInjuryRules.ToFlags(injuries);
        var (blocked, rehab) = await injuryRepo.GetExerciseImpactAsync(flags);
        var blockedCardio = WorkoutInjuryRules.GetBlockedActivities(injuries)
            .Select(a => a.ToString())
            .OrderBy(n => n)
            .ToList();

        var siteSummaries = injuries
            .Select(i => $"{DisplayNameFor(i.Site)} ({i.Status})")
            .ToList();
        var summaryParts = string.Join(", ", siteSummaries);
        InjuryBannerSummary = blocked.Count > 0
            ? $"{summaryParts} -- {blocked.Count} exercises blocked"
            : summaryParts;

        BlockedExerciseNames = new ObservableCollection<string>(blocked);
        RehabExerciseNames = new ObservableCollection<string>(rehab);
        BlockedCardioNames = new ObservableCollection<string>(blockedCardio);
        NotifyInjuryBannerProperties();
    }

    private void NotifyInjuryBannerProperties()
    {
        OnPropertyChanged(nameof(HasBlockedExercises));
        OnPropertyChanged(nameof(HasRehabExercises));
        OnPropertyChanged(nameof(HasBlockedCardio));
    }

    private static string DisplayNameFor(InjuryFlag flag) => flag switch
    {
        InjuryFlag.Metatarsal => "Foot",
        InjuryFlag.LowBack => "Low back",
        _ => flag.ToString()
    };

    private async Task OpenSwapPanelAsync(WorkoutExerciseItem? exercise)
    {
        if (exercise is null) return;

        _swapTarget = exercise;
        IsSwapPanelVisible = true;
        IsSwapLoading = true;
        OnPropertyChanged(nameof(SwapTargetName));

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var workoutService = scope.ServiceProvider.GetRequiredService<IWorkoutService>();

            var userId = await authService.GetCurrentUserIdAsync();
            System.Diagnostics.Trace.WriteLine($"[Swap] userId={userId}, dayExerciseId={exercise.WorkoutDayExerciseId}");
            var candidates = await workoutService.GetSwapCandidatesAsync(userId, exercise.WorkoutDayExerciseId);
            System.Diagnostics.Trace.WriteLine($"[Swap] Got {candidates.Count} candidates");

            SwapCandidates = new ObservableCollection<ExerciseSwapOption>(candidates);
            OnPropertyChanged(nameof(HasSwapCandidates));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Swap] ERROR: {ex}");
        }
        finally
        {
            IsSwapLoading = false;
        }
    }

    private async Task ConfirmSwapAsync()
    {
        var selected = SwapCandidates.FirstOrDefault(c => c.IsSelected);
        if (selected is null || _swapTarget is null) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var workoutService = scope.ServiceProvider.GetRequiredService<IWorkoutService>();

            var userId = await authService.GetCurrentUserIdAsync();
            await workoutService.SwapExerciseAsync(userId, _swapTarget.WorkoutDayExerciseId, selected.ExerciseId, true);

            _sessionDetailCache.Remove(_selectedWorkoutDayId);
            CloseSwapPanel();
            await LoadWorkoutSessionDetailAsync();
        }
        catch
        {
            CloseSwapPanel();
            WorkoutModalErrorText = "Could not swap this exercise. Please try again.";
        }
    }

    private void SelectSwapCandidate(ExerciseSwapOption? option)
    {
        if (option is null) return;
        foreach (var c in SwapCandidates)
            c.IsSelected = c == option;
    }

    private void CloseSwapPanel()
    {
        IsSwapPanelVisible = false;
        _swapTarget = null;
        SwapCandidates = new ObservableCollection<ExerciseSwapOption>();
        OnPropertyChanged(nameof(HasSwapCandidates));
        OnPropertyChanged(nameof(SwapTargetName));
    }
}
