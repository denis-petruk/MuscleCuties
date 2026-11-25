using MuscleCuties.Core.Domain.Onboarding;
using MuscleCuties.Core.Domain.Profile;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.PersonalInfo;

/// <summary>Loads and saves personal info, refusing a save based on an older version.</summary>
public sealed class ProfilePersonalInfoUseCase(IProfilePersonalInfoStore store) : IProfilePersonalInfoUseCase
{
    /// <summary>The personal info with the version a save must send back.</summary>
    public Task<LoadPersonalInfoResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        return store.LoadAsync(cancellationToken);
    }

    /// <summary>Applies the edits, refusing one based on an older version so two devices cannot overwrite each other.</summary>
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

        var measurementsValid = request.CycleLength == PersonalInfoConstraints.UnsetCycleLength
            ? ProfileUpdateValidationRule.IsValidRequiredBodyMeasurements(request.Height, request.Weight)
            : ProfileUpdateValidationRule.IsValidRequiredMeasurements(
                request.Height, request.Weight, request.CycleLength);
        if (!measurementsValid)
            return new SavePersonalInfoResult(PersonalInfoSaveError.InvalidInput);

        var saved = await store.SaveWithSnapshotAsync(request, cancellationToken).ConfigureAwait(false);
        return new SavePersonalInfoResult(saved ? PersonalInfoSaveError.None : PersonalInfoSaveError.AccountMissing);
    }

    /// <summary>Changes the account password after checking the current one.</summary>
    public Task<ChangeAccountPasswordResult> ChangePasswordAsync(
        ChangeAccountPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.ChangePasswordWithSnapshotAsync(request, cancellationToken);
    }
}
