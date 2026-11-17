using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Workout;
using MuscleCuties.UI.Services.Formatting;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Models.UI.Workout;

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

    public ExerciseSetInputItem()
    {
    }

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

    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorkoutSessionLogId { get; set; }
    public string? ExerciseId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public bool ShowWeight { get; set; } = true;
    public bool ShowPerSideReps { get; set; }
    private int LabelSetNumber => DisplaySetNumber > 0 ? DisplaySetNumber : SetNumber;
    public string SetDisplayText => IsWarmup
        ? UiTextCatalog.Format("Workout_Session_WarmupSetFormat", LabelSetNumber)
        : UiTextCatalog.Format("Workout_Session_WorkingSetFormat", LabelSetNumber);
    private string AutomationPrefix => $"{(IsWarmup ? "Warmup" : "Working")}Set{LabelSetNumber}";
    public string WeightAutomationId => $"{AutomationPrefix}Weight";
    public string RepsAutomationId => $"{AutomationPrefix}Reps";
    public string LeftRepsAutomationId => $"{AutomationPrefix}LeftReps";
    public string RightRepsAutomationId => $"{AutomationPrefix}RightReps";
    public string RirAutomationId => $"{AutomationPrefix}Rir";
    public int EffectiveReps => ShowPerSideReps && LeftSideReps is { } left && RightSideReps is { } right
        ? Math.Min(left, right)
        : RepsCompleted;
    public System.Windows.Input.ICommand? RemoveCommand { get; set; }
    public Action? OnChanged { get; set; }

    private bool _isHydratingInputs;

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

    internal void ClearUnobservedCalibrationWeightInput()
    {
        _isHydratingInputs = true;
        try { WeightInput = string.Empty; }
        finally { _isHydratingInputs = false; }
    }

    internal static (string WeightInput, string RepsInput) FormatWeightAndReps(double weightKg, int repsCompleted)
    {
        return (DisplayFormat.Decimal1(weightKg), repsCompleted.ToString(CultureInfo.CurrentCulture));
    }

    internal static (double WeightKg, int RepsCompleted) ParseWeightAndReps(string weightInput, string repsInput,
        double currentWeightKg, int currentRepsCompleted)
    {
        var weightKg = currentWeightKg;
        var repsCompleted = currentRepsCompleted;

        if (double.TryParse(weightInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedWeight) ||
            double.TryParse(weightInput, NumberStyles.Float, CultureInfo.CurrentCulture, out parsedWeight))
            weightKg = Math.Max(0, Math.Round(parsedWeight, 1));

        if (int.TryParse(repsInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedReps))
            repsCompleted = Math.Max(0, parsedReps);

        return (weightKg, repsCompleted);
    }

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

    [RelayCommand]
    private void ToggleWarmup()
    {
        IsWarmup = !IsWarmup;
        WasEdited = true;
        OnChanged?.Invoke();
    }

    partial void OnWeightInputChanged(string value)
    {
        SyncFromInputs();
    }

    partial void OnRepsInputChanged(string value)
    {
        SyncFromInputs();
    }

    partial void OnRpeInputChanged(string value)
    {
        SyncFromInputs();
    }

    partial void OnRirInputChanged(string value)
    {
        SyncFromInputs();
    }

    partial void OnTechniqueAcceptableChanged(bool value)
    {
        WasEdited = true;
        OnChanged?.Invoke();
    }

    partial void OnIsWarmupChanged(bool value)
    {
        WasEdited = true;
        OnPropertyChanged(nameof(SetDisplayText));
        OnChanged?.Invoke();
    }

    partial void OnSetNumberChanged(int value) => OnPropertyChanged(nameof(SetDisplayText));

    partial void OnDisplaySetNumberChanged(int value)
    {
        OnPropertyChanged(nameof(SetDisplayText));
        OnPropertyChanged(nameof(WeightAutomationId));
        OnPropertyChanged(nameof(RepsAutomationId));
        OnPropertyChanged(nameof(LeftRepsAutomationId));
        OnPropertyChanged(nameof(RightRepsAutomationId));
        OnPropertyChanged(nameof(RirAutomationId));
    }

    partial void OnLeftSideRepsInputChanged(string value) => UpdateSideRepetitions(value, isLeft: true);

    partial void OnRightSideRepsInputChanged(string value) => UpdateSideRepetitions(value, isLeft: false);

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
