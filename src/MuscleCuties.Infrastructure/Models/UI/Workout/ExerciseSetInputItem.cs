using System.Globalization;
using MuscleCuties.Core.Models.Workout;

namespace MuscleCuties.Core.Models.UI.Workout;

public class ExerciseSetInputItem
{
    private bool _isWarmup;
    private string _repsInput = string.Empty;
    private string _rpeInput = string.Empty;
    private string _weightInput = string.Empty;

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
        RpeInput = rpe.HasValue ? rpe.Value.ToString("F1", CultureInfo.InvariantCulture) : string.Empty;
    }

    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorkoutSessionLogId { get; set; }
    public string? ExerciseId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public Action? OnChanged { get; set; }

    public int SetNumber { get; set; }
    public double WeightKg { get; set; }
    public int RepsCompleted { get; set; }
    public double? Rpe { get; set; }

    public bool IsWarmup
    {
        get => _isWarmup;
        set
        {
            _isWarmup = value;
            OnChanged?.Invoke();
        }
    }

    public int RestTimeSeconds { get; set; } = 90;

    public string WeightInput
    {
        get => _weightInput;
        set
        {
            _weightInput = value;
            SyncFromInputs();
        }
    }

    public string RepsInput
    {
        get => _repsInput;
        set
        {
            _repsInput = value;
            SyncFromInputs();
        }
    }

    public string RpeInput
    {
        get => _rpeInput;
        set
        {
            _rpeInput = value;
            SyncFromInputs();
        }
    }

    internal static (string WeightInput, string RepsInput) FormatWeightAndReps(double weightKg, int repsCompleted)
    {
        return (weightKg.ToString("F1", CultureInfo.InvariantCulture),
            repsCompleted.ToString(CultureInfo.InvariantCulture));
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

    public void ToggleWarmup()
    {
        IsWarmup = !IsWarmup;
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
