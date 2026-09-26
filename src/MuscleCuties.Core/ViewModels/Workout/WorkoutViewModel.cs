using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;
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
    private readonly Func<int, Task> _openWorkoutSessionAsync;
    private readonly ViewModelLoadGate _loadGate = new(ViewModelLoadGate.PageFreshnessWindow);
    private InjuryLogViewModel? _injuryLogVm;

    [ObservableProperty] private bool _isInjuryModalVisible;

    [ObservableProperty] private WorkoutPlan? _activePlan;
    private List<WorkoutItem> _allWorkouts = new();
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

    [ObservableProperty] private string _weekTitle = "This week's plan";
    [ObservableProperty] private List<WorkoutDay> _workoutDays = new();
    [ObservableProperty] private ObservableCollection<WorkoutItem> _workouts = new();

    [ObservableProperty] private bool _hasActiveInjuries;
    [ObservableProperty] private string _injuryBannerSummary = string.Empty;
    [ObservableProperty] private bool _isInjuryBannerExpanded;
    [ObservableProperty] private ObservableCollection<string> _blockedExerciseNames = new();
    [ObservableProperty] private ObservableCollection<string> _rehabExerciseNames = new();
    [ObservableProperty] private ObservableCollection<string> _blockedCardioNames = new();

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
        await _loadGate.RunAsync(LoadDataCoreAsync, this, true);
    }

    public WorkoutViewModel(
        IServiceScopeFactory scopeFactory,
        Func<string, Task>? navigateAsync = null,
        Func<int, Task>? openWorkoutSessionAsync = null)
    {
        _scopeFactory = scopeFactory;
        _navigateAsync = navigateAsync ?? (_ => Task.CompletedTask);
        _openWorkoutSessionAsync = openWorkoutSessionAsync ?? (_ => Task.CompletedTask);
        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync, this));
        EmptyWorkoutsActionCommand = new AsyncRelayCommand(HandleEmptyWorkoutsActionAsync, CanUseEmptyWorkoutsAction);
        OpenWorkoutCommand = new AsyncRelayCommand<WorkoutItem>(OpenWorkoutAsync);
        SelectFilterCommand = new RelayCommand<FilterChipItem>(SelectFilter);
        StartFeaturedWorkoutCommand = new AsyncRelayCommand(OpenFeaturedWorkoutAsync);
        ToggleInjuryBannerCommand = new RelayCommand(() => IsInjuryBannerExpanded = !IsInjuryBannerExpanded);
        NavigateToInjuryLogCommand = new AsyncRelayCommand(() => _navigateAsync("InjuryLogPage"));
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
    }

    public bool HasWorkouts => !IsBusy && Workouts.Count > 0;
    public bool HasNoWorkouts => !IsBusy && !IsLoadError && Workouts.Count == 0;
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

    private string SelectedFilterLabel => Filters.FirstOrDefault(f => f.IsSelected)?.Label ?? "All";

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand EmptyWorkoutsActionCommand { get; }
    public AsyncRelayCommand<WorkoutItem> OpenWorkoutCommand { get; }
    public RelayCommand<FilterChipItem> SelectFilterCommand { get; }
    public AsyncRelayCommand StartFeaturedWorkoutCommand { get; }
    public RelayCommand ToggleInjuryBannerCommand { get; }
    public AsyncRelayCommand NavigateToInjuryLogCommand { get; }
    public bool IsPageLoading => IsBusy && !_loadGate.HasLoaded;

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

            // These services share one scoped DbContext; await every operation
            // before starting the next, including the injury banner queries.
            var phase = await DataLoadScheduler.RunAsync(() => cycleService.GetCurrentPhaseAsync(userId));
            await LoadInjuryBannerAsync(scope.ServiceProvider, userId);

            CurrentPhaseName = phase.ToString();
            var summary = await DataLoadScheduler.RunAsync(() => workoutService.GetPlanSummaryAsync(userId, phase));

            ActivePlan = summary.ActivePlan;
            WorkoutDays = summary.WorkoutDays.ToList();
            _allWorkouts = summary.Workouts.ToList();

            ApplyFeaturedWorkout();
            ApplyFilter();
            NotifyWorkoutStateProperties();

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
            await _loadGate.RunAsync(LoadDataCoreAsync, this, true);
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

        await _openWorkoutSessionAsync(workout.WorkoutDayId);
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

    partial void OnActivePlanChanged(WorkoutPlan? value)
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

    private async Task LoadInjuryBannerAsync(IServiceProvider services, int userId)
    {
        var injuryRepo = services.GetRequiredService<IWorkoutInjuryRepository>();
        var logs = await DataLoadScheduler.RunAsync(() => injuryRepo.GetActiveAsync(userId));

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
        var (blocked, rehab) = await DataLoadScheduler.RunAsync(() => injuryRepo.GetExerciseImpactAsync(flags));
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
}
