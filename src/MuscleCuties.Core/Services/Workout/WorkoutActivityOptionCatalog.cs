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
                WorkoutActivityType.Strength,
                WorkoutActivityType.ZoneTwoCardio,
                WorkoutActivityType.YogaFlow,
                WorkoutActivityType.Mobility,
                WorkoutActivityType.ActiveRecovery
            }
            : selectedTypes;

        return new ObservableCollection<WorkoutActivityOptionItem>(
        [
            Option(WorkoutActivityType.Strength, "Strength", "Weights, machines, bodyweight", "LIFT", defaults),
            Option(WorkoutActivityType.CardioIntervals, "Intervals", "Short pushes, clean recovery", "CARDIO", defaults),
            Option(WorkoutActivityType.ZoneTwoCardio, "Zone 2 cardio", "Ride, walk, steady engine work", "CARDIO", defaults),
            Option(WorkoutActivityType.RockClimbing, "Rock climbing", "Grip, skill, pulling power", "CLIMB", defaults),
            Option(WorkoutActivityType.YogaFlow, "Yoga flow", "Breath, mobility, light strength", "YOGA", defaults),
            Option(WorkoutActivityType.PowerYoga, "Power yoga", "A little sweat, still bendy", "YOGA", defaults),
            Option(WorkoutActivityType.YinYoga, "Yin yoga", "Slow holds, nervous system downshift", "YOGA", defaults),
            Option(WorkoutActivityType.RestorativeYoga, "Restorative yoga", "Soft reset, deep recovery", "YOGA", defaults),
            Option(WorkoutActivityType.Pilates, "Pilates", "Core control and clean lines", "CORE", defaults),
            Option(WorkoutActivityType.Mobility, "Mobility", "Joints, hips, shoulders", "FLOW", defaults),
            Option(WorkoutActivityType.Walking, "Walking", "Easy movement that still counts", "WALK", defaults),
            Option(WorkoutActivityType.Cycling, "Cycling", "Low-impact engine work", "RIDE", defaults),
            Option(WorkoutActivityType.Swimming, "Swimming", "Smooth cardio, kind on joints", "SWIM", defaults),
            Option(WorkoutActivityType.Dance, "Dance", "Cardio that does not feel like homework", "MOVE", defaults),
            Option(WorkoutActivityType.ActiveRecovery, "Active recovery", "Perform and log lighter sessions", "EASY", defaults)
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
