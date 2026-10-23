namespace MuscleCuties.Core.Models.UI.Workout;

public sealed class ExerciseSwapOption
{
    public int ExerciseId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string MuscleMatch { get; init; } = string.Empty;
    public string Equipment { get; init; } = string.Empty;
    public string PrimaryMuscles { get; init; } = string.Empty;
    public bool IsSelected { get; set; }
}
