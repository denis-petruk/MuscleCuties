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
        if (string.IsNullOrWhiteSpace(draft.Name))
            return new ProfileSetupResult(ProfileSetupError.InvalidName);

        var birthDateEligibility = BirthDateEligibilityRule.Assess(draft.BirthDate, DateTime.Today);
        if (birthDateEligibility != BirthDateEligibility.Eligible)
            return new ProfileSetupResult(birthDateEligibility == BirthDateEligibility.Underage
                ? ProfileSetupError.Underage
                : ProfileSetupError.InvalidBirthDate);

        if (!float.IsFinite(draft.HeightCm) || draft.HeightCm < 50f || draft.HeightCm > 280f ||
            !float.IsFinite(draft.WeightKg) || draft.WeightKg < 20f || draft.WeightKg > 400f)
            return new ProfileSetupResult(ProfileSetupError.InvalidMeasurements);

        if (draft.SelectedActivityTypes is null || draft.SelectedActivityTypes.Count == 0)
            return new ProfileSetupResult(ProfileSetupError.InvalidWorkoutActivities);

        var saved = await store.SaveAndPrepareQuizAsync(draft, cancellationToken).ConfigureAwait(false);
        return new ProfileSetupResult(saved ? ProfileSetupError.None : ProfileSetupError.AccountMissing);
    }
}
