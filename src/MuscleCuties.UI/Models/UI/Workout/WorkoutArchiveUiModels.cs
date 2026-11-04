using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.UseCases.Features.Workout.Archive;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class ExerciseSetsUiGroup : ObservableObject
{
    [ObservableProperty] private string _volumeSummary = string.Empty;

    public ExerciseSetsUiGroup(string exerciseName)
    {
        ExerciseName = exerciseName;
    }

    public string ExerciseName { get; }

    public ObservableCollection<WorkoutExerciseSetDto> Sets { get; } = [];
    public ObservableCollection<EditableExerciseSetItem> EditableSets { get; } = [];

    public void RefreshSummary()
    {
        if (EditableSets.Count > 0)
        {
            var working = EditableSets.Where(s => !s.IsWarmup).ToList();
            var totalVol = working.Sum(s => s.WeightKg * s.RepsCompleted);
            VolumeSummary = $"{working.Count} working sets · {totalVol:F0} kg vol";
        }
        else
        {
            var workingSets = Sets.Where(s => !s.IsWarmup).ToList();
            var totalVol = Sets.Where(s => !s.IsWarmup).Sum(s => s.WeightKg * s.RepsCompleted);
            VolumeSummary = $"{workingSets.Count} working sets · {totalVol:F0} kg vol";
        }
    }
}
