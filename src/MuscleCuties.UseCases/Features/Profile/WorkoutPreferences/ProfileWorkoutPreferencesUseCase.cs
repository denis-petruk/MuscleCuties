namespace MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

public sealed class ProfileWorkoutPreferencesUseCase(IProfileWorkoutPreferencesStore store)
    : IProfileWorkoutPreferencesUseCase
{
    public Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken = default) =>
        store.LoadAsync(cancellationToken);
}
