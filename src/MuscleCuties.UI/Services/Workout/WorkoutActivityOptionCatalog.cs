using System.Collections.ObjectModel;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.Services.Workout;

public static class WorkoutActivityOptionCatalog
{
    private static readonly ActivityDefinition[] Definitions =
    [
        new(
            WorkoutActivityDto.HighVolumeStrength,
            "High volume strength",
            "Strength training with activity-specific set, repetition, and rest targets.",
            ActivityGroup.Strength,
            true,
            "Dumbbell24"),
        new(
            WorkoutActivityDto.StrengthHighIntensity,
            "Strength high intensity",
            "Strength training with heavier main movements and planned rest between sets.",
            ActivityGroup.Strength,
            false,
            "Flash24"),
        new(
            WorkoutActivityDto.RockClimbing,
            "Rock climbing",
            "Plan or log climbing time; demand depends on the selected session.",
            ActivityGroup.Climbing,
            false,
            "Sport24"),
        new(
            WorkoutActivityDto.Hiit,
            "HIIT",
            "HIIT means high-intensity interval training: short hard efforts with recovery breaks.",
            ActivityGroup.Cardio,
            false,
            "Fire24"),
        new(
            WorkoutActivityDto.Cycling,
            "Cycling",
            "Ride for low-impact cardio.",
            ActivityGroup.Cardio,
            false,
            "VehicleBicycle24"),
        new(
            WorkoutActivityDto.Running,
            "Running",
            "Run steadily or mix in faster intervals.",
            ActivityGroup.Cardio,
            false,
            "Run24"),
        new(
            WorkoutActivityDto.Swimming,
            "Swimming",
            "Swim for full-body movement and endurance.",
            ActivityGroup.Cardio,
            false,
            "SwimmingPool24"),
        new(
            WorkoutActivityDto.Yoga,
            "Yoga",
            "Choose gentle or demanding yoga content when planning a session.",
            ActivityGroup.Yoga,
            false,
            "LeafThree24"),
        new(WorkoutActivityDto.GeneralStrength, "General strength training",
            "Strength sessions with targets matched to your activity history.", ActivityGroup.Strength, false, "Dumbbell24"),
        new(WorkoutActivityDto.Walking, "Walking",
            "Plan or log walking time.", ActivityGroup.Cardio, false, "Run24"),
        new(WorkoutActivityDto.Rowing, "Rowing",
            "Plan or log rowing time.", ActivityGroup.Cardio, false, "PulseSquare24"),
        new(WorkoutActivityDto.Elliptical, "Elliptical",
            "Plan or log elliptical time.", ActivityGroup.Cardio, false, "PulseSquare24"),
        new(WorkoutActivityDto.Mobility, "Mobility",
            "Plan or log mobility work.", ActivityGroup.Recovery, false, "LeafThree24"),
        new(WorkoutActivityDto.GentleRecovery, "Gentle recovery activities",
            "Plan or log gentle recovery time.", ActivityGroup.Recovery, false, "LeafThree24"),
        new(WorkoutActivityDto.RestDay, "Rest day",
            "Keep a day free of planned exercise.", ActivityGroup.Rest, false, "WeatherMoon24")
    ];

    public static ObservableCollection<WorkoutActivityOptionItem> Build(
        IReadOnlySet<WorkoutActivityDto> selectedTypes)
    {
        var selected = selectedTypes.Count == 0
            ? WorkoutPreferencesConstraints.BuildDefaultActivitySelection()
            : selectedTypes;
        var defaults = WorkoutPreferencesConstraints.EnsureRequiredActivities(selected);

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
        var optionsList = options as IReadOnlyList<WorkoutActivityOptionItem> ?? options.ToList();
        if (item.IsSelected)
        {
            var selectedList = optionsList.Where(option => option.IsSelected).ToList();
            if (selectedList.Count == 1)
                return "Choose at least one activity.";

            if (WorkoutPlanningConstants.IsStrengthActivity(item.ActivityType) &&
                selectedList.Count(option => WorkoutPlanningConstants.IsStrengthActivity(option.ActivityType)) == 1)
            {
                return "At least one strength activity is required for your plan.";
            }
        }

        item.IsSelected = !item.IsSelected;
        return string.Empty;
    }

    private static WorkoutActivityOptionItem ToOptionItem(
        ActivityDefinition definition,
        IReadOnlySet<WorkoutActivityDto> selectedTypes)
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
        WorkoutActivityDto ActivityType,
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
        Rest
    }
}
