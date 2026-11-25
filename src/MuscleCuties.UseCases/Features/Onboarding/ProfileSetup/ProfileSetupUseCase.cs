using MuscleCuties.Core.Domain.Onboarding;

namespace MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;

/// <summary>Saves the setup answers, refusing an answer set the planner cannot plan from.</summary>
public sealed class ProfileSetupUseCase(IProfileSetupStore store) : IProfileSetupUseCase
{
    /// <summary>The setup answers stored so far, so the form reopens where she left it.</summary>
    public Task<ProfileSetupLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        return store.LoadAsync(cancellationToken);
    }

    /// <summary>Saves the profile setup answers and prepares the quiz.</summary>
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
