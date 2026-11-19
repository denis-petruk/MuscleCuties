using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Models.Workout.Planning;

public record PickedExercise
{
    public int ExerciseId { get; init; }
    public string ExerciseName { get; init; } = string.Empty;
    public MovementPattern Pattern { get; init; }
    public int PrimaryMuscleId { get; init; }
    public int Sets { get; init; }
    public byte RepsMin { get; init; }
    public byte RepsMax { get; init; }
    public int Rpe { get; init; }
    public int RestSeconds { get; init; }
    public byte SupersetGroup { get; init; }
    public bool Droppable { get; init; }
    public bool IsBodyweight { get; init; }
    public int SlotTemplateId { get; init; }
}

public record PrescribedSession
{
    public int ArchetypeId { get; init; }
    public string ArchetypeCode { get; init; } = string.Empty;
    public DayOfWeek Day { get; init; }
    public int Variant { get; init; }
    public required List<PickedExercise> Exercises { get; init; }
    public int EstimatedMinutes { get; init; }
}
