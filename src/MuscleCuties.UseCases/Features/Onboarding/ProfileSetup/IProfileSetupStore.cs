namespace MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;

public interface IProfileSetupStore
{
    Task<ProfileSetupLoadResult> LoadAsync(CancellationToken cancellationToken);
    Task<bool> SaveAndPrepareQuizAsync(ProfileSetupDraft draft, CancellationToken cancellationToken);
}

public interface IProfileSetupPersistence
{
    Task<ProfileSetupDraft?> LoadExistingAsync(CancellationToken cancellationToken);
    Task<bool> SaveExistingWithSnapshotAsync(ProfileSetupDraft draft, CancellationToken cancellationToken);
}
