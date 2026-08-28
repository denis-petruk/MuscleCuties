using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Services.Workout;

public static class WorkoutActivityPreferences
{
    private const string StrengthStylePrefix = "StrengthStyle:";

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
        Serialize(activityTypes, StrengthTrainingStyle.ComfortableModerate);

    public static string Serialize(
        IEnumerable<WorkoutActivityType> activityTypes,
        StrengthTrainingStyle strengthTrainingStyle) =>
        string.Join(
            ',',
            activityTypes
                .Distinct()
                .OrderBy(activityType => activityType)
                .Select(activityType => activityType.ToString())
                .Concat([$"{StrengthStylePrefix}{strengthTrainingStyle}"]));

    public static StrengthTrainingStyle ParseStrengthStyle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return StrengthTrainingStyle.ComfortableModerate;

        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!part.StartsWith(StrengthStylePrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var rawStyle = part[StrengthStylePrefix.Length..];
            if (Enum.TryParse<StrengthTrainingStyle>(rawStyle, ignoreCase: true, out var style))
                return style;
        }

        return StrengthTrainingStyle.ComfortableModerate;
    }

    private static WorkoutActivityType? ParseActivityType(string value)
    {
        if (value.StartsWith(StrengthStylePrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        if (Enum.TryParse<WorkoutActivityType>(value, ignoreCase: true, out var activityType))
            return activityType;

        var normalized = value.Replace(" ", string.Empty).Replace("-", string.Empty);
        return normalized switch
        {
            "Strength" => WorkoutActivityType.HighVolumeStrength,
            "StrengthHighIntensity" => WorkoutActivityType.StrengthHighIntensity,
            "HighVolumeStrength" => WorkoutActivityType.HighVolumeStrength,
            "CardioIntervals" => WorkoutActivityType.Hiit,
            "ZoneTwoCardio" => WorkoutActivityType.Cycling,
            "YogaFlow" => WorkoutActivityType.Yoga,
            "PowerYoga" => WorkoutActivityType.Yoga,
            "YinYoga" => WorkoutActivityType.Yoga,
            "RestorativeYoga" => WorkoutActivityType.Yoga,
            "Pilates" => WorkoutActivityType.Yoga,
            "Mobility" => WorkoutActivityType.Yoga,
            "Walking" => WorkoutActivityType.Yoga,
            "Dance" => WorkoutActivityType.Hiit,
            "ActiveRecovery" => WorkoutActivityType.Yoga,
            _ => null
        };
    }
}
