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
            "Strength training with activity-specific set, repetition, and rest targets.",
            ActivityGroup.Strength,
            true,
            "Dumbbell24"),
        new(
            WorkoutActivityType.StrengthHighIntensity,
            "Strength high intensity",
            "Strength training with heavier main movements and planned rest between sets.",
            ActivityGroup.Strength,
            false,
            "Flash24"),
        new(
            WorkoutActivityType.RockClimbing,
            "Rock climbing",
            "Plan or log climbing time; demand depends on the selected session.",
            ActivityGroup.Climbing,
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
            "Choose gentle or demanding yoga content when planning a session.",
            ActivityGroup.Yoga,
            false,
            "LeafThree24"),
        new(WorkoutActivityType.GeneralStrength, "General strength training",
            "Strength sessions with targets matched to your activity history.", ActivityGroup.Strength, false, "Dumbbell24"),
        new(WorkoutActivityType.Walking, "Walking",
            "Plan or log walking time.", ActivityGroup.Cardio, false, "Run24"),
        new(WorkoutActivityType.Rowing, "Rowing",
            "Plan or log rowing time.", ActivityGroup.Cardio, false, "PulseSquare24"),
        new(WorkoutActivityType.Elliptical, "Elliptical",
            "Plan or log elliptical time.", ActivityGroup.Cardio, false, "PulseSquare24"),
        new(WorkoutActivityType.Mobility, "Mobility",
            "Plan or log mobility work.", ActivityGroup.Recovery, false, "LeafThree24"),
        new(WorkoutActivityType.GentleRecovery, "Gentle recovery activities",
            "Plan or log gentle recovery time.", ActivityGroup.Recovery, false, "LeafThree24"),
        new(WorkoutActivityType.RestDay, "Rest day",
            "Keep a day free of planned exercise.", ActivityGroup.Rest, false, "WeatherMoon24"),
        new(WorkoutActivityType.Other, "Other activity",
            "Record another activity without assigning it a prescription.", ActivityGroup.Other, false, "CircleSmall24")
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
        if (item.IsSelected && options.Count(option => option.IsSelected) == 1)
            return "Choose at least one activity.";

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
            ActivityGroup.Climbing => "Climbing",
            ActivityGroup.Yoga => "Yoga",
            ActivityGroup.Rest => "Rest",
            ActivityGroup.Other => "Other",
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
            ActivityGroup.Climbing => "CLIMBING",
            ActivityGroup.Yoga => "YOGA",
            ActivityGroup.Rest => "REST",
            ActivityGroup.Other => "OTHER",
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
            "CLIMBING" => "Sport24",
            "YOGA" => "LeafThree24",
            "REST" => "WeatherMoon24",
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
        Recovery,
        Climbing,
        Yoga,
        Rest,
        Other
    }
}
