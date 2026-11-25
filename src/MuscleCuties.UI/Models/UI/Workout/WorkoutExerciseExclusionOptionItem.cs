
namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>One exercise the person can exclude from a generated plan.</summary>
public sealed partial class WorkoutExerciseExclusionOptionItem(int exerciseId, string name) : ObservableObject
{
    /// <summary>The exercise the option excludes.</summary>
    public int ExerciseId { get; } = exerciseId;
    /// <summary>The exercise's name.</summary>
    public string Name { get; } = name;

    [ObservableProperty] private bool _isSelected;

    /// <summary>Toggles the option's selection.</summary>
    [RelayCommand]
    private void Toggle() => IsSelected = !IsSelected;
}

