using Microsoft.Maui.Graphics;

using MuscleCuties.Infrastructure.Services.Workout;

namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>One exercise in a session, with its planned prescription, the live set rows being edited,</summary>
/// and the metric fields the card shows for endurance movements.</summary>
public partial class WorkoutExerciseItem : ObservableObject
{
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanMarkOptional), nameof(CalibrationPromptText), nameof(HasRecommendationText))] private bool _isLogged;
    [ObservableProperty] private bool _isOptional;
    [ObservableProperty] private int _optionalSets;
    private bool _lastSavedIsOptional;
    private int _lastSavedOptionalSets;
    /// <summary>The optional flag last saved on the server.</summary>
    public bool LastSavedIsOptional => _lastSavedIsOptional;
    /// <summary>The optional set count last saved on the server.</summary>
    public int LastSavedOptionalSets => _lastSavedOptionalSets;

    /// <summary>Remembers the optional settings that were just saved, so a discarded edit can be undone.</summary>
    public void SyncSavedOptionalState()
    {
        _lastSavedIsOptional = IsOptional;
        _lastSavedOptionalSets = OptionalSets;
    }

    /// <summary>Restores the optional settings to the last saved values, discarding an unsaved change.</summary>
    public void RevertOptionalState()
    {
        IsOptional = _lastSavedIsOptional;
        OptionalSets = _lastSavedOptionalSets;
    }

    /// <summary>True only when the optional settings differ from the saved ones and the exercise is not</summary>
    /// already logged, so an unchanged card sends no request.</summary>
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
    /// <summary>True when the exercise has movement notes.</summary>
    [ObservableProperty] private string _movementNotes = string.Empty;

    /// <summary>Identity of this exercise within the planned day, used when logging against the plan.</summary>
    public int WorkoutDayExerciseId { get; set; }
    /// <summary>Working sets the plan prescribes, before any optional reduction.</summary>
    public int PlannedSets { get; set; }
    /// <summary>Warm-up sets the plan prescribes, shown before the working sets.</summary>
    public int PlannedWarmUpSets { get; set; }
    /// <summary>Lowest optional set count is one fewer than planned, so an optional movement is still</summary>
    /// trained rather than dropped.</summary>
    public int MaxOptionalSets => Math.Max(0, PlannedSets - 1);
    /// <summary>True when the exercise can be marked optional.</summary>
    public bool CanMarkOptional => !IsLogged;
    /// <summary>Catalog exercise id, saved with the log.</summary>
    public int ExerciseId { get; set; }
    /// <summary>Activity tag of the block this exercise belongs to; it selects the icon and colours.</summary>
    public string ActivityTag { get; set; } = string.Empty;
    /// <summary>Glyph for the exercise's activity tag.</summary>
    public string IconGlyph => WorkoutActivityIcons.ForTag(ActivityTag);
    /// <summary>Localized activity name shown above the exercise.</summary>
    public string ActivityTitle { get; set; } = string.Empty;
    /// <summary>Background tint inherited from the activity, so a block reads as one unit.</summary>
    public Color ActivityBackground { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
    /// <summary>Text colour that stays readable on the activity tint.</summary>
    public Color ActivityTextColor { get; set; } = AppThemeResources.GetRequired<Color>("TextPrimary");
    /// <summary>Exercise name as shown on the card.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Why the movement is in this session; also the fallback for the cue text.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Illustration for the movement, when one is cached locally.</summary>
    public string ImageUrl { get; set; } = string.Empty;
    /// <summary>Coaching cues from the catalog, shown in the expanded card.</summary>
    public string TechniqueNotes { get; set; } = string.Empty;
    /// <summary>Short tips shown while the card is collapsed.</summary>
    public string QuickTipsText { get; set; } = string.Empty;
    /// <summary>Planned sets and reps summarised for the card header.</summary>
    public string TargetText { get; set; } = string.Empty;
    /// <summary>What was lifted last time for this movement, already formatted.</summary>
    public string PreviousText { get; set; } = string.Empty;
    /// <summary>True only when there is a real previous entry; the "no previous log" placeholder does not</summary>
    /// count as one.</summary>
    public bool HasPreviousLog => !string.IsNullOrWhiteSpace(PreviousText) &&
        !PreviousText.Equals("No previous log", StringComparison.OrdinalIgnoreCase);
    /// <summary>The recommendation line shown for the exercise.</summary>
    public string RecommendationText { get; set; } = string.Empty;
    /// <summary>True when a recommendation line is shown.</summary>
    public bool HasRecommendationText => !string.IsNullOrWhiteSpace(RecommendationText) &&
        string.IsNullOrWhiteSpace(CalibrationPromptText);
    /// <summary>True when the exercise is tracked by time and distance.</summary>
    public bool UsesEnduranceMetrics { get; set; }
    /// <summary>True when the exercise is tracked by weight and reps.</summary>
    public bool UsesStrengthMetrics => !UsesEnduranceMetrics;
    /// <summary>The endurance field set shown for this movement; each flag adds one entry field.</summary>
    public bool UsesDurationMetric { get; set; }
    /// <summary>Whether the movement records distance.</summary>
    public bool UsesDistanceMetric { get; set; }
    /// <summary>Whether the movement records pace.</summary>
    public bool UsesPaceMetric { get; set; }
    /// <summary>Whether the movement records heart rate.</summary>
    public bool UsesHeartRateMetric { get; set; }
    /// <summary>Whether the movement records power.</summary>
    public bool UsesPowerMetric { get; set; }
    /// <summary>Whether the movement records cadence.</summary>
    public bool UsesCadenceMetric { get; set; }
    /// <summary>True when the exercise logs an effort rating.</summary>
    public bool UsesEffortMetric { get; set; }

    /// <summary>True when only the duration metric applies.</summary>
    public bool UsesDurationOnlyMetric =>
        UsesDurationMetric &&
        !UsesDistanceMetric &&
        !UsesPaceMetric &&
        !UsesHeartRateMetric &&
        !UsesPowerMetric &&
        !UsesCadenceMetric &&
        !UsesEffortMetric;

    /// <summary>True when the cardio metric grid is shown.</summary>
    public bool UsesCardioMetricGrid =>
        UsesEnduranceMetrics &&
        !UsesDurationOnlyMetric;

    /// <summary>False for bodyweight work, which hides the load field on every set row.</summary>
    public bool UsesWeight { get; set; } = true;
    /// <summary>Label above the duration field; the unit depends on the activity.</summary>
    public string DurationLabel { get; set; } = "Minutes";
    /// <summary>Unit shown for the distance field.</summary>
    public string DistanceLabel { get; set; } = "km";
    /// <summary>Hint for the pace field, including its unit.</summary>
    public string PaceLabel { get; set; } = "Pace / km";
    /// <summary>Hint for the heart-rate field.</summary>
    public string HeartRateLabel { get; set; } = "Avg bpm";
    /// <summary>Unit shown for the power field.</summary>
    public string PowerLabel { get; set; } = "Watts";
    /// <summary>Unit shown for the cadence field.</summary>
    public string CadenceLabel { get; set; } = "RPM";
    /// <summary>Label above the effort slider.</summary>
    public string EffortLabel { get; set; } = "Effort";
    /// <summary>The sets the user is entering.</summary>
    public ObservableCollection<ExerciseSetInputItem> Sets { get; } = [];
    /// <summary>Sticky flag: once any set row changed, the plan's prescribed sets are not reused.</summary>
    public bool SetsWereEdited { get; private set; }

    /// <summary>Endurance inputs for this movement; empty means the field was left blank.</summary>
    public string LoggedDurationMinutesText { get; set; } = string.Empty;
    /// <summary>Distance typed for an endurance movement, in the label's unit.</summary>
    public string LoggedDistanceKmText { get; set; } = string.Empty;
    /// <summary>Pace typed for an endurance movement, as minutes and seconds per unit.</summary>
    public string LoggedPaceText { get; set; } = string.Empty;
    /// <summary>Average heart rate typed for an endurance movement.</summary>
    public string LoggedHeartRateText { get; set; } = string.Empty;
    /// <summary>Power typed for an endurance movement, in watts.</summary>
    public string LoggedPowerWattsText { get; set; } = string.Empty;
    /// <summary>Cadence typed for an endurance movement, in the label's unit.</summary>
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

    /// <summary>Shows the chosen effort, or the prompt that the slider has not been touched.</summary>
    public string EffortDisplayText => TryGetEffort(out var effort) ? $"{effort} / 10" : "Move to choose";

    /// <summary>Label of the expand toggle, which flips with the card's state.</summary>
    public string DetailsButtonText => IsExpanded ? "Hide" : "Info";
    /// <summary>Label of the log action, reworded once a log exists so it reads as an edit.</summary>
    public string LogButtonText => IsLogged ? "Update" : "Log";
    /// <summary>Status line under the log action.</summary>
    public string LogStatusText => IsLogged ? "Logged today" : "Not logged yet";

    /// <summary>Why this movement is in the session, with a generic line when the catalog gives no reason.</summary>
    public string WhyText => string.IsNullOrWhiteSpace(Description)
        ? "This movement supports today's workout focus and keeps the session balanced."
        : Description;

    /// <summary>Coaching cues, falling back to the description so the section is never empty.</summary>
    public string CuesText => string.IsNullOrWhiteSpace(TechniqueNotes)
        ? Description
        : TechniqueNotes;

    /// <summary>True when the exercise has quick tips.</summary>
    public bool HasQuickTips => !string.IsNullOrWhiteSpace(QuickTipsText);

    // Gate 75, 76, 77: Unilateral, Superset split, and Initial Calibration UI metadata.
    /// <summary>True when the exercise belongs to a superset.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsInSuperset), nameof(SupersetLabel))] private int? _supersetGroupId;
    public bool IsInSuperset => SupersetGroupId.HasValue;
    /// <summary>Superset badge text, naming the group; empty when the exercise is not in one.</summary>
    public string SupersetLabel => SupersetGroupId is { } groupId
        ? UiTextCatalog.Format("Workout_Session_SupersetLabelFormat", groupId)
        : string.Empty;
    /// <summary>The label explaining the unilateral mode.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(UnilateralModeLabel))] private bool _isUnilateral;
    public string UnilateralModeLabel => IsUnilateral ? UiTextCatalog.Get("Workout_Session_UnilateralPerSide") : string.Empty;
    /// <summary>The prompt shown while the exercise still needs calibrating.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CalibrationPromptText), nameof(HasRecommendationText))] private bool _requiresInitialCalibration;
    public string CalibrationPromptText => RequiresInitialCalibration && !IsLogged
        ? UiTextCatalog.Get("Workout_Session_CalibrationPrompt")
        : string.Empty;
    /// <summary>Split action supplied by the session, which owns the reload that splitting requires.</summary>
    public Func<WorkoutExerciseItem, Task>? SplitSupersetAction { get; set; }

    /// <summary>Splits the superset into separate exercises.</summary>
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

    /// <summary>Adds another set to the exercise.</summary>
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

    /// <summary>Removes a set from the exercise.</summary>
    [RelayCommand]
    public void RemoveSet(ExerciseSetInputItem? set)
    {
        if (set is null || Sets.Count <= 1) return;
        Sets.Remove(set);
        RenumberSets();
        UpdateAggregatesFromSets(fromUser: true);
    }

    /// <summary>Fills the card with editable rows when it has none, seeded from the exercise-level values.</summary>
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

    /// <summary>Re-derives the exercise totals from its rows: set count, average reps and the entered load,</summary>
    /// so the card header and the save payload agree.</summary>
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

    /// <summary>Parses a typed load into kilograms.</summary>
    private static bool TryParseWeight(string? value, out double weight)
    {
        var parsed = ExerciseSetInputItem.ParseWeight(value);
        weight = (double)parsed.GetValueOrDefault();
        return parsed.HasValue;
    }

    /// <summary>Records the slider's value when the person releases it, even at the neutral midpoint.</summary>
    public void CommitEffortSliderValue(double value)
    {
        SetEffort(value, true);
    }

    /// <summary>Clamps and stores an effort rating. A centered slider that was never moved is treated as</summary>
    /// "not answered" rather than a rating of five.</summary>
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

    /// <summary>Reads the stored effort, accepting only the 1 to 10 range.</summary>
    private bool TryGetEffort(out int effort)
    {
        return int.TryParse(LoggedEffortText, NumberStyles.None, CultureInfo.InvariantCulture, out effort) &&
               effort is >= 1 and <= 10;
    }

    /// <summary>Refreshes the layout when the exercise expands.</summary>
    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(DetailsButtonText));
    }

    /// <summary>Refreshes the state when the exercise is logged.</summary>
    partial void OnIsLoggedChanged(bool value)
    {
        OnPropertyChanged(nameof(LogButtonText));
        OnPropertyChanged(nameof(LogStatusText));
    }
}
