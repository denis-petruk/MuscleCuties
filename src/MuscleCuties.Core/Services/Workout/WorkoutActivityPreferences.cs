using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Services.Workout;

/// <summary>Her stored activity preferences, parsed once so the planner reads a typed set.</summary>
public static class WorkoutActivityPreferences
{
    /// <summary>
    /// A default must be the lowest-demand style so a new account is never started on the
    /// highest-volume prescription. High volume remains available when the user selects it.
    /// </summary>
    public static IReadOnlySet<WorkoutActivityType> BuildDefaultSelection()
    {
        return new HashSet<WorkoutActivityType>
        {
            WorkoutActivityType.GeneralStrength
        };
    }

    /// <summary>Reads the stored preferences, dropping values it does not recognise rather than failing.</summary>
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

    /// <summary>Stores the chosen activities as the code list the profile keeps.</summary>
    public static string Serialize(IEnumerable<WorkoutActivityType> activityTypes) =>
        string.Join(',', EnsureRequired(activityTypes)
            .Distinct()
            .OrderBy(activityType => activityType)
            .Select(activityType => activityType.ToString()));

    /// <summary>The chosen activities with the required defaults added.</summary>
    public static IReadOnlySet<WorkoutActivityType> EnsureRequired(
        IEnumerable<WorkoutActivityType> activityTypes)
    {
        return activityTypes.ToHashSet();
    }

    /// <summary>Whether the activity is cardio, which decides the metrics its session records.</summary>
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

    /// <summary>Whether the activity counts as recovery, which keeps it from being withheld as hard work.</summary>
    public static bool IsRecoveryActivity(WorkoutActivityType activityType)
    {
        return activityType is WorkoutActivityType.Yoga or WorkoutActivityType.Mobility or
            WorkoutActivityType.GentleRecovery;
    }

    /// <summary>Maps a stored activity name onto its type, answering null for one this build does not know.</summary>
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
