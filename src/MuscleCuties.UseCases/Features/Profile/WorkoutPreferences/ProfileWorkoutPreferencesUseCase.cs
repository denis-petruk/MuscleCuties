namespace MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

/// <summary>Loads and saves workout preferences, enforcing the constraints first.</summary>
public sealed class ProfileWorkoutPreferencesUseCase(IProfileWorkoutPreferencesStore store)
    : IProfileWorkoutPreferencesUseCase
{
    /// <summary>Loads the user's workout preferences.</summary>
    public Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken = default) =>
        store.LoadAsync(cancellationToken);

    /// <summary>Saves the workout preferences.</summary>
    public Task<bool> SaveAsync(WorkoutPreferencesDto preferences, CancellationToken cancellationToken = default) =>
        store.SaveAsync(preferences, cancellationToken);
}
