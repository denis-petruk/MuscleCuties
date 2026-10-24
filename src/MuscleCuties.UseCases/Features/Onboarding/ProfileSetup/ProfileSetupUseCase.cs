using MuscleCuties.Core.Domain.Onboarding;

namespace MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;

public sealed class ProfileSetupUseCase(IProfileSetupStore store) : IProfileSetupUseCase
{
    public Task<ProfileSetupLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        return store.LoadAsync(cancellationToken);
    }

    public async Task<ProfileSetupResult> SaveAndPrepareQuizAsync(
        ProfileSetupDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var profileValidation = OnboardingValidationRule.ValidateProfileSetup(
            new StagedProfileObservation(
                draft.Name,
                draft.BirthDate,
                draft.HeightCm,
                draft.WeightKg,
                draft.CycleLength),
            draft.SelectedActivityTypes,
            DateTime.Today);
        if (!profileValidation.IsValid)
            return new ProfileSetupResult(ProfileSetupError.InvalidProfile, profileValidation.ErrorMessage);

        var saved = await store.SaveAndPrepareQuizAsync(draft, cancellationToken).ConfigureAwait(false);
        return new ProfileSetupResult(saved ? ProfileSetupError.None : ProfileSetupError.AccountMissing);
    }
}
