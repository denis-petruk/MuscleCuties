using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

public sealed class ProfileWorkoutPreferencesUseCase(IProfileWorkoutPreferencesStore store)
    : IProfileWorkoutPreferencesUseCase
{
    public Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken = default) =>
        store.LoadAsync(cancellationToken);

    public async Task<WorkoutPreferencesResult> SaveAndUpdatePlanAsync(
        WorkoutPreferencesDto preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (!WorkoutPreferencesRule.IsValid(preferences.ActivityTypes, preferences.AvailableWeekdays,
                preferences.ActivitySessionDurationsMinutes, preferences.RequestedSessionsPerWeek))
            return new WorkoutPreferencesResult(WorkoutPreferencesError.InvalidPreferences);

        return new WorkoutPreferencesResult(await store.SaveAndUpdatePlanAsync(preferences, cancellationToken)
            .ConfigureAwait(false));
    }

    public async Task<WorkoutPreferencesResult> RegenerateSavedPlanAsync(
        CancellationToken cancellationToken = default)
    {
        var preferences = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        return preferences is null
            ? new WorkoutPreferencesResult(WorkoutPreferencesError.ProfileMissing)
            : await SaveAndUpdatePlanAsync(
                WorkoutPreferencesLegacyNormalizer.RemoveUnsupportedOtherActivity(preferences), cancellationToken)
                .ConfigureAwait(false);
    }
}
