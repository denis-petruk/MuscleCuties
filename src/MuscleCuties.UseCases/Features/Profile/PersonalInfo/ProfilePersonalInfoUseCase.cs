using MuscleCuties.Core.Domain.Onboarding;
using MuscleCuties.Core.Domain.Profile;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.PersonalInfo;

public sealed class ProfilePersonalInfoUseCase(IProfilePersonalInfoStore store) : IProfilePersonalInfoUseCase
{
    public Task<LoadPersonalInfoResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        return store.LoadAsync(cancellationToken);
    }

    public async Task<SavePersonalInfoResult> SaveAsync(
        SavePersonalInfoRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Name))
            return new SavePersonalInfoResult(PersonalInfoSaveError.InvalidInput);

        var birthDateEligibility = BirthDateEligibilityRule.Assess(request.BirthDate, DateTime.Today);
        if (birthDateEligibility == BirthDateEligibility.MissingOrInvalid)
            return new SavePersonalInfoResult(PersonalInfoSaveError.InvalidBirthDate);
        if (birthDateEligibility == BirthDateEligibility.Underage)
            return new SavePersonalInfoResult(PersonalInfoSaveError.Underage);

        if (!ProfileUpdateValidationRule.IsValidRequiredMeasurements(
                request.Height, request.Weight, request.CycleLength))
            return new SavePersonalInfoResult(PersonalInfoSaveError.InvalidInput);

        var saved = await store.SaveWithSnapshotAsync(request, cancellationToken).ConfigureAwait(false);
        return new SavePersonalInfoResult(saved ? PersonalInfoSaveError.None : PersonalInfoSaveError.AccountMissing);
    }

    public Task<ChangeAccountPasswordResult> ChangePasswordAsync(
        ChangeAccountPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.ChangePasswordWithSnapshotAsync(request, cancellationToken);
    }
}
