using System.Collections.ObjectModel;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;

namespace MuscleCuties.Core.Services.Workout;

public static class WorkoutActivityOptionCatalog
{
    private static readonly ActivityDefinition[] Definitions =
    [
        new(
            WorkoutActivityType.HighVolumeStrength,
            "High volume strength",
            "More working sets for glutes, shoulders, back, and steady shape work.",
            ActivityGroup.Strength,
            true,
            "Dumbbell24"),
        new(
            WorkoutActivityType.StrengthHighIntensity,
            "Strength high intensity",
            "Heavier lower-rep work when your phase and readiness can support it.",
            ActivityGroup.Strength,
            false,
            "Flash24"),
        new(
            WorkoutActivityType.RockClimbing,
            "Rock climbing",
            "Pull strength, grip, core tension, and skill treated as strength work.",
            ActivityGroup.Strength,
            false,
            "Sport24"),
        new(
            WorkoutActivityType.Hiit,
            "HIIT",
            "Short hard conditioning used only when selected and phase-ready.",
            ActivityGroup.Cardio,
            false,
            "Fire24"),
        new(
            WorkoutActivityType.Cycling,
            "Cycling",
            "Low-impact cardio with duration, heart rate, watts, and cadence.",
            ActivityGroup.Cardio,
            false,
            "VehicleBicycle24"),
        new(
            WorkoutActivityType.Running,
            "Running",
            "Intervals or steady miles with pace and heart-rate logging.",
            ActivityGroup.Cardio,
            false,
            "Run24"),
        new(
            WorkoutActivityType.Swimming,
            "Swimming",
            "Joint-friendly full-body conditioning with distance and pace per 100 m.",
            ActivityGroup.Cardio,
            false,
            "SwimmingPool24"),
        new(
            WorkoutActivityType.Yoga,
            "Yoga",
            "The recovery baseline for mobility, breath, and longer calm sessions.",
            ActivityGroup.Recovery,
            true,
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
        IEnumerable<WorkoutActivityOptionItem> options) =>
        options
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

        if (item.IsSelected && WorkoutActivityPreferences.IsRecoveryActivity(item.ActivityType))
        {
            var selectedRecoveryCount = options.Count(option =>
                option.IsSelected && WorkoutActivityPreferences.IsRecoveryActivity(option.ActivityType));
            if (selectedRecoveryCount <= 1)
                return "Yoga stays as your recovery fallback when no other recovery option is ready yet.";
        }

        item.IsSelected = !item.IsSelected;
        return string.Empty;
    }

    private static WorkoutActivityOptionItem ToOptionItem(
        ActivityDefinition definition,
        IReadOnlySet<WorkoutActivityType> selectedTypes) =>
        new()
        {
            ActivityType = definition.ActivityType,
            Title = definition.Title,
            Subtitle = definition.Subtitle,
            GroupTitle = GetGroupTitle(definition.Group),
            GroupDescription = GetGroupDescription(definition.Group),
            Tag = GetGroupTag(definition.Group),
            IconGlyph = definition.IconGlyph,
            IsRequired = definition.IsCoreDefault,
            IsSelected = selectedTypes.Contains(definition.ActivityType)
        };

    private static string GetGroupTitle(ActivityGroup group) =>
        group switch
        {
            ActivityGroup.Strength => "Strength",
            ActivityGroup.Cardio => "Cardio",
            ActivityGroup.Recovery => "Recovery",
            _ => string.Empty
        };

    private static string GetGroupDescription(ActivityGroup group) =>
        group switch
        {
            ActivityGroup.Strength => "Required. Choose at least one strength base.",
            ActivityGroup.Cardio => "Optional. If you skip it, the plan uses recovery instead.",
            ActivityGroup.Recovery => "Always keeps a gentle baseline available.",
            _ => string.Empty
        };

    private static string GetGroupTag(ActivityGroup group) =>
        group switch
        {
            ActivityGroup.Strength => "STRENGTH",
            ActivityGroup.Cardio => "CARDIO",
            ActivityGroup.Recovery => "RECOVERY",
            _ => string.Empty
        };

    private static string GetGroupIconGlyph(string tag) =>
        tag switch
        {
            "STRENGTH" => "Dumbbell24",
            "CARDIO" => "PulseSquare24",
            "RECOVERY" => "LeafThree24",
            _ => "Circle24"
        };

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
