
namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>One candidate in the swap sheet, shown with what makes it a fair replacement.</summary>
public partial class ExerciseSwapOption : ObservableObject
{
    /// <summary>The exercise the option swaps in.</summary>
    [ObservableProperty] private bool _isSelected;
    public int ExerciseId { get; init; }
    /// <summary>Candidate movement name shown in the swap sheet.</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>How well the candidate matches the target muscles, as shown on the row.</summary>
    public string MuscleMatch { get; init; } = string.Empty;
    /// <summary>Equipment the candidate needs.</summary>
    public string Equipment { get; init; } = string.Empty;
    /// <summary>Primary muscles the candidate works, as one line of text.</summary>
    public string PrimaryMuscles { get; init; } = string.Empty;
}
