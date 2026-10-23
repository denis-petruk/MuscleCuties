using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Workout;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class ExerciseSetInputItem : ObservableObject
{
    [ObservableProperty] private bool _isWarmup;
    [ObservableProperty] private int _repsCompleted;
    [ObservableProperty] private string _repsInput = string.Empty;
    [ObservableProperty] private int _restTimeSeconds = 90;
    [ObservableProperty] private double? _rpe;
    [ObservableProperty] private string _rpeInput = string.Empty;

    [ObservableProperty] private int _setNumber;
    [ObservableProperty] private string _weightInput = string.Empty;
    [ObservableProperty] private double _weightKg;

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
        int restTimeSeconds)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        WorkoutSessionLogId = sessionId;
        ExerciseId = exerciseId;
        ExerciseName = exerciseName;
        SetNumber = setNumber;
        WeightKg = weightKg;
        RepsCompleted = repsCompleted;
        Rpe = rpe;
        IsWarmup = isWarmup;
        RestTimeSeconds = restTimeSeconds;
        var formatted = FormatWeightAndReps(weightKg, repsCompleted);
        WeightInput = formatted.WeightInput;
        RepsInput = formatted.RepsInput;
        RpeInput = rpe.HasValue ? rpe.Value.ToString("F1", CultureInfo.CurrentCulture) : string.Empty;
    }

    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorkoutSessionLogId { get; set; }
    public string? ExerciseId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public Action? OnChanged { get; set; }

    internal static (string WeightInput, string RepsInput) FormatWeightAndReps(double weightKg, int repsCompleted)
    {
        return (weightKg.ToString("F1", CultureInfo.CurrentCulture),
            repsCompleted.ToString(CultureInfo.CurrentCulture));
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
        var parsed = ParseWeightAndReps(WeightInput, RepsInput, WeightKg, RepsCompleted);
        WeightKg = parsed.WeightKg;
        RepsCompleted = parsed.RepsCompleted;

        if (double.TryParse(RpeInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedRpe) ||
            double.TryParse(RpeInput, NumberStyles.Float, CultureInfo.CurrentCulture, out parsedRpe))
            Rpe = Math.Clamp(Math.Round(parsedRpe, 1), 1.0, 10.0);
        else if (string.IsNullOrWhiteSpace(RpeInput)) Rpe = null;

        OnChanged?.Invoke();
    }

    [RelayCommand]
    private void ToggleWarmup()
    {
        IsWarmup = !IsWarmup;
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

    partial void OnIsWarmupChanged(bool value)
    {
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
            Rpe,
            IsWarmup,
            RestTimeSeconds
        );
    }
}
