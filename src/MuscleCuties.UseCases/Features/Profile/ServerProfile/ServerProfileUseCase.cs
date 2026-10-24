using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Domain.Profile;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Profile.ServerProfile;

public sealed class ServerProfileUseCase(
    IServerProfileDataStore store,
    GetReviewedExerciseExclusionCatalogV020UseCase exclusionCatalog,
    TimeProvider? clock = null)
{
    private const int MaximumTargetCount = 32;
    private const int MaximumTargetKeyLength = 64;
    private readonly TimeProvider timeProvider = clock ?? TimeProvider.System;

    public Task<ServerProfileSnapshot?> LoadAsync(Guid userId, CancellationToken cancellationToken) =>
        store.LoadAsync(userId, cancellationToken);

    public Task<IReadOnlyList<ServerProfileHistoryEntry>> LoadHistoryAsync(Guid userId, CancellationToken cancellationToken) =>
        store.LoadHistoryAsync(userId, cancellationToken);

    public Task<IReadOnlyList<ServerQuizResponse>> LoadQuizResponsesAsync(Guid userId, CancellationToken cancellationToken) =>
        store.LoadQuizResponsesAsync(userId, cancellationToken);

    public Task<ServerProfileWriteResult> UpdateProfileAsync(UpdateServerProfileRequest request, CancellationToken cancellationToken) =>
        request.ExpectedVersion <= 0 || request.UserId == Guid.Empty
            ? Invalid()
            : !ProfileUpdateValidationRule.IsValid(new ProfileUpdateObservation(
                request.DateOfBirth,
                request.SetDateOfBirth,
                request.Height,
                request.Weight,
                request.CycleLength),
                DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
                ? Invalid()
            : store.UpdateProfileAsync(request, cancellationToken);

    public Task<ServerProfileWriteResult> CorrectQuizResponseAsync(CorrectServerQuizResponseRequest request, CancellationToken cancellationToken) =>
        request.ExpectedVersion <= 0 || request.UserId == Guid.Empty || request.QuestionId <= 0 || request.AnswerId <= 0
            ? Invalid()
            : store.CorrectQuizResponseAsync(request, cancellationToken);

    public Task<ServerProfileWriteResult> UpdateNutritionPreferencesAsync(UpdateServerNutritionPreferencesRequest request, CancellationToken cancellationToken) =>
        request.ExpectedVersion <= 0 || request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.Goal) ||
        string.IsNullOrWhiteSpace(request.WeightGoalPace) || request.DietaryTags is null
            ? Invalid()
            : store.UpdateNutritionPreferencesAsync(request, cancellationToken);

    public async Task<ServerProfileWriteResult> UpdateWorkoutPreferencesAsync(
        UpdateServerWorkoutPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion <= 0 || request.UserId == Guid.Empty ||
            !WorkoutPreferencesRule.IsValid(request.ActivityTypes, request.AvailableWeekdays,
                request.ActivitySessionDurationsMinutes, request.RequestedSessionsPerWeek) ||
            !WorkoutPreferencesRule.IsValidEquipmentLevel(request.EquipmentLevel) ||
            request.ExcludedExerciseIds?.Any(exerciseId => exerciseId <= 0) == true)
            return new ServerProfileWriteResult(ServerProfileWriteStatus.InvalidRequest, null);

        if (request.ExcludedExerciseIds is { Count: > 0 })
        {
            var reviewedOptions = await exclusionCatalog.ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
            var reviewedIds = reviewedOptions.Select(option => option.ExerciseId).ToHashSet();
            if (request.ExcludedExerciseIds.Any(exerciseId => !reviewedIds.Contains(exerciseId)))
                return new ServerProfileWriteResult(ServerProfileWriteStatus.InvalidCatalogReference, null);
        }

        return await store.UpdateWorkoutPreferencesAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public Task<ServerProfileWriteResult> UpdateNutritionTargetsAsync(UpdateServerNutritionTargetsRequest request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty || request.ExpectedVersion <= 0 || request.Targets is null ||
            request.Targets.Count == 0 || request.Targets.Count > MaximumTargetCount ||
            request.Targets.Any(target => string.IsNullOrWhiteSpace(target.Key) || target.Key.Length > MaximumTargetKeyLength ||
                                          string.IsNullOrWhiteSpace(target.Value) ||
                                          !float.TryParse(target.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) ||
                                          !ProfileNutritionTargetsRule.IsValidUserEnteredTarget(target.Key, value)))
            return Task.FromResult(new ServerProfileWriteResult(ServerProfileWriteStatus.InvalidRequest, null));
        return store.UpdateNutritionTargetsAsync(request, cancellationToken);
    }

    private static Task<ServerProfileWriteResult> Invalid() =>
        Task.FromResult(new ServerProfileWriteResult(ServerProfileWriteStatus.InvalidRequest, null));
}
