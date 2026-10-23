using CommunityToolkit.Mvvm.ComponentModel;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class ExerciseSwapOption : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    public int ExerciseId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string MuscleMatch { get; init; } = string.Empty;
    public string Equipment { get; init; } = string.Empty;
    public string PrimaryMuscles { get; init; } = string.Empty;
}
