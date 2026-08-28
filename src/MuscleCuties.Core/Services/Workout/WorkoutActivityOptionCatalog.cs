using System.Collections.ObjectModel;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;

namespace MuscleCuties.Core.Services.Workout;

public static class WorkoutActivityOptionCatalog
{
    public static ObservableCollection<WorkoutActivityOptionItem> Build(
        IReadOnlySet<WorkoutActivityType> selectedTypes)
    {
        var defaults = selectedTypes.Count == 0
            ? new HashSet<WorkoutActivityType>
            {
                WorkoutActivityType.HighVolumeStrength,
                WorkoutActivityType.Cycling,
                WorkoutActivityType.Yoga
            }
            : selectedTypes;

        return new ObservableCollection<WorkoutActivityOptionItem>(
        [
            Option(WorkoutActivityType.StrengthHighIntensity, "Strength high intensity", "Lower volume, heavier work", "STRENGTH", defaults),
            Option(WorkoutActivityType.HighVolumeStrength, "High volume strength", "More sets, muscle-building focus", "STRENGTH", defaults),
            Option(WorkoutActivityType.RockClimbing, "Rock climbing", "Pull strength, grip, skill", "STRENGTH", defaults),
            Option(WorkoutActivityType.Yoga, "Yoga", "Recovery, mobility, calm strength", "RECOVERY", defaults),
            Option(WorkoutActivityType.Hiit, "HIIT", "Short hard conditioning", "CARDIO", defaults),
            Option(WorkoutActivityType.Cycling, "Cycling", "Low-impact conditioning", "CARDIO", defaults),
            Option(WorkoutActivityType.Running, "Running", "Intervals, tempo, steady miles", "CARDIO", defaults),
            Option(WorkoutActivityType.Swimming, "Swimming", "Full-body conditioning", "CARDIO", defaults)
        ]);
    }

    private static WorkoutActivityOptionItem Option(
        WorkoutActivityType activityType,
        string title,
        string subtitle,
        string tag,
        IReadOnlySet<WorkoutActivityType> selectedTypes) =>
        new()
        {
            ActivityType = activityType,
            Title = title,
            Subtitle = subtitle,
            Tag = tag,
            IsSelected = selectedTypes.Contains(activityType)
        };
}
