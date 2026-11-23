using Microsoft.Maui.Graphics;

using MuscleCuties.Infrastructure.Services.Workout;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class WorkoutExerciseItem : ObservableObject
{
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanMarkOptional), nameof(CalibrationPromptText), nameof(HasRecommendationText))] private bool _isLogged;
    [ObservableProperty] private bool _isOptional;
    [ObservableProperty] private int _optionalSets;
    private bool _lastSavedIsOptional;
    private int _lastSavedOptionalSets;
    public bool LastSavedIsOptional => _lastSavedIsOptional;
    public int LastSavedOptionalSets => _lastSavedOptionalSets;

    public void SyncSavedOptionalState()
    {
        _lastSavedIsOptional = IsOptional;
        _lastSavedOptionalSets = OptionalSets;
    }

    public void RevertOptionalState()
    {
        IsOptional = _lastSavedIsOptional;
        OptionalSets = _lastSavedOptionalSets;
    }

    public bool ShouldInvokeOptionalApi()
    {
        if (IsLogged || !CanMarkOptional)
            return false;
        if (IsOptional == _lastSavedIsOptional && OptionalSets == _lastSavedOptionalSets)
            return false;
        return true;
    }
    private string _loggedEffortText = string.Empty;
    [ObservableProperty] private string _loggedRepsText = string.Empty;
    [ObservableProperty] private string _loggedSetsText = string.Empty;
    [ObservableProperty] private string _loggedWeightText = string.Empty;
    [ObservableProperty] private string _movementNotes = string.Empty;
    public bool HasMovementNotes => !string.IsNullOrWhiteSpace(MovementNotes);

    public int WorkoutDayExerciseId { get; set; }
    public int PlannedSets { get; set; }
    public int PlannedWarmUpSets { get; set; }
    public int MaxOptionalSets => Math.Max(0, PlannedSets - 1);
    public bool CanMarkOptional => !IsLogged;
    public int ExerciseId { get; set; }
    public string ActivityTag { get; set; } = string.Empty;
    public string IconGlyph => WorkoutActivityIcons.ForTag(ActivityTag);
    public string ActivityTitle { get; set; } = string.Empty;
    public Color ActivityBackground { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
    public Color ActivityTextColor { get; set; } = AppThemeResources.GetRequired<Color>("TextPrimary");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string TechniqueNotes { get; set; } = string.Empty;
    public string QuickTipsText { get; set; } = string.Empty;
    public string TargetText { get; set; } = string.Empty;
    public string PreviousText { get; set; } = string.Empty;
    public bool HasPreviousLog => !string.IsNullOrWhiteSpace(PreviousText) &&
        !PreviousText.Equals("No previous log", StringComparison.OrdinalIgnoreCase);
    public string RecommendationText { get; set; } = string.Empty;
    public bool HasRecommendationText => !string.IsNullOrWhiteSpace(RecommendationText) &&
        string.IsNullOrWhiteSpace(CalibrationPromptText);
    public bool UsesEnduranceMetrics { get; set; }
    public bool UsesStrengthMetrics => !UsesEnduranceMetrics;
    public bool UsesDurationMetric { get; set; }
    public bool UsesDistanceMetric { get; set; }
    public bool UsesPaceMetric { get; set; }
    public bool UsesHeartRateMetric { get; set; }
    public bool UsesPowerMetric { get; set; }
    public bool UsesCadenceMetric { get; set; }
    public bool UsesEffortMetric { get; set; }

    public bool UsesDurationOnlyMetric =>
        UsesDurationMetric &&
        !UsesDistanceMetric &&
        !UsesPaceMetric &&
        !UsesHeartRateMetric &&
        !UsesPowerMetric &&
        !UsesCadenceMetric &&
        !UsesEffortMetric;

    public bool UsesCardioMetricGrid =>
        UsesEnduranceMetrics &&
        !UsesDurationOnlyMetric;

    public bool UsesWeight { get; set; } = true;
    public string DurationLabel { get; set; } = "Minutes";
    public string DistanceLabel { get; set; } = "km";
    public string PaceLabel { get; set; } = "Pace / km";
    public string HeartRateLabel { get; set; } = "Avg bpm";
    public string PowerLabel { get; set; } = "Watts";
    public string CadenceLabel { get; set; } = "RPM";
    public string EffortLabel { get; set; } = "Effort";
    public ObservableCollection<ExerciseSetInputItem> Sets { get; } = [];
    public bool SetsWereEdited { get; private set; }

    public string LoggedDurationMinutesText { get; set; } = string.Empty;
    public string LoggedDistanceKmText { get; set; } = string.Empty;
    public string LoggedPaceText { get; set; } = string.Empty;
    public string LoggedHeartRateText { get; set; } = string.Empty;
    public string LoggedPowerWattsText { get; set; } = string.Empty;
    public string LoggedCadenceRpmText { get; set; } = string.Empty;

    public string LoggedEffortText
    {
        get => _loggedEffortText;
        set
        {
            if (!SetProperty(ref _loggedEffortText, value ?? string.Empty))
                return;

            OnPropertyChanged(nameof(EffortSliderValue));
            OnPropertyChanged(nameof(EffortDisplayText));
        }
    }

    public double EffortSliderValue
    {
        get => TryGetEffort(out var effort) ? effort : 5d;
        set => SetEffort(value, false);
    }

    public string EffortDisplayText => TryGetEffort(out var effort) ? $"{effort} / 10" : "Move to choose";

    public string DetailsButtonText => IsExpanded ? "Hide" : "Info";
    public string LogButtonText => IsLogged ? "Update" : "Log";
    public string LogStatusText => IsLogged ? "Logged today" : "Not logged yet";
    public bool HasVideo => !string.IsNullOrWhiteSpace(VideoUrl);
    public string VideoStatusText => HasVideo ? "Technique video saved" : string.Empty;

    public string WhyText => string.IsNullOrWhiteSpace(Description)
        ? "This movement supports today's workout focus and keeps the session balanced."
        : Description;

    public string CuesText => string.IsNullOrWhiteSpace(TechniqueNotes)
        ? Description
        : TechniqueNotes;

    public bool HasQuickTips => !string.IsNullOrWhiteSpace(QuickTipsText);

    // Gate 75, 76, 77: Unilateral, Superset split, and Initial Calibration UI metadata.
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsInSuperset), nameof(SupersetLabel))] private int? _supersetGroupId;
    public bool IsInSuperset => SupersetGroupId.HasValue;
    public string SupersetLabel => SupersetGroupId is { } groupId
        ? UiTextCatalog.Format("Workout_Session_SupersetLabelFormat", groupId)
        : string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(UnilateralModeLabel))] private bool _isUnilateral;
    public string UnilateralModeLabel => IsUnilateral ? UiTextCatalog.Get("Workout_Session_UnilateralPerSide") : string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CalibrationPromptText), nameof(HasRecommendationText))] private bool _requiresInitialCalibration;
    public string CalibrationPromptText => RequiresInitialCalibration && !IsLogged
        ? UiTextCatalog.Get("Workout_Session_CalibrationPrompt")
        : string.Empty;
    public Func<WorkoutExerciseItem, Task>? SplitSupersetAction { get; set; }

    [RelayCommand]
    private async Task SplitSuperset()
    {
        if (!IsInSuperset) return;
        if (SplitSupersetAction is null)
        {
            SupersetGroupId = null;
            return;
        }
        await SplitSupersetAction(this).ConfigureAwait(false);
    }

    [RelayCommand]
    public void AddSet()
    {
        var nextSetNumber = Sets.Count + 1;
        var nextWorkingSetNumber = Sets.Where(set => !set.IsWarmup)
            .Select(set => set.DisplaySetNumber > 0 ? set.DisplaySetNumber : set.SetNumber)
            .DefaultIfEmpty()
            .Max() + 1;
        var previousSet = Sets.LastOrDefault();
        var weight = previousSet?.WeightKg ?? (TryParseWeight(LoggedWeightText, out var w) ? w : 0.0);
        var reps = previousSet?.RepsCompleted ??
                   (int.TryParse(LoggedRepsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
                       ? r
                       : 10);
        var rest = previousSet?.RestTimeSeconds ?? 90;

        var set = new ExerciseSetInputItem(
            Guid.NewGuid(),
            Guid.Empty,
            ExerciseId.ToString(CultureInfo.InvariantCulture),
            Name,
            nextSetNumber,
            weight,
            reps,
            null,
            false,
            rest)
        {
            OnChanged = () => UpdateAggregatesFromSets(fromUser: true),
            RemoveCommand = RemoveSetCommand,
            ShowWeight = UsesWeight,
            ShowPerSideReps = IsUnilateral,
            DisplaySetNumber = nextWorkingSetNumber,
            WasEdited = true
        };
        if (UsesWeight && RequiresInitialCalibration && previousSet?.EnteredWeightKg is null)
            set.ClearUnobservedCalibrationWeightInput();
        Sets.Add(set);
        RenumberSets();
        UpdateAggregatesFromSets(fromUser: true);
    }

    [RelayCommand]
    public void RemoveSet(ExerciseSetInputItem? set)
    {
        if (set is null || Sets.Count <= 1) return;
        Sets.Remove(set);
        RenumberSets();
        UpdateAggregatesFromSets(fromUser: true);
    }

    public void EnsureSets(int count = 3, Guid sessionId = default)
    {
        if (Sets.Count > 0) return;
        var targetCount = Math.Max(1, count);
        var defaultWeight = TryParseWeight(LoggedWeightText, out var w) ? w : 0.0;
        var defaultReps = int.TryParse(LoggedRepsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) &&
                          r > 0
            ? r
            : 10;
        for (var i = 1; i <= targetCount; i++)
        {
            var set = new ExerciseSetInputItem(
                Guid.NewGuid(),
                sessionId,
                ExerciseId.ToString(CultureInfo.InvariantCulture),
                Name,
                i,
                defaultWeight,
                defaultReps,
                null,
                false,
                90)
            {
                OnChanged = () => UpdateAggregatesFromSets(fromUser: true),
                RemoveCommand = RemoveSetCommand,
                ShowWeight = UsesWeight,
                ShowPerSideReps = IsUnilateral
            };
            Sets.Add(set);
        }

        UpdateAggregatesFromSets();
    }

    /// <summary>
    /// Assigns the absolute set number used for persistence and the per-kind ordinal used by the row
    /// label and automation ids, so a warm-up no longer hides "Working set 1".
    /// </summary>
    public void RenumberSets()
    {
        var numbers = WorkoutSetNumbering.Number(Sets.Select(set => set.IsWarmup).ToArray());
        for (var index = 0; index < Sets.Count; index++)
        {
            Sets[index].SetNumber = numbers[index].AbsoluteNumber;
            Sets[index].DisplaySetNumber = numbers[index].DisplayNumber;
            Sets[index].ShowDivider = index > 0;
            Sets[index].CanRemove = Sets.Count > 1;
        }
    }

    public void UpdateAggregatesFromSets(bool fromUser = false)
    {
        SetsWereEdited |= fromUser || Sets.Any(s => s.WasEdited);
        RenumberSets();
        if (Sets.Count == 0) return;
        LoggedSetsText = Sets.Count.ToString(CultureInfo.InvariantCulture);
        var workingSets = Sets.Where(s => !s.IsWarmup).ToList();
        var primarySets = workingSets.Count > 0 ? workingSets : Sets.ToList();
        var avgReps = (int)Math.Round(primarySets.Average(s => s.EffectiveReps));
        LoggedRepsText = avgReps.ToString(CultureInfo.InvariantCulture);
        // A calibration exercise clears its unobserved rows, so the exercise-level load has to come
        // from whatever the user actually typed, including a warm-up row, or validation sees no load
        // even though the screen holds one.
        var enteredWeight = WorkoutWeightEntryRule.ResolveEnteredWeightKg(
            Sets.Select(set => new WorkoutEnteredSet(set.WeightInput, set.IsWarmup, set.WasEdited)).ToArray(),
            fallbackText: null);
        if (enteredWeight is { } weight)
            LoggedWeightText = DisplayFormat.Decimal1(weight);
    }

    private static bool TryParseWeight(string? value, out double weight)
    {
        var parsed = ExerciseSetInputItem.ParseWeight(value);
        weight = (double)parsed.GetValueOrDefault();
        return parsed.HasValue;
    }

    public void CommitEffortSliderValue(double value)
    {
        SetEffort(value, true);
    }

    private void SetEffort(double value, bool allowNeutralIfUnset)
    {
        if (!double.IsFinite(value))
            return;

        var effort = (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 1d, 10d);
        // An untouched centered thumb is a neutral starting position, not a recorded rating.
        if (!allowNeutralIfUnset && !TryGetEffort(out _) && effort == 5)
            return;

        LoggedEffortText = effort.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(EffortSliderValue));
    }

    private bool TryGetEffort(out int effort)
    {
        return int.TryParse(LoggedEffortText, NumberStyles.None, CultureInfo.InvariantCulture, out effort) &&
               effort is >= 1 and <= 10;
    }

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(DetailsButtonText));
    }

    partial void OnIsLoggedChanged(bool value)
    {
        OnPropertyChanged(nameof(LogButtonText));
        OnPropertyChanged(nameof(LogStatusText));
    }
}
