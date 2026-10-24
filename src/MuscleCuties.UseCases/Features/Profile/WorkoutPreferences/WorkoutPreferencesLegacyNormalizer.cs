using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

internal static class WorkoutPreferencesLegacyNormalizer
{
    private const int UnsupportedLegacyActivityType = (int)WorkoutActivityType.Other;

    public static WorkoutPreferencesDto RemoveUnsupportedOtherActivity(WorkoutPreferencesDto preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        if (!preferences.ActivityTypes.Contains(UnsupportedLegacyActivityType) &&
            !preferences.ActivitySessionDurationsMinutes.ContainsKey(UnsupportedLegacyActivityType) &&
            preferences.RequestedSessionsPerWeek?.ContainsKey(UnsupportedLegacyActivityType) != true)
            return preferences;

        return preferences with
        {
            ActivityTypes = preferences.ActivityTypes
                .Where(activityType => activityType != UnsupportedLegacyActivityType)
                .ToArray(),
            ActivitySessionDurationsMinutes = preferences.ActivitySessionDurationsMinutes
                .Where(pair => pair.Key != UnsupportedLegacyActivityType)
                .ToDictionary(pair => pair.Key, pair => pair.Value),
            RequestedSessionsPerWeek = preferences.RequestedSessionsPerWeek?
                .Where(pair => pair.Key != UnsupportedLegacyActivityType)
                .ToDictionary(pair => pair.Key, pair => pair.Value)
        };
    }
}
