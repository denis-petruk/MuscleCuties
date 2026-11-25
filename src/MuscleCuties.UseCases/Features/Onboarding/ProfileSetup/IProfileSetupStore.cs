namespace MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;

/// <summary>The persistence the setup form needs.</summary>
public interface IProfileSetupStore
{
    Task<ProfileSetupLoadResult> LoadAsync(CancellationToken cancellationToken);
    Task<bool> SaveAndPrepareQuizAsync(ProfileSetupDraft draft, CancellationToken cancellationToken);
}

/// <summary>The write that stores a setup draft.</summary>
public interface IProfileSetupPersistence
{
    Task<ProfileSetupDraft?> LoadExistingAsync(CancellationToken cancellationToken);
    Task<bool> SaveExistingWithSnapshotAsync(ProfileSetupDraft draft, CancellationToken cancellationToken);
}
