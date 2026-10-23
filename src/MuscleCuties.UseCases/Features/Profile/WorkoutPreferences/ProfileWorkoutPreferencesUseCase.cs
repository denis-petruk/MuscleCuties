using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

public sealed class ProfileWorkoutPreferencesUseCase(IProfileWorkoutPreferencesStore store)
    : IProfileWorkoutPreferencesUseCase
{
    public Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken = default)
    {
        return store.LoadAsync(cancellationToken);
    }

    public async Task<WorkoutPreferencesResult> SaveAndUpdatePlanAsync(
        WorkoutPreferencesDto preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (!WorkoutPreferencesRule.HasStrengthActivity(preferences.ActivityTypes))
            return new WorkoutPreferencesResult(WorkoutPreferencesError.StrengthActivityRequired);

        if (!WorkoutPreferencesRule.IsValid(preferences.ActivityTypes, preferences.StrengthStyle, preferences.DaysPerWeek))
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
            : await SaveAndUpdatePlanAsync(preferences, cancellationToken).ConfigureAwait(false);
    }
}
