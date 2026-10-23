using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Domain.Profile;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.UseCases.Features.Profile.ServerProfile;

public sealed class ServerProfileUseCase(IServerProfileDataStore store, TimeProvider? clock = null)
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
                request.CycleLength,
                request.WorkoutDaysPerWeek,
                request.SessionDurationMinutes),
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

    public Task<ServerProfileWriteResult> UpdateWorkoutPreferencesAsync(UpdateServerWorkoutPreferencesRequest request, CancellationToken cancellationToken) =>
        request.ExpectedVersion <= 0 || request.UserId == Guid.Empty || request.ActivityTypes is null ||
        !WorkoutPreferencesRule.IsValid(request.ActivityTypes, request.StrengthStyle, request.DaysPerWeek)
            ? Invalid()
            : store.UpdateWorkoutPreferencesAsync(request, cancellationToken);

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
