namespace MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;

public sealed record ProfileSetupDraft(
    string Name,
    DateTime BirthDate,
    float HeightCm,
    float WeightKg,
    string ProfileImagePath,
    int WorkoutDaysPerWeek,
    int CycleLength,
    IReadOnlyList<int> SelectedActivityTypes,
    bool UseMetricSystem,
    bool UpdateActivityPreferences = true);

public sealed record ProfileSetupLoadResult(
    bool HasPendingRegistration,
    string? PendingUsername,
    ProfileSetupDraft? Profile);

public enum ProfileSetupError
{
    None,
    InvalidName,
    InvalidBirthDate,
    Underage,
    InvalidMeasurements,
    InvalidWorkoutActivities,
    AccountMissing
}

public sealed record ProfileSetupResult(ProfileSetupError Error)
{
    public bool Succeeded => Error == ProfileSetupError.None;
}

public interface IProfileSetupUseCase
{
    Task<ProfileSetupLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    Task<ProfileSetupResult> SaveAndPrepareQuizAsync(ProfileSetupDraft draft,
        CancellationToken cancellationToken = default);
}
