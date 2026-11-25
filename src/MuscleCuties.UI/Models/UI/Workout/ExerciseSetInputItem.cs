using MuscleCuties.Core.Models.Workout;

namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>One editable set row in a session log: what was lifted, how many reps, how hard it felt,</summary>
/// and the text values the entry fields bind to.</summary>
public partial class ExerciseSetInputItem : ObservableObject
{
    [ObservableProperty] private bool _isWarmup;
    [ObservableProperty] private int _repsCompleted;
    [ObservableProperty] private string _repsInput = string.Empty;
    [ObservableProperty] private int? _leftSideReps;
    [ObservableProperty] private string _leftSideRepsInput = string.Empty;
    [ObservableProperty] private int? _rightSideReps;
    [ObservableProperty] private string _rightSideRepsInput = string.Empty;
    [ObservableProperty] private int _restTimeSeconds = 90;
    [ObservableProperty] private double? _rpe;
    [ObservableProperty] private string _rpeInput = string.Empty;
    [ObservableProperty] private double? _rir;
    [ObservableProperty] private string _rirInput = string.Empty;
    [ObservableProperty] private bool _techniqueAcceptable = true;

    [ObservableProperty] private int _setNumber;
    // Per-kind ordinal: the first working set is "Working set 1" even when a warm-up exists.
    [ObservableProperty] private int _displaySetNumber;
    [ObservableProperty] private string _weightInput = string.Empty;
    [ObservableProperty] private double _weightKg;
    [ObservableProperty] private bool _wasEdited;
    [ObservableProperty] private bool _showDivider;
    [ObservableProperty] private bool _canRemove;

    /// <summary>Parameterless form used by the binding layer; every field keeps its default until set.</summary>
    public ExerciseSetInputItem()
    {
    }

