using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

public sealed class ProfileWorkoutPreferencesUseCase(IProfileWorkoutPreferencesStore store)
    : IProfileWorkoutPreferencesUseCase
{
    public Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken = default) =>
        store.LoadAsync(cancellationToken);

    public async Task<WorkoutPreferencesResult> SaveAsync(
        WorkoutPreferencesDto preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        preferences = WorkoutPreferencesLegacyNormalizer.RemoveUnsupportedOtherActivity(preferences);
        if (!WorkoutPreferencesRule.IsValid(preferences.ActivityTypes, preferences.AvailableWeekdays,
                preferences.ActivitySessionDurationsMinutes, preferences.RequestedSessionsPerWeek) ||
            !WorkoutPreferencesRule.IsValidEquipmentLevel(preferences.EquipmentLevel))
            return new WorkoutPreferencesResult(WorkoutPreferencesError.InvalidPreferences);

        return new WorkoutPreferencesResult(await store.SaveAsync(preferences, cancellationToken)
            .ConfigureAwait(false));
    }

}
