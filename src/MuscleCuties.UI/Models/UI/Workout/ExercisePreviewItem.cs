namespace MuscleCuties.UI.Models.UI.Workout;

public sealed class ExercisePreviewItem
{
    public string Name { get; set; } = string.Empty;
    public string SetsReps { get; set; } = string.Empty;
    public string MuscleGroups { get; set; } = string.Empty;
    public string Tempo { get; set; } = string.Empty;
    public string MovementNotes { get; set; } = string.Empty;
    public string WarmUpNote { get; set; } = string.Empty;
    public bool IsWarmUp { get; set; }
    public bool HasGuidance { get; set; }
    public string UnavailableGuidanceText { get; set; } = string.Empty;
}
