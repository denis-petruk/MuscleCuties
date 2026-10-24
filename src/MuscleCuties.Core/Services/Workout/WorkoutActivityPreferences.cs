using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Services.Workout;

public static class WorkoutActivityPreferences
{
    public static IReadOnlySet<WorkoutActivityType> BuildDefaultSelection()
    {
        return new HashSet<WorkoutActivityType>
        {
            WorkoutActivityType.HighVolumeStrength
        };
    }

    public static IReadOnlySet<WorkoutActivityType> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new HashSet<WorkoutActivityType>();

        return value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseActivityType)
            .Where(activityType => activityType.HasValue)
            .Select(activityType => activityType!.Value)
            .ToHashSet();
    }

    public static string Serialize(IEnumerable<WorkoutActivityType> activityTypes) =>
        string.Join(',', EnsureRequired(activityTypes)
            .Distinct()
            .OrderBy(activityType => activityType)
            .Select(activityType => activityType.ToString()));

    public static IReadOnlySet<WorkoutActivityType> EnsureRequired(
        IEnumerable<WorkoutActivityType> activityTypes)
    {
        return activityTypes.ToHashSet();
    }

    public static bool IsCardioActivity(WorkoutActivityType activityType)
    {
        return activityType is WorkoutActivityType.Hiit or
            WorkoutActivityType.Cycling or
            WorkoutActivityType.Running or
            WorkoutActivityType.Swimming or
            WorkoutActivityType.Walking or
            WorkoutActivityType.Rowing or
            WorkoutActivityType.Elliptical;
    }

    public static bool IsRecoveryActivity(WorkoutActivityType activityType)
    {
        return activityType is WorkoutActivityType.Yoga or WorkoutActivityType.Mobility or
            WorkoutActivityType.GentleRecovery;
    }

    private static WorkoutActivityType? ParseActivityType(string value)
    {
        if (Enum.TryParse<WorkoutActivityType>(value, true, out var activityType))
            return activityType;

        var normalized = value.Replace(" ", string.Empty).Replace("-", string.Empty);
        return normalized switch
        {
            "Strength" => WorkoutActivityType.GeneralStrength,
            "StrengthHighIntensity" => WorkoutActivityType.StrengthHighIntensity,
            "HighVolumeStrength" => WorkoutActivityType.HighVolumeStrength,
            "CardioIntervals" => WorkoutActivityType.Hiit,
            "ZoneTwoCardio" => WorkoutActivityType.Cycling,
            "YogaFlow" => WorkoutActivityType.Yoga,
            "PowerYoga" => WorkoutActivityType.Yoga,
            "YinYoga" => WorkoutActivityType.Yoga,
            "RestorativeYoga" => WorkoutActivityType.Yoga,
            "Pilates" => WorkoutActivityType.Yoga,
            "Mobility" => WorkoutActivityType.Mobility,
            "Walking" => WorkoutActivityType.Walking,
            "Dance" => WorkoutActivityType.Other,
            "ActiveRecovery" => WorkoutActivityType.GentleRecovery,
            _ => null
        };
    }
}