    /// <summary>Creates the set input from a saved set.</summary>
    public ExerciseSetInputItem(
        Guid id,
        Guid sessionId,
        string? exerciseId,
        string exerciseName,
        int setNumber,
        double weightKg,
        int repsCompleted,
        double? rpe,
        bool isWarmup,
        int restTimeSeconds,
        int? leftSideReps = null,
        int? rightSideReps = null)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        WorkoutSessionLogId = sessionId;
        ExerciseId = exerciseId;
        ExerciseName = exerciseName;
        SetNumber = setNumber;
        WeightKg = weightKg;
        RepsCompleted = repsCompleted;
        Rpe = rpe;
        Rir = rpe.HasValue ? Math.Clamp(10 - rpe.Value, 0, 10) : null;
        IsWarmup = isWarmup;
        RestTimeSeconds = restTimeSeconds;
        _leftSideReps = leftSideReps;
        _rightSideReps = rightSideReps;
        var formatted = FormatWeightAndReps(weightKg, repsCompleted);
        WeightInput = formatted.WeightInput;
        RepsInput = formatted.RepsInput;
        _leftSideRepsInput = leftSideReps?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        _rightSideRepsInput = rightSideReps?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        RpeInput = rpe.HasValue ? DisplayFormat.Decimal1(rpe.Value) : string.Empty;
        RirInput = Rir.HasValue ? DisplayFormat.Decimal1(Rir.Value) : string.Empty;
        WasEdited = false;
    }

    /// <summary>Identity of the set, used to update the same row instead of appending a duplicate.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    /// <summary>Session the set belongs to; empty until the session log exists (preview rows).</summary>
    public Guid WorkoutSessionLogId { get; set; }
    /// <summary>Catalog exercise id carried as text so an unsaved catalog entry can still be logged.</summary>
    public string? ExerciseId { get; set; }
    /// <summary>Exercise name stored with the set, so a log stays readable if the catalog changes.</summary>
    public string ExerciseName { get; set; } = string.Empty;
    /// <summary>False for bodyweight movements, which hide the load column instead of asking for 0 kg.</summary>
    public bool ShowWeight { get; set; } = true;
    /// <summary>True for unilateral movements, which record each side separately instead of one total.</summary>
    public bool ShowPerSideReps { get; set; }
    /// <summary>The set arrived from the server rather than being typed on this screen.</summary>
    public bool IsObserved { get; init; }
    /// <summary>The load currently typed, or null when the field is empty or unparsable.</summary>
    public double? EnteredWeightKg => ParseWeight(WeightInput) is { } parsed ? (double)parsed : null;
    /// <summary>True once reps are recorded, on both sides for unilateral movements.</summary>
    public bool HasEnteredReps => ShowPerSideReps
        ? (LeftSideReps is > 0 && RightSideReps is > 0)
        : int.TryParse(RepsInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var reps) && reps > 0;
    /// <summary>A working set counts as captured when it was touched, came from the server, or holds any</summary>
    /// value; an untouched blank row is not submitted.</summary>
    public bool IsCapturedWorkingSet => !IsWarmup && (WasEdited || IsObserved || HasEnteredReps || EnteredWeightKg.HasValue);
    /// <summary>Number shown on the row, preferring the per-kind ordinal over the absolute set number.</summary>
    private int LabelSetNumber => DisplaySetNumber > 0 ? DisplaySetNumber : SetNumber;
    /// <summary>Row heading: warm-up or working set, with its per-kind number.</summary>
    public string SetDisplayText => IsWarmup
        ? UiTextCatalog.Format("Workout_Session_WarmupSetFormat", LabelSetNumber)
        : UiTextCatalog.Format("Workout_Session_WorkingSetFormat", LabelSetNumber);
    /// <summary>Shared prefix for the row's automation ids, so a warm-up and a working set never collide.</summary>
    private string AutomationPrefix => $"{(IsWarmup ? "Warmup" : "Working")}Set{LabelSetNumber}";
    /// <summary>Automation id of the load field.</summary>
    public string WeightAutomationId => $"{AutomationPrefix}Weight";
    /// <summary>Automation id of the reps field for a bilateral set.</summary>
    public string RepsAutomationId => $"{AutomationPrefix}Reps";
    /// <summary>Automation id of the left-side reps field.</summary>
    public string LeftRepsAutomationId => $"{AutomationPrefix}LeftReps";
    /// <summary>Automation id of the right-side reps field.</summary>
    public string RightRepsAutomationId => $"{AutomationPrefix}RightReps";
    /// <summary>Automation id of the reps-in-reserve field.</summary>
    public string RirAutomationId => $"{AutomationPrefix}Rir";
    /// <summary>Reps a unilateral set is credited with: the weaker side, because that is what limited it.</summary>
    public int EffectiveReps => ShowPerSideReps && LeftSideReps is { } left && RightSideReps is { } right
        ? Math.Min(left, right)
        : RepsCompleted;
    /// <summary>Bound by the owning exercise so the row's delete button can remove itself.</summary>
    public System.Windows.Input.ICommand? RemoveCommand { get; set; }
    /// <summary>Raised after any edit, so the exercise can re-derive its totals from the rows.</summary>
    public Action? OnChanged { get; set; }

    private bool _isHydratingInputs;

    /// <summary>Fills both side fields without marking the set edited or triggering a save, for values that</summary>
    /// come from the server.</summary>
    internal void HydrateSideRepetitions(int? left, int? right)
    {
        _isHydratingInputs = true;
        try
        {
            LeftSideReps = left;
            RightSideReps = right;
            LeftSideRepsInput = left?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            RightSideRepsInput = right?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            OnPropertyChanged(nameof(EffectiveReps));
        }
        finally { _isHydratingInputs = false; }
    }

    /// <summary>Clears a prefilled calibration load that the person never actually entered, so it is not</summary>
    /// saved as their starting weight.</summary>
    internal void ClearUnobservedCalibrationWeightInput()
    {
        _isHydratingInputs = true;
        try { WeightInput = string.Empty; }
        finally { _isHydratingInputs = false; }
    }

    /// <summary>Renders a load and rep count into the two field texts, in the app's number format.</summary>
    internal static (string WeightInput, string RepsInput) FormatWeightAndReps(double weightKg, int repsCompleted)
    {
        return (DisplayFormat.Decimal1(weightKg), repsCompleted.ToString(CultureInfo.CurrentCulture));
    }

    /// <summary>Reads the entered weight and reps, keeping the current values when unreadable.</summary>
    internal static (double WeightKg, int RepsCompleted) ParseWeightAndReps(string weightInput, string repsInput,
        double currentWeightKg, int currentRepsCompleted)
    {
        var weightKg = currentWeightKg;
        var repsCompleted = currentRepsCompleted;

        if (ParseWeight(weightInput) is { } parsedWeight)
            weightKg = (double)parsedWeight;

        if (int.TryParse(repsInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedReps))
            repsCompleted = Math.Max(0, parsedReps);

        return (weightKg, repsCompleted);
    }

    /// <summary>Parses a typed load, accepting a comma as the decimal separator and returning null for</summary>
    /// anything unparsable.</summary>
    internal static decimal? ParseWeight(string? value)
    {
        if (value is null) return null;
        var normalized = value.Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var kg)
            ? kg
            : null;
    }

    /// <summary>Re-derives the stored values from the typed text: load, reps, RPE and RIR, each clamped to</summary>
    /// its valid range, then notifies the owner.</summary>
    public void SyncFromInputs()
    {
        if (_isHydratingInputs) return;
        WasEdited = true;
        var parsed = ParseWeightAndReps(WeightInput, RepsInput, WeightKg, RepsCompleted);
        WeightKg = parsed.WeightKg;
        RepsCompleted = parsed.RepsCompleted;

        if (double.TryParse(RpeInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedRpe) ||
            double.TryParse(RpeInput, NumberStyles.Float, CultureInfo.CurrentCulture, out parsedRpe))
            Rpe = Math.Clamp(Math.Round(parsedRpe, 1), 1.0, 10.0);
        else if (string.IsNullOrWhiteSpace(RpeInput)) Rpe = null;

        if (double.TryParse(RirInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedRir) ||
            double.TryParse(RirInput, NumberStyles.Float, CultureInfo.CurrentCulture, out parsedRir))
            Rir = Math.Clamp(Math.Round(parsedRir, 1), 0.0, 10.0);
        else if (string.IsNullOrWhiteSpace(RirInput)) Rir = null;

        OnChanged?.Invoke();
    }

    /// <summary>Re-reads the weight when the input changes.</summary>
    partial void OnWeightInputChanged(string value)
    {
        SyncFromInputs();
    }

    /// <summary>Re-reads the reps when the input changes.</summary>
    partial void OnRepsInputChanged(string value)
    {
        SyncFromInputs();
    }

    /// <summary>Re-reads the effort rating when the input changes.</summary>
    partial void OnRpeInputChanged(string value)
    {
        SyncFromInputs();
    }

    /// <summary>Re-reads the reps in reserve when the input changes.</summary>
    partial void OnRirInputChanged(string value)
    {
        SyncFromInputs();
    }

    /// <summary>Refreshes the completion state when technique changes.</summary>
    partial void OnTechniqueAcceptableChanged(bool value)
    {
        WasEdited = true;
        OnChanged?.Invoke();
    }

    /// <summary>Refreshes the completion state when the set becomes a warmup.</summary>
    partial void OnIsWarmupChanged(bool value)
    {
        WasEdited = true;
        OnPropertyChanged(nameof(SetDisplayText));
        OnChanged?.Invoke();
    }

    /// <summary>Refreshes the set label when its number changes.</summary>
    partial void OnSetNumberChanged(int value) => OnPropertyChanged(nameof(SetDisplayText));

    /// <summary>Refreshes the set label when the displayed number changes.</summary>
    partial void OnDisplaySetNumberChanged(int value)
    {
        OnPropertyChanged(nameof(SetDisplayText));
        OnPropertyChanged(nameof(WeightAutomationId));
        OnPropertyChanged(nameof(RepsAutomationId));
        OnPropertyChanged(nameof(LeftRepsAutomationId));
        OnPropertyChanged(nameof(RightRepsAutomationId));
        OnPropertyChanged(nameof(RirAutomationId));
    }

    /// <summary>Reads the left side's reps into the set.</summary>
    partial void OnLeftSideRepsInputChanged(string value) => UpdateSideRepetitions(value, isLeft: true);

    /// <summary>Reads the right side's reps into the set.</summary>
    partial void OnRightSideRepsInputChanged(string value) => UpdateSideRepetitions(value, isLeft: false);

    /// <summary>Applies one side's typed reps, marks the set edited, and notifies the owner.</summary>
    private void UpdateSideRepetitions(string value, bool isLeft)
    {
        if (_isHydratingInputs) return;
        var parsed = int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var reps) && reps > 0
            ? reps
            : (int?)null;
        if (isLeft) LeftSideReps = parsed;
        else RightSideReps = parsed;
        WasEdited = true;
        OnPropertyChanged(nameof(EffectiveReps));
        OnChanged?.Invoke();
    }

    /// <summary>Builds the persisted form of the row, converting RIR back to RPE because the server stores</summary>
    /// effort that way.</summary>
    public ExerciseSetLogDto ToDto(Guid sessionId)
    {
        SyncFromInputs();
        return new ExerciseSetLogDto(
            Id,
            sessionId,
            ExerciseId,
            ExerciseName,
            SetNumber,
            WeightKg,
            RepsCompleted,
            Rir.HasValue ? 10 - Rir.Value : null,
            IsWarmup,
            RestTimeSeconds
        );
    }
}
