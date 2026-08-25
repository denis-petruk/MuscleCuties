using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.ViewModels.Workout;

public partial class WorkoutViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ICycleService _cycleService;
    private readonly IWorkoutService _workoutService;
    private readonly ViewModelLoadGate _loadGate = new(TimeSpan.FromSeconds(20));
    private List<WorkoutItem> _allWorkouts = new();
    private WorkoutItem? _featuredWorkout;
    private int _selectedWorkoutDayId;

    [ObservableProperty] private WorkoutPlan? _activePlan;
    [ObservableProperty] private List<WorkoutDay> _workoutDays = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _currentPhaseName = string.Empty;
    [ObservableProperty] private string _weekTitle = "This week's plan";
    [ObservableProperty] private string _featuredWorkoutBadgeText = "TODAY RECOVERY";
    [ObservableProperty] private string _featuredWorkoutTitle = "Living happy life";
    [ObservableProperty] private string _featuredWorkoutSubtitle = "Recovery day";
    [ObservableProperty] private string _featuredWorkoutDurationText = "Flexible";
    [ObservableProperty] private string _featuredWorkoutExercisesCount = "0";
    [ObservableProperty] private string _featuredWorkoutIntensity = "Light";
    [ObservableProperty] private bool _isWorkoutModalVisible;
    [ObservableProperty] private bool _isWorkoutDetailLoading;
    [ObservableProperty] private bool _isSelectedWorkoutRestDay;
    [ObservableProperty] private string _selectedWorkoutTitle = "Workout";
    [ObservableProperty] private string _selectedWorkoutSubtitle = string.Empty;
    [ObservableProperty] private string _selectedWorkoutSummaryText = string.Empty;
    [ObservableProperty] private string _workoutModalErrorText = string.Empty;
    [ObservableProperty] private string _workoutModalStatusText = string.Empty;
    [ObservableProperty] private bool _isExerciseDetailModalVisible;
    [ObservableProperty] private string _selectedExerciseName = string.Empty;
    [ObservableProperty] private string _selectedExerciseDescription = string.Empty;
    [ObservableProperty] private string _selectedExerciseTechniqueNotes = string.Empty;
    [ObservableProperty] private string _selectedExerciseTargetText = string.Empty;
    [ObservableProperty] private string _selectedExercisePreviousText = string.Empty;
    [ObservableProperty] private string _selectedExerciseRecommendationText = string.Empty;
    [ObservableProperty] private string _selectedExerciseVideoUrl = string.Empty;
    [ObservableProperty] private string _selectedExerciseImageUrl = string.Empty;
    [ObservableProperty] private ObservableCollection<FilterChipItem> _filters = new();
    [ObservableProperty] private ObservableCollection<WorkoutItem> _workouts = new();
    [ObservableProperty] private ObservableCollection<WorkoutExerciseItem> _selectedWorkoutExercises = new();

    public bool HasWorkouts => !IsBusy && Workouts.Count > 0;
    public bool HasNoWorkouts => !IsBusy && Workouts.Count == 0;
    public string ActivePlanTitle => ActivePlan?.Name ?? "No active plan";
    public string PlanSummaryText
    {
        get
        {
            var sessionCount = _allWorkouts.Count(workout => !workout.IsRestDay);
            if (sessionCount == 0)
                return "No sessions scheduled yet";

            return $"{sessionCount} workouts";
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
    public bool ShowWorkoutRestDayState => !IsWorkoutDetailLoading && IsSelectedWorkoutRestDay;
    public bool HasWorkoutModalError => !string.IsNullOrWhiteSpace(WorkoutModalErrorText);
    public bool HasWorkoutModalStatus => !string.IsNullOrWhiteSpace(WorkoutModalStatusText);
    public bool HasSelectedExerciseVideo => !string.IsNullOrWhiteSpace(SelectedExerciseVideoUrl);
    public bool HasSelectedExerciseImage => !string.IsNullOrWhiteSpace(SelectedExerciseImageUrl);
    public string SelectedExerciseVideoText => HasSelectedExerciseVideo ? "Video saved" : "Video pending";
    public string WorkoutLogButtonText => IsSelectedWorkoutRestDay ? "Log rest day" : "Log workout";
    private string SelectedFilterLabel => Filters.FirstOrDefault(f => f.IsSelected)?.Label ?? "All";

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand EmptyWorkoutsActionCommand { get; }
    public AsyncRelayCommand<WorkoutItem> OpenWorkoutCommand { get; }
    public AsyncRelayCommand SaveWorkoutSessionCommand { get; }
    public RelayCommand<FilterChipItem> SelectFilterCommand { get; }
    public AsyncRelayCommand StartFeaturedWorkoutCommand { get; }
    public RelayCommand CloseWorkoutModalCommand { get; }
    public RelayCommand<WorkoutExerciseItem> OpenExerciseDetailCommand { get; }
    public RelayCommand CloseExerciseDetailCommand { get; }

    public WorkoutViewModel(
        IAuthService authService,
        ICycleService cycleService,
        IWorkoutService workoutService)
    {
        _authService = authService;
        _cycleService = cycleService;
        _workoutService = workoutService;
        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync));
        EmptyWorkoutsActionCommand = new AsyncRelayCommand(HandleEmptyWorkoutsActionAsync, CanUseEmptyWorkoutsAction);
        OpenWorkoutCommand = new AsyncRelayCommand<WorkoutItem>(OpenWorkoutAsync);
        SaveWorkoutSessionCommand = new AsyncRelayCommand(SaveWorkoutSessionAsync);
        SelectFilterCommand = new RelayCommand<FilterChipItem>(SelectFilter);
        StartFeaturedWorkoutCommand = new AsyncRelayCommand(OpenFeaturedWorkoutAsync);
        CloseWorkoutModalCommand = new RelayCommand(CloseWorkoutModal);
        OpenExerciseDetailCommand = new RelayCommand<WorkoutExerciseItem>(OpenExerciseDetail);
        CloseExerciseDetailCommand = new RelayCommand(CloseExerciseDetail);
        Filters = new ObservableCollection<FilterChipItem>
        {
            new FilterChipItem { Label = "All", IsSelected = true },
            new FilterChipItem { Label = "Strength" },
            new FilterChipItem { Label = "Cardio" },
            new FilterChipItem { Label = "Climb" },
            new FilterChipItem { Label = "Yoga" },
            new FilterChipItem { Label = "Pilates" },
            new FilterChipItem { Label = "Recovery" }
        };
    }

    private async Task LoadDataCoreAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var phase = await _cycleService.GetCurrentPhaseAsync(userId);
            CurrentPhaseName = phase.ToString();
            var summary = await _workoutService.GetPlanSummaryAsync(userId, phase);

            ActivePlan = summary.ActivePlan;
            WorkoutDays = summary.WorkoutDays.ToList();
            _allWorkouts = summary.Workouts
                .Select(WorkoutItem.FromPlanItem)
                .ToList();

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
        {
            Workouts = new ObservableCollection<WorkoutItem>(_allWorkouts.Where(IsVisibleWorkoutItem));
        }
        else if (selected.Label == "Recovery")
        {
            Workouts = new ObservableCollection<WorkoutItem>(_allWorkouts.Where(IsRecoveryWorkoutItem));
        }
        else
        {
            Workouts = new ObservableCollection<WorkoutItem>(
                _allWorkouts.Where(w => !w.IsRestDay && string.Equals(
                    w.Tag,
                    selected.Label.ToUpperInvariant(),
                    StringComparison.OrdinalIgnoreCase)));
        }

        NotifyWorkoutStateProperties();
    }

    private void ApplyFeaturedWorkout()
    {
        var todayLabel = FormatDayLabel(DateTime.Today.DayOfWeek);
        var featuredWorkout = _allWorkouts.FirstOrDefault(workout => workout.DayLabel == todayLabel) ??
                              _allWorkouts.FirstOrDefault();

        if (featuredWorkout is null)
        {
            _featuredWorkout = null;
            FeaturedWorkoutBadgeText = "TODAY RECOVERY";
            FeaturedWorkoutTitle = ActivePlan is null ? "No active plan" : "Living happy life";
            FeaturedWorkoutSubtitle = ActivePlan is null
                ? "Your training sessions will appear once a plan exists."
                : "Recovery day";
            FeaturedWorkoutDurationText = "Rest day";
            FeaturedWorkoutExercisesCount = "0";
            FeaturedWorkoutIntensity = "Light";
            return;
        }

        _featuredWorkout = featuredWorkout;
        FeaturedWorkoutBadgeText = $"TODAY {featuredWorkout.Tag}";
        FeaturedWorkoutTitle = featuredWorkout.Title;
        FeaturedWorkoutSubtitle = featuredWorkout.DetailsText;
        FeaturedWorkoutDurationText = featuredWorkout.Duration;
        FeaturedWorkoutExercisesCount = ExtractExerciseCount(featuredWorkout.ExerciseCountText);
        FeaturedWorkoutIntensity = BuildFeaturedIntensity(featuredWorkout.Tag);
    }

    private async Task HandleEmptyWorkoutsActionAsync()
    {
        if (ActivePlan is null)
        {
            await _loadGate.RunAsync(LoadDataCoreAsync, force: true);
            return;
        }

        var allFilter = Filters.FirstOrDefault(f => f.Label == "All");
        if (allFilter is not null)
            SelectFilter(allFilter);
    }

    private bool CanUseEmptyWorkoutsAction() =>
        !IsBusy;

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
        IsWorkoutModalVisible = true;
        WorkoutModalErrorText = string.Empty;
        WorkoutModalStatusText = string.Empty;
        SelectedWorkoutTitle = workout.Title;
        SelectedWorkoutSubtitle = workout.Tag;
        SelectedWorkoutSummaryText = $"{workout.Duration} with {workout.ExerciseCountText}";
        IsSelectedWorkoutRestDay = false;

        await LoadWorkoutSessionDetailAsync();
    }

    private async Task LoadWorkoutSessionDetailAsync()
    {
        if (_selectedWorkoutDayId <= 0)
            return;

        IsWorkoutDetailLoading = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var detail = await _workoutService.GetWorkoutSessionDetailAsync(userId, _selectedWorkoutDayId);
            SelectedWorkoutTitle = detail.Title;
            SelectedWorkoutSubtitle = detail.Subtitle;
            SelectedWorkoutSummaryText = detail.SummaryText;
            SelectedWorkoutExercises = new ObservableCollection<WorkoutExerciseItem>(detail.Exercises);
            IsSelectedWorkoutRestDay = detail.IsRestDay;
            WorkoutModalErrorText = string.Empty;
        }
        catch
        {
            SelectedWorkoutExercises.Clear();
            IsSelectedWorkoutRestDay = false;
            WorkoutModalErrorText = "Could not open this workout yet. Please refresh the plan and try again.";
        }
        finally
        {
            IsWorkoutDetailLoading = false;
        }
    }

    private async Task SaveWorkoutSessionAsync()
    {
        if (_selectedWorkoutDayId <= 0 ||
            (!IsSelectedWorkoutRestDay && SelectedWorkoutExercises.Count == 0))
        {
            return;
        }

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var logs = SelectedWorkoutExercises
                .Select(item => new WorkoutExerciseLogInput(
                    item.WorkoutDayExerciseId,
                    item.ExerciseId,
                    ParsePositiveInt(item.LoggedSetsText),
                    ParsePositiveInt(item.LoggedRepsText),
                    ParseOptionalFloat(item.LoggedWeightText),
                    ParseDurationSeconds(item.LoggedDurationMinutesText),
                    ParseOptionalFloat(item.LoggedDistanceKmText),
                    ParsePositiveNullableInt(item.LoggedHeartRateText),
                    ParsePaceSecondsPerKm(item.LoggedPaceText)))
                .ToList();

            await _workoutService.LogWorkoutSessionAsync(userId, _selectedWorkoutDayId, logs, DateTime.Today);
            WorkoutModalStatusText = IsSelectedWorkoutRestDay
                ? "Rest day logged. Recovery counts too."
                : "Session saved. Your next suggestions are updated.";
            WorkoutModalErrorText = string.Empty;
            await LoadWorkoutSessionDetailAsync();
            _loadGate.MarkStale();
            await _loadGate.RunAsync(LoadDataCoreAsync, force: true);
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

    private void CloseWorkoutModal()
    {
        CloseExerciseDetail();
        IsWorkoutModalVisible = false;
        IsWorkoutDetailLoading = false;
        WorkoutModalErrorText = string.Empty;
        WorkoutModalStatusText = string.Empty;
        IsSelectedWorkoutRestDay = false;
        SelectedWorkoutExercises.Clear();
    }

    private void OpenExerciseDetail(WorkoutExerciseItem? exercise)
    {
        if (exercise is null)
            return;

        SelectedExerciseName = exercise.Name;
        SelectedExerciseDescription = exercise.Description;
        SelectedExerciseTechniqueNotes = string.IsNullOrWhiteSpace(exercise.TechniqueNotes)
            ? exercise.Description
            : exercise.TechniqueNotes;
        SelectedExerciseTargetText = exercise.TargetText;
        SelectedExercisePreviousText = exercise.PreviousText;
        SelectedExerciseRecommendationText = exercise.RecommendationText;
        SelectedExerciseVideoUrl = exercise.VideoUrl;
        SelectedExerciseImageUrl = exercise.ImageUrl;
        IsExerciseDetailModalVisible = true;
    }

    private void CloseExerciseDetail()
    {
        IsExerciseDetailModalVisible = false;
        SelectedExerciseName = string.Empty;
        SelectedExerciseDescription = string.Empty;
        SelectedExerciseTechniqueNotes = string.Empty;
        SelectedExerciseTargetText = string.Empty;
        SelectedExercisePreviousText = string.Empty;
        SelectedExerciseRecommendationText = string.Empty;
        SelectedExerciseVideoUrl = string.Empty;
        SelectedExerciseImageUrl = string.Empty;
    }

    private static string ExtractExerciseCount(string exerciseCountText)
    {
        var firstToken = exerciseCountText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return int.TryParse(firstToken, out var count) ? count.ToString() : "0";
    }

    private static int ParsePositiveInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsed)
            ? Math.Max(0, parsed)
            : 0;

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
        {
            return Math.Max(0, (minutes * 60) + seconds);
        }

        var decimalMinutes = ParseOptionalFloat(value);
        return decimalMinutes is > 0f ? (int)Math.Round(decimalMinutes.Value * 60f) : null;
    }

    private static bool IsVisibleWorkoutItem(WorkoutItem workout) =>
        !workout.IsRestDay;

    private static bool IsRecoveryWorkoutItem(WorkoutItem workout) =>
        !workout.IsRestDay && IsRecoveryTag(workout.Tag);

    private static bool IsRecoveryTag(string tag) =>
        tag.Contains("RECOVERY", StringComparison.OrdinalIgnoreCase);

    private static string BuildFeaturedIntensity(string tag)
    {
        if (tag.Contains("RECOVERY", StringComparison.OrdinalIgnoreCase))
            return "Light";

        if (tag.Contains("REST", StringComparison.OrdinalIgnoreCase))
            return "None";

        if (tag.Contains("YOGA", StringComparison.OrdinalIgnoreCase))
            return "Steady";

        if (tag.Contains("CARDIO", StringComparison.OrdinalIgnoreCase))
            return "Steady";

        return "Heavy";
    }

    private static string FormatDayLabel(DayOfWeek dayOfWeek) =>
        dayOfWeek switch
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
        OnPropertyChanged(nameof(ShowWorkoutRestDayState));
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

    partial void OnIsWorkoutDetailLoadingChanged(bool value)
    {
        NotifyWorkoutModalProperties();
    }

    partial void OnIsSelectedWorkoutRestDayChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowWorkoutRestDayState));
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

    partial void OnSelectedExerciseVideoUrlChanged(string value)
    {
        OnPropertyChanged(nameof(HasSelectedExerciseVideo));
        OnPropertyChanged(nameof(SelectedExerciseVideoText));
    }

    partial void OnSelectedExerciseImageUrlChanged(string value)
    {
        OnPropertyChanged(nameof(HasSelectedExerciseImage));
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyWorkoutStateProperties();
    }

}
