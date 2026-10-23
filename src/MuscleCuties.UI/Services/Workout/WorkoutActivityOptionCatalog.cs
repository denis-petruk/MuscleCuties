using System.Collections.ObjectModel;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.UI.Models.UI.Workout;

namespace MuscleCuties.UI.Services.Workout;

public static class WorkoutActivityOptionCatalog
{
    private static readonly ActivityDefinition[] Definitions =
    [
        new(
            WorkoutActivityType.HighVolumeStrength,
            "High volume strength",
            "Build strength with a few more sets.",
            ActivityGroup.Strength,
            true,
            "Dumbbell24"),
        new(
            WorkoutActivityType.StrengthHighIntensity,
            "Strength high intensity",
            "Build strength with heavier lifts and fewer reps.",
            ActivityGroup.Strength,
            false,
            "Flash24"),
        new(
            WorkoutActivityType.RockClimbing,
            "Rock climbing",
            "Climb for grip, core, and upper-body strength.",
            ActivityGroup.Strength,
            false,
            "Sport24"),
        new(
            WorkoutActivityType.Hiit,
            "HIIT",
            "HIIT means high-intensity interval training: short hard efforts with recovery breaks.",
            ActivityGroup.Cardio,
            false,
            "Fire24"),
        new(
            WorkoutActivityType.Cycling,
            "Cycling",
            "Ride for low-impact cardio.",
            ActivityGroup.Cardio,
            false,
            "VehicleBicycle24"),
        new(
            WorkoutActivityType.Running,
            "Running",
            "Run steadily or mix in faster intervals.",
            ActivityGroup.Cardio,
            false,
            "Run24"),
        new(
            WorkoutActivityType.Swimming,
            "Swimming",
            "Swim for full-body movement and endurance.",
            ActivityGroup.Cardio,
            false,
            "SwimmingPool24"),
        new(
            WorkoutActivityType.Yoga,
            "Yoga",
            "Move, stretch, and breathe at your own pace.",
            ActivityGroup.Recovery,
            false,
            "LeafThree24")
    ];

    public static ObservableCollection<WorkoutActivityOptionItem> Build(
        IReadOnlySet<WorkoutActivityType> selectedTypes)
    {
        var defaults = WorkoutActivityPreferences.EnsureRequired(
            selectedTypes.Count == 0
                ? WorkoutActivityPreferences.BuildDefaultSelection()
                : selectedTypes);

        return new ObservableCollection<WorkoutActivityOptionItem>(
            Definitions.Select(definition => ToOptionItem(definition, defaults)));
    }

    public static IReadOnlyList<WorkoutActivityGroupSection> BuildGroups(
        IEnumerable<WorkoutActivityOptionItem> options)
    {
        return options
            .GroupBy(option => option.Tag)
            .Select(group =>
            {
                var first = group.First();
                return new WorkoutActivityGroupSection(
                    first.GroupTitle,
                    first.GroupDescription,
                    GetGroupIconGlyph(first.Tag),
                    group.ToList());
            })
            .ToList();
    }

    public static string ToggleSelection(
        IEnumerable<WorkoutActivityOptionItem> options,
        WorkoutActivityOptionItem item)
    {
        if (item.IsSelected && WorkoutActivityPreferences.IsStrengthActivity(item.ActivityType))
        {
            var selectedStrengthCount = options.Count(option =>
                option.IsSelected && WorkoutActivityPreferences.IsStrengthActivity(option.ActivityType));
            if (selectedStrengthCount <= 1)
                return "Keep one strength style selected so your plan has a strong base.";
        }

        item.IsSelected = !item.IsSelected;
        return string.Empty;
    }

    private static WorkoutActivityOptionItem ToOptionItem(
        ActivityDefinition definition,
        IReadOnlySet<WorkoutActivityType> selectedTypes)
    {
        return new WorkoutActivityOptionItem
        {
            ActivityType = definition.ActivityType,
            Title = definition.Title,
            Subtitle = definition.Subtitle,
            GroupTitle = GetGroupTitle(definition.Group),
            GroupDescription = string.Empty,
            Tag = GetGroupTag(definition.Group),
            IconGlyph = definition.IconGlyph,
            IsRequired = definition.IsCoreDefault,
            IsSelected = selectedTypes.Contains(definition.ActivityType)
        };
    }

    private static string GetGroupTitle(ActivityGroup group)
    {
        return group switch
        {
            ActivityGroup.Strength => "Strength",
            ActivityGroup.Cardio => "Cardio",
            ActivityGroup.Recovery => "Recovery",
            _ => string.Empty
        };
    }

    private static string GetGroupTag(ActivityGroup group)
    {
        return group switch
        {
            ActivityGroup.Strength => "STRENGTH",
            ActivityGroup.Cardio => "CARDIO",
            ActivityGroup.Recovery => "RECOVERY",
            _ => string.Empty
        };
    }

    private static string GetGroupIconGlyph(string tag)
    {
        return tag switch
        {
            "STRENGTH" => "Trophy24",
            "CARDIO" => "PulseSquare24",
            "RECOVERY" => "WeatherMoon24",
            _ => "CircleSmall24"
        };
    }

    private sealed record ActivityDefinition(
        WorkoutActivityType ActivityType,
        string Title,
        string Subtitle,
        ActivityGroup Group,
        bool IsCoreDefault,
        string IconGlyph);

    private enum ActivityGroup
    {
        Strength,
        Cardio,
        Recovery
    }
}
