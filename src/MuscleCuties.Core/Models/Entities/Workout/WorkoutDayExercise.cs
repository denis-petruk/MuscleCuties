namespace MuscleCuties.Core.Models.Entities.Workout;

public class WorkoutDayExercise
{
    public int Id { get; set; }
    public int WorkoutDayId { get; set; }
    public int ExerciseId { get; set; }
    public int Sets { get; set; }
    public int Reps { get; set; }
    public int? DurationSeconds { get; set; }

    public WorkoutDay? WorkoutDay { get; set; }
    public Exercise? Exercise { get; set; }
}