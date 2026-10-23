using MuscleCuties.Core.Domain.Onboarding;
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
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Name) ||
            !float.IsFinite(request.Height) || request.Height <= 0 ||
            !float.IsFinite(request.Weight) || request.Weight <= 0 ||
            request.WorkoutDaysPerWeek is < 0 or > 7 || request.CycleLength is < 0 or > 60 ||
            BirthDateEligibilityRule.Assess(request.BirthDate, DateTime.Today) != BirthDateEligibility.Eligible)
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
