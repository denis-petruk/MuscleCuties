namespace MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

public interface IProfileWorkoutPreferencesStore
{
    Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken);

    Task<WorkoutPreferencesError> SaveAsync(WorkoutPreferencesDto preferences,
        CancellationToken cancellationToken);
}
