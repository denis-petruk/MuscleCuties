global using ProfileSetupDraft = MuscleCuties.Contracts.Onboarding.ProfileSetupDraft;
global using ProfileSetupLoadResult = MuscleCuties.Contracts.Onboarding.ProfileSetupLoadResult;
global using ProfileSetupError = MuscleCuties.Contracts.Onboarding.ProfileSetupError;
global using ProfileSetupResult = MuscleCuties.Contracts.Onboarding.ProfileSetupResult;

namespace MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;

public interface IProfileSetupUseCase
{
    Task<ProfileSetupLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    Task<ProfileSetupResult> SaveAndPrepareQuizAsync(ProfileSetupDraft draft,
        CancellationToken cancellationToken = default);
}
