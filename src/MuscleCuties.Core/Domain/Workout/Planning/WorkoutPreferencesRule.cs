using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public static class WorkoutPreferencesRule
{
    public const int MinimumDaysPerWeek = WeekPlanRule.MinimumDaysPerWeek;
    public const int MaximumDaysPerWeek = WeekPlanRule.MaximumDaysPerWeek;

    public static bool HasStrengthActivity(IReadOnlyList<int>? activityTypes) =>
        activityTypes is not null && activityTypes.Any(value => value is
            (int)WorkoutActivityType.StrengthHighIntensity or
            (int)WorkoutActivityType.HighVolumeStrength or
            (int)WorkoutActivityType.RockClimbing);

    public static bool IsValid(IReadOnlyList<int>? activityTypes, int strengthStyle, int daysPerWeek) =>
        activityTypes is { Count: > 0 } &&
        activityTypes.All(value => Enum.IsDefined(typeof(WorkoutActivityType), value)) &&
        Enum.IsDefined(typeof(StrengthTrainingStyle), strengthStyle) &&
        daysPerWeek is >= MinimumDaysPerWeek and <= MaximumDaysPerWeek &&
        HasStrengthActivity(activityTypes);

    public static bool IsValidEquipmentLevel(string? equipmentLevel) =>
        equipmentLevel is null ||
        Enum.TryParse<Equipment>(equipmentLevel, true, out var equipment) && Enum.IsDefined(equipment);
}
