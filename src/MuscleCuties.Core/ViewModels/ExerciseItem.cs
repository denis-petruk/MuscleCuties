using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.ViewModels;

public class ExerciseItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MuscleGroup PrimaryMuscle { get; set; }
    public int Sets { get; set; }
    public int Reps { get; set; }
    public int? DurationSeconds { get; set; }

    public string SetsRepsLabel => DurationSeconds.HasValue
        ? $"{Sets} sets · {DurationSeconds}s"
        : $"{Sets} sets × {Reps} reps";

    public string MuscleLabel => PrimaryMuscle.ToString();
}
