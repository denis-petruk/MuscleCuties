

namespace MuscleCuties.Infrastructure.Cloud;

public sealed class CloudProfileSetupStore(
    IPendingRegistrationService pending,
    IBackendSessionTokenStore? tokenStore = null) : IProfileSetupStore, IProfileSetupPersistence
{
    private const int DefaultCycleLengthDays = 28;
    private readonly OnboardingAccountScope _accountScope = new(tokenStore);

    private static ProfileSetupDraft Map(StagedProfileData staged) =>
        new(staged.Name, staged.BirthDate, staged.Height, staged.Weight,
            staged.ProfileImagePath, staged.CycleLength,
            staged.SelectedActivities.Select(a => (int)a).ToArray(), staged.UseMetricSystem);

    private static StagedProfileData Map(ProfileSetupDraft draft) =>
        new(draft.Name.Trim(), draft.BirthDate, draft.HeightCm, draft.WeightKg,
            draft.ProfileImagePath.Trim(),
            draft.CycleLength > 0 ? draft.CycleLength : DefaultCycleLengthDays,
            draft.SelectedActivityTypes.Where(v => Enum.IsDefined(typeof(WorkoutActivityType), v)).Select(v => (WorkoutActivityType)v).ToHashSet(),
            draft.UseMetricSystem);

    public async Task<ProfileSetupLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        var incompleteAccountId = await _accountScope.ReadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (incompleteAccountId is { } accountId)
        {
            _accountScope.ClaimForReset(accountId, pending.Clear);

            return new ProfileSetupLoadResult(
                true,
                null,
                pending.StagedProfile is { } stagedForAccount ? Map(stagedForAccount) : null);
        }

        var reg = pending.CurrentRegistration;
        if (reg is not null)
        {
            return new ProfileSetupLoadResult(
                true,
                reg.Username,
                pending.StagedProfile is { } staged ? Map(staged) : null);
        }

        return new ProfileSetupLoadResult(false, null, null);
    }

    public async Task<bool> SaveAndPrepareQuizAsync(ProfileSetupDraft draft, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var incompleteAccountId = await _accountScope.ReadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (incompleteAccountId is { } accountId)
        {
            _accountScope.ClaimForReset(accountId, pending.Clear);

            pending.StageProfile(Map(draft));
            return true;
        }

        if (pending.CurrentRegistration is null)
            return false;
        pending.StageProfile(Map(draft));
        return true;
    }

    public Task<ProfileSetupDraft?> LoadExistingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("Cloud profile setup cannot load an existing account draft from registration staging.");
    }

    public Task<bool> SaveExistingWithSnapshotAsync(ProfileSetupDraft draft, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("Cloud profile setup cannot persist existing account edits through registration staging.");
    }
}
