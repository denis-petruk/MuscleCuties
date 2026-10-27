using System.Text.Json;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

namespace MuscleCuties.Infrastructure.Cloud;

internal static class CloudWorkoutPreferencesSnapshotMapper
{
    public static WorkoutPreferencesDto ToPreferences(ServerProfileSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var activities = WorkoutActivityPreferences.Parse(snapshot.PreferredWorkoutActivityTypes)
            .Select(activity => (int)activity)
            .ToArray();
        var weekdays = ParseAvailableWeekdays(snapshot.AvailableWorkoutWeekdaysJson);

        var durations = ParseActivityDurations(snapshot.ActivitySessionDurationsJson);
        var selectedActivities = activities.ToHashSet();
        foreach (var staleActivity in durations.Keys.Where(activity =>
                     !selectedActivities.Contains(activity) ||
                     !Enum.IsDefined(typeof(WorkoutActivityType), activity) ||
                     !WorkoutPreferencesRule.RequiresSessionDuration((WorkoutActivityType)activity)).ToArray())
            durations.Remove(staleActivity);
        foreach (var activity in activities)
        {
            var activityType = (WorkoutActivityType)activity;
            if (!WorkoutPreferencesRule.RequiresSessionDuration(activityType))
                continue;
            if (durations.TryGetValue(activity, out var minutes) &&
                WorkoutPreferencesRule.IsValidSessionDuration(activityType, minutes))
                continue;

            var defaultDuration = WorkoutPreferencesRule.DefaultSessionDurationMinutes(activityType);
            if (defaultDuration is { } defaultMinutes)
                durations[activity] = defaultMinutes;
            else
                durations.Remove(activity);
        }

        var frequencies = ParseActivityFrequencies(snapshot.RequestedActivitySessionsPerWeekJson);
        foreach (var staleActivity in frequencies.Keys.Where(activity =>
                     !selectedActivities.Contains(activity) ||
                     !Enum.IsDefined(typeof(WorkoutActivityType), activity) ||
                     !WorkoutPreferencesRule.RequiresExplicitWeeklyFrequency((WorkoutActivityType)activity)).ToArray())
            frequencies.Remove(staleActivity);
        foreach (var activity in frequencies.Keys.ToArray())
            if (!WorkoutPreferencesRule.IsValidRequestedWeeklyFrequency(frequencies[activity]))
                frequencies.Remove(activity);

        var equipmentLevel = Enum.TryParse<Equipment>(snapshot.EquipmentLevel, true, out var equipment) &&
                             Enum.IsDefined(equipment)
            ? equipment.ToString()
            : Equipment.FullGym.ToString();

        var excludedExerciseIds = ParseExcludedExerciseIds(snapshot.ExcludedStrengthExerciseIdsJson);

        return new WorkoutPreferencesDto(activities, weekdays, durations, equipmentLevel, frequencies,
            excludedExerciseIds);
    }

    private static IReadOnlySet<int> ParseExcludedExerciseIds(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new HashSet<int>();
            return document.RootElement.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.Number &&
                                value.TryGetInt32(out var exerciseId) && exerciseId > 0)
                .Select(value => value.GetInt32())
                .ToHashSet();
        }
        catch (JsonException)
        {
            return new HashSet<int>();
        }
    }

    private static int[] ParseAvailableWeekdays(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return [];
            return document.RootElement.EnumerateArray()
                .Select(value => value.ValueKind switch
                {
                    JsonValueKind.Number when value.TryGetInt32(out var day) &&
                                              Enum.IsDefined(typeof(DayOfWeek), day) => day,
                    JsonValueKind.String when Enum.TryParse<DayOfWeek>(value.GetString(), true, out var day) &&
                                              Enum.IsDefined(day) => (int)day,
                    _ => -1
                })
                .Where(value => value >= 0)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Dictionary<int, int> ParseActivityDurations(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return [];
            var values = new Dictionary<int, int>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (Enum.TryParse<WorkoutActivityType>(property.Name, true, out var activity) &&
                    Enum.IsDefined(activity) && property.Value.TryGetInt32(out var minutes))
                    values[(int)activity] = minutes;
            }
            return values;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Dictionary<int, int> ParseActivityFrequencies(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return [];
            var values = new Dictionary<int, int>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (Enum.TryParse<WorkoutActivityType>(property.Name, true, out var activity) &&
                    Enum.IsDefined(activity) && property.Value.TryGetInt32(out var sessionsPerWeek))
                    values[(int)activity] = sessionsPerWeek;
            }
            return values;
        }
        catch (JsonException)
        {
            return [];
        }
    }

}
