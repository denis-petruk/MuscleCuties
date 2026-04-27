namespace MuscleCuties.Core.Models.Entities;

public class WorkoutExercise
{
    public int Id { get; set; }
    public int WorkoutDayId { get; set; }
    public int ExerciseId { get; set; }
    public int Sets { get; set; }
    public int Reps { get; set; }
    public float Weight { get; set; }
    public int OrderIndex { get; set; }

    public WorkoutDay? WorkoutDay { get; set; }
    public Exercise? Exercise { get; set; }
}
