using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;

namespace MuscleCuties.UI.Services.Profile;

public sealed class LocalProfileSetupStore(
    IPendingRegistrationService pendingRegistration,
    IProfileSetupPersistence persistence,
    IServiceScopeFactory scopeFactory,
    QuizQuestionCache questionCache) : IProfileSetupStore
{
    public async Task<ProfileSetupLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (pendingRegistration.CurrentRegistration is { } registration)
        {
            var staged = pendingRegistration.StagedProfile;
            return new ProfileSetupLoadResult(true, registration.Username,
                staged is null ? null : Map(staged));
        }

        var existing = await DataLoadScheduler.RunAsync(() =>
            persistence.LoadExistingAsync(cancellationToken)).ConfigureAwait(false);
        return new ProfileSetupLoadResult(false, null, existing);
    }

    public async Task<bool> SaveAndPrepareQuizAsync(
        ProfileSetupDraft draft, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var selected = WorkoutActivityPreferences.EnsureRequired(
            draft.SelectedActivityTypes
                .Where(value => Enum.IsDefined(typeof(WorkoutActivityType), value))
                .Select(value => (WorkoutActivityType)value));

        if (pendingRegistration.CurrentRegistration is { } registration)
        {
            var staged = new StagedProfileData(
                draft.Name.Trim(), draft.BirthDate, draft.HeightCm, draft.WeightKg,
                draft.ProfileImagePath.Trim(),
                draft.CycleLength > 0 ? draft.CycleLength : 28, selected, draft.UseMetricSystem);
            if (!pendingRegistration.TryStageProfile(registration, staged))
                return false;
        }
        else if (!await DataLoadScheduler.RunAsync(() =>
                     persistence.SaveExistingWithSnapshotAsync(draft, cancellationToken)).ConfigureAwait(false))
        {
            return false;
        }

        await questionCache.GetOrLoadAsync(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return await DataLoadScheduler.RunAsync(() =>
                    scope.ServiceProvider.GetRequiredService<IQuizService>().GetOnboardingQuestionsAsync())
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
        return true;
    }

    private static ProfileSetupDraft Map(StagedProfileData staged)
    {
        return new ProfileSetupDraft(
            staged.Name, staged.BirthDate, staged.Height, staged.Weight,
            staged.ProfileImagePath, staged.CycleLength,
            staged.SelectedActivities.Select(activity => (int)activity).ToArray(), staged.UseMetricSystem);
    }
}
