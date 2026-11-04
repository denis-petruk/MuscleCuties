using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MuscleCuties.UI.Models.UI.Workout;

public sealed partial class WorkoutExerciseExclusionOptionItem(int exerciseId, string name) : ObservableObject
{
    public int ExerciseId { get; } = exerciseId;
    public string Name { get; } = name;

    [ObservableProperty] private bool _isSelected;

    [RelayCommand]
    private void Toggle() => IsSelected = !IsSelected;
}

