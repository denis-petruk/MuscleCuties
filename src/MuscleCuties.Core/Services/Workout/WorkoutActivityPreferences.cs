using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Services.Workout;

public static class WorkoutActivityPreferences
{
    public static IReadOnlySet<WorkoutActivityType> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new HashSet<WorkoutActivityType>();

        return value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Enum.TryParse<WorkoutActivityType>(part, ignoreCase: true, out var activityType)
                ? activityType
                : (WorkoutActivityType?)null)
            .Where(activityType => activityType.HasValue)
            .Select(activityType => activityType!.Value)
            .ToHashSet();
    }

    public static string Serialize(IEnumerable<WorkoutActivityType> activityTypes) =>
        string.Join(
            ',',
            activityTypes
                .Distinct()
                .OrderBy(activityType => activityType)
                .Select(activityType => activityType.ToString()));
}
