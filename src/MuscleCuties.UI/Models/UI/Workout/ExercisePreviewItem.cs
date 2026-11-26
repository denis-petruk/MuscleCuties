namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>A movement in the pre-session preview: what is prescribed and how to perform it.</summary>
public sealed class ExercisePreviewItem
{
    /// <summary>Movement name shown in the preview header.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Prescribed sets and reps, already formatted.</summary>
    public string SetsReps { get; set; } = string.Empty;
    /// <summary>Muscles the movement targets, as one line of text.</summary>
    public string MuscleGroups { get; set; } = string.Empty;
    /// <summary>Prescribed lifting tempo, when the plan specifies one.</summary>
    public string Tempo { get; set; } = string.Empty;
    /// <summary>Coaching notes for the movement.</summary>
    public string MovementNotes { get; set; } = string.Empty;
    /// <summary>Warm-up instruction shown before the working sets.</summary>
    public string WarmUpNote { get; set; } = string.Empty;
    /// <summary>Explains why guidance is missing, instead of leaving an empty expander.</summary>
    public string UnavailableGuidanceText { get; set; } = string.Empty;
}
