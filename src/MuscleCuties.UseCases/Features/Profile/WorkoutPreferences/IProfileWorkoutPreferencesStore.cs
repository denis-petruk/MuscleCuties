namespace MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

public interface IProfileWorkoutPreferencesStore
{
    Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken);

    Task<WorkoutPreferencesError> SaveAndUpdatePlanAsync(WorkoutPreferencesDto preferences,
        CancellationToken cancellationToken);
}
