
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

    /// <summary>Creates the editable set from a saved set.</summary>
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

    /// <summary>Identity of the logged set being edited.</summary>
    public Guid Id { get; init; }
    /// <summary>Session the set belongs to, needed to address the update.</summary>
    public Guid WorkoutSessionLogId { get; init; }
    /// <summary>Exercise the set belongs to, shown in the group header.</summary>
    public string ExerciseName { get; init; } = string.Empty;
    /// <summary>Set number as originally logged.</summary>
    public int SetNumber { get; init; }
    /// <summary>Warm-up sets count towards neither working volume nor set totals.</summary>
    public bool IsWarmup { get; init; }
    /// <summary>Rest the session recorded for this set, preserved when only load or reps change.</summary>
    public int RestTimeSeconds { get; init; }

    /// <summary>Re-reads the typed load and reps, so a save stores what the fields show.</summary>
    public void SyncFromInputs()
    {
        var parsed = ExerciseSetInputItem.ParseWeightAndReps(WeightInput, RepsInput, WeightKg, RepsCompleted);
        WeightKg = parsed.WeightKg;
        RepsCompleted = parsed.RepsCompleted;
    }
}
