namespace MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

public sealed record WorkoutPreferencesDto(IReadOnlyList<int> ActivityTypes, int StrengthStyle, int DaysPerWeek = 3,
    string? EquipmentLevel = null);

public enum WorkoutPreferencesError { None, ProfileMissing, StrengthActivityRequired, InvalidPreferences, PlanUpdateFailed }

public sealed record WorkoutPreferencesResult(WorkoutPreferencesError Error)
{
    public bool Succeeded => Error == WorkoutPreferencesError.None;
}

public interface IProfileWorkoutPreferencesUseCase
{
    Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken = default);

    Task<WorkoutPreferencesResult> SaveAndUpdatePlanAsync(WorkoutPreferencesDto preferences,
        CancellationToken cancellationToken = default);

    Task<WorkoutPreferencesResult> RegenerateSavedPlanAsync(CancellationToken cancellationToken = default);
}
