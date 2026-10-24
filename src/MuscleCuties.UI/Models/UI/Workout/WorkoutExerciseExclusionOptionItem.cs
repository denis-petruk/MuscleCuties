using CommunityToolkit.Mvvm.ComponentModel;

namespace MuscleCuties.UI.Models.UI.Workout;

public sealed partial class WorkoutExerciseExclusionOptionItem(int exerciseId, string name) : ObservableObject
{
    public int ExerciseId { get; } = exerciseId;
    public string Name { get; } = name;

    [ObservableProperty] private bool _isSelected;
}
