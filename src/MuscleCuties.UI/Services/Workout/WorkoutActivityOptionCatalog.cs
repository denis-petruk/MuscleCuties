using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.Services.Workout;

/// <summary>The activity list the workout-preferences screen offers, and the selection rules that keep a</summary>
/// plan plausible: at least one activity, and at least one of them a strength activity.</summary>
public static class WorkoutActivityOptionCatalog
{
    private static readonly ActivityDefinition[] Definitions =
    [
        new(
            WorkoutActivityDto.HighVolumeStrength,
            "High volume strength",
            "More sets for steady strength progress.",
            ActivityGroup.Strength,
            true,
            "Dumbbell24",
            "activity_strength_volume.png"),
        new(
            WorkoutActivityDto.StrengthHighIntensity,
            "Strength high intensity",
            "Heavier main lifts with fewer reps.",
            ActivityGroup.Strength,
            false,
            "Flash24",
            "activity_strength_intensity.png"),
        new(
            WorkoutActivityDto.RockClimbing,
            "Rock climbing",
            "Plan or log climbing time; demand depends on the selected session.",
            ActivityGroup.Climbing,
            false,
            "Sport24",
            "activity_climbing.png"),
        new(
            WorkoutActivityDto.Hiit,
            "HIIT",
            "HIIT means high-intensity interval training: short hard efforts with recovery breaks.",
            ActivityGroup.Cardio,
            false,
            "Fire24",
            "activity_hiit.png"),
        new(
            WorkoutActivityDto.Cycling,
            "Cycling",
            "Ride for low-impact cardio.",
            ActivityGroup.Cardio,
            false,
            "VehicleBicycle24",
            "activity_cycling.png"),
        new(
            WorkoutActivityDto.Running,
            "Running",
            "Run steadily or mix in faster intervals.",
            ActivityGroup.Cardio,
            false,
            "Run24",
            "activity_running.png"),
        new(
            WorkoutActivityDto.Swimming,
            "Swimming",
            "Swim for full-body movement and endurance.",
            ActivityGroup.Cardio,
            false,
            "SwimmingPool24",
            "activity_swimming.png"),
        new(
            WorkoutActivityDto.Yoga,
            "Yoga",
            "Choose gentle or demanding yoga content when planning a session.",
            ActivityGroup.Yoga,
            false,
            "LeafThree24",
            "activity_yoga.svg"),
        new(WorkoutActivityDto.GeneralStrength, "General strength training",
            "Strength sessions with targets matched to your activity history.", ActivityGroup.Strength, false, "Dumbbell24",
            "activity_strength_general.png"),
        new(WorkoutActivityDto.Walking, "Walking",
            "Steady low-impact walking for daily movement and aerobic base.", ActivityGroup.Cardio, false, "Run24",
            "activity_walking.png"),
        new(WorkoutActivityDto.Rowing, "Rowing",
            "Full-body rowing for aerobic conditioning and back endurance.", ActivityGroup.Cardio, false, "PulseSquare24",
            "activity_rowing.svg"),
        new(WorkoutActivityDto.Elliptical, "Elliptical",
            "Low-impact cardio that protects joints during steady sessions.", ActivityGroup.Cardio, false, "PulseSquare24",
            "activity_elliptical.svg"),
        new(WorkoutActivityDto.Mobility, "Mobility",
            "Guided joint range-of-motion and movement-quality work.", ActivityGroup.Recovery, false, "LeafThree24",
            "activity_mobility.svg"),
        new(WorkoutActivityDto.GentleRecovery, "Gentle recovery activities",
            "Easy restorative movement to support recovery between harder days.", ActivityGroup.Recovery, false, "LeafThree24",
            "activity_gentle_recovery.svg"),
        new(WorkoutActivityDto.RestDay, "Rest day",
            "Keep a day free of planned exercise.", ActivityGroup.Rest, false, "WeatherMoon24",
            "activity_rest_day.png")
    ];

    /// <summary>Builds the option list for the current selection, adding the required activities when the saved</summary>
    /// set is empty so the screen never opens with nothing chosen.</summary>
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

    /// <summary>Groups the options under their headings, in catalog order.</summary>
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

    /// <summary>Applies a tap, or refuses it with the reason: the last activity and the last strength activity</summary>
    /// cannot be deselected, and an unavailable option cannot be newly selected.</summary>
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

        else if (!item.IsOffered)
        {
            // Not offered as a new selection; the card shows the reason and the alternative.
            return string.Empty;
        }

        item.IsSelected = !item.IsSelected;
        return string.Empty;
    }

    /// <summary>The option item for a catalog activity.</summary>
    private static WorkoutActivityOptionItem ToOptionItem(
        ActivityDefinition definition,
        IReadOnlySet<WorkoutActivityDto> selectedTypes)
    {
        return new WorkoutActivityOptionItem
        {
            ActivityType = definition.ActivityType,
            // Activity copy is authored in English; the runtime catalog translates it on a Russian
            // device so a card never mixes languages with the rest of the screen.
            Title = UiTextCatalog.TranslateRuntimeText(definition.Title),
            Subtitle = UiTextCatalog.TranslateRuntimeText(definition.Subtitle),
            UnavailableReason = definition.ActivityType == WorkoutActivityDto.Hiit &&
                                !selectedTypes.Contains(WorkoutActivityDto.Hiit)
                ? UiTextCatalog.Get("Profile_WorkoutPrefs_HiitNotAvailable")
                : string.Empty,
            GroupTitle = UiTextCatalog.TranslateRuntimeText(GetGroupTitle(definition.Group)),
            GroupDescription = string.Empty,
            Tag = GetGroupTag(definition.Group),
            IconGlyph = definition.IconGlyph,
            IconImage = definition.IconImage,
            IsRequired = definition.IsCoreDefault,
            IsSelected = selectedTypes.Contains(definition.ActivityType)
        };
    }

    /// <summary>English group heading, translated at render time on a Russian device.</summary>
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

    /// <summary>Stable tag the visual palette and grouping keys use; never display copy.</summary>
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

    /// <summary>Glyph for a group heading.</summary>
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

    /// <summary>One entry of the catalog: what the option selects, its copy, its group and its artwork.</summary>
    private sealed record ActivityDefinition(
        WorkoutActivityDto ActivityType,
        string Title,
        string Subtitle,
        ActivityGroup Group,
        bool IsCoreDefault,
        string IconGlyph,
        string IconImage = "");

    /// <summary>Headings the options are listed under.</summary>
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
