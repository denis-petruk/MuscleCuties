namespace MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

/// <summary>The workout preferences read and write.</summary>
public interface IProfileWorkoutPreferencesStore
{
    Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken);

    Task<bool> SaveAsync(WorkoutPreferencesDto preferences, CancellationToken cancellationToken);
}
