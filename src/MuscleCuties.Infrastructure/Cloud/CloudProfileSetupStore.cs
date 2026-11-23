using System.Collections.Concurrent;
using System.Text.Json;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Profile;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Nutrition.Planning;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Nutrition.Planning;
using MuscleCuties.UseCases.Features.Account.Deletion.Contracts;
using MuscleCuties.UseCases.Features.Account.Otp;
using MuscleCuties.UseCases.Features.Account.Otp.Contracts;
using MuscleCuties.UseCases.Features.Account.Recovery;
using MuscleCuties.UseCases.Features.Account.Recovery.Contracts;
using MuscleCuties.Contracts.Account;
using MuscleCuties.Contracts.Onboarding;
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Cycle.Contracts;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;
using MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;
using MuscleCuties.UseCases.Features.Profile.NutritionSettings;
using MuscleCuties.UseCases.Features.Profile.Overview;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UseCases.Features.Profile.PhotoVault.Contracts;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Archive;


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
