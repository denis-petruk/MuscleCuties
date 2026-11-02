namespace MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

public interface IProfileWorkoutPreferencesStore
{
    Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken);
}
