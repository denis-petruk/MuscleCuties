using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.Workout.Planning;

public record WeekGenerationInput
{
    public int DaysPerWeek { get; init; }
    public TrainingExperienceLevel Experience { get; init; }
    public bool? isInjuried { get; init; }
    public UserGoal Goal { get; init; }
    public int? SessionMinutesTarget { get; init; }
    public DayOfWeek[]? AvailableDays { get; init; }
    public bool HasRunningOrClimbing { get; init; }
    public CyclePhase Phase { get; init; }
    public HashSet<WorkoutActivityType> SelectedActivities { get; init; } = [];
}

public record PlannedWeek
{
    public int DaysPerWeek { get; init; }
    public required IReadOnlyList<PlannedSession> Sessions { get; init; }
    public required WeekValidationReport Validation { get; init; }
}

public record PlannedSession
{
    public int ArchetypeId { get; init; }
    public string ArchetypeCode { get; init; } = string.Empty;
    public DayOfWeek Day { get; init; }
    public int Variant { get; init; }
    public required List<PlannedSlot> Slots { get; init; }
    public bool IsCardio { get; init; }
    public bool IsRecovery { get; init; }
    public WorkoutActivityType? CardioActivity { get; init; }
}

public record PlannedSlot
{
    public int SlotTemplateId { get; init; }
    public int PrimaryMuscleId { get; init; }
    public int AssignedSets { get; set; }
    public byte SetsMin { get; init; }
    public byte SetsMax { get; init; }
    public byte RepsMin { get; init; }
    public byte RepsMax { get; init; }
    public byte TargetRir { get; init; }
    public bool Droppable { get; init; }
    public byte SupersetGroup { get; init; }
    public BlockType Block { get; init; }
}

public record PickedExercise
{
    public int ExerciseId { get; init; }
    public string ExerciseName { get; init; } = string.Empty;
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

public record WeekValidationReport
{
    public required IReadOnlyList<ValidationEntry> Entries { get; init; }
    public bool HasErrors => Entries.Any(e => e.Severity == ValidationSeverity.Error);
}

public record ValidationEntry(ValidationSeverity Severity, string Message);

public enum ValidationSeverity { Error, Warning }
