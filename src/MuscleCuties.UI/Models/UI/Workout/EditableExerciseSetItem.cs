using CommunityToolkit.Mvvm.ComponentModel;

namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>
///     Observable model for inline editing of weight and reps for a set in WorkoutHistoryPage.
/// </summary>
public partial class EditableExerciseSetItem : ObservableObject
{
    [ObservableProperty] private bool _isSaved;

    [ObservableProperty] private bool _isSaving;

    [ObservableProperty] private int _repsCompleted;

    [ObservableProperty] private string _repsInput = string.Empty;

    [ObservableProperty] private string _weightInput = string.Empty;

    [ObservableProperty] private double _weightKg;

    public EditableExerciseSetItem(
        Guid id,
        Guid workoutSessionLogId,
        string exerciseName,
        int setNumber,
        double weightKg,
        int repsCompleted,
        bool isWarmup,
        int restTimeSeconds)
    {
        Id = id;
        WorkoutSessionLogId = workoutSessionLogId;
        ExerciseName = exerciseName;
        SetNumber = setNumber;
        WeightKg = weightKg;
        RepsCompleted = repsCompleted;
        IsWarmup = isWarmup;
        RestTimeSeconds = restTimeSeconds;
        var formatted = ExerciseSetInputItem.FormatWeightAndReps(weightKg, repsCompleted);
        WeightInput = formatted.WeightInput;
        RepsInput = formatted.RepsInput;
    }

    public Guid Id { get; init; }
    public Guid WorkoutSessionLogId { get; init; }
    public string ExerciseName { get; init; } = string.Empty;
    public int SetNumber { get; init; }
    public bool IsWarmup { get; init; }
    public int RestTimeSeconds { get; init; }

    public void SyncFromInputs()
    {
        var parsed = ExerciseSetInputItem.ParseWeightAndReps(WeightInput, RepsInput, WeightKg, RepsCompleted);
        WeightKg = parsed.WeightKg;
        RepsCompleted = parsed.RepsCompleted;
    }
}
