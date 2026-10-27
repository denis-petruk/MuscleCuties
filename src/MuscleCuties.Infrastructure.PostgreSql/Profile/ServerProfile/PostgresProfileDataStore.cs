using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.ServerProfile;

public sealed class PostgresProfileDataStore(
    PostgresDbContext dbContext,
    TimeProvider timeProvider)
    : IServerProfileDataStore
{
    private const string ProfileUpdateReason = "ProfileUpdate";
    private const string QuizCorrectionReason = "QuizResponseCorrection";
    private const string NutritionPreferencesReason = "NutritionPreferencesUpdate";
    private const string WorkoutPreferencesReason = "WorkoutPreferencesUpdate";
    private const string NutritionTargetsReason = "NutritionTargetsUpdate";
    private const string EmptyJsonObject = "{}";
    private const int MinimumQuizIdentifier = 1;
    private const int MinimumExpectedProfileVersion = 1;
    private const string ClearedInjuryStatus = "Cleared";
    private const int DefaultBaselineSteps = 8000;

    public async Task<ServerProfileSnapshot?> LoadAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            return null;

        return await ExecuteTenantTransactionAsync(userId, cancellationToken, async (transaction, token) =>
        {
            var profile = await dbContext.UserProfiles.AsNoTracking()
                .SingleOrDefaultAsync(item => item.UserId == userId, token).ConfigureAwait(false);
            var user = profile is null
                ? null
                : await dbContext.Users.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == userId, token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return profile is null ? null : ToSnapshot(profile, user);
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ServerProfileHistoryEntry>> LoadHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            return [];

        return await ExecuteTenantTransactionAsync(userId, cancellationToken, async (transaction, token) =>
        {
            var rows = await dbContext.UserProfileSnapshots.AsNoTracking()
                .Where(item => item.UserId == userId)
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .Select(item => new ServerProfileHistoryEntry(item.Id, item.SnapshotReason, item.ProfileJson, item.CreatedAt))
                .ToListAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return (IReadOnlyList<ServerProfileHistoryEntry>)rows.Select(SanitizeHistoryEntry).ToArray();
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ServerQuizResponse>> LoadQuizResponsesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            return [];

        return await ExecuteTenantTransactionAsync(userId, cancellationToken, async (transaction, token) =>
        {
            var rows = await dbContext.UserQuizResponses.AsNoTracking()
                .Where(item => item.UserId == userId)
                .OrderBy(item => item.QuizQuestionId)
                .ThenByDescending(item => item.AnsweredAt)
                .ThenByDescending(item => item.UserProfileSnapshotId)
                .ThenByDescending(item => item.Id)
                .Select(item => new ServerQuizResponse(item.QuizQuestionId, item.QuizAnswerId, item.AnsweredAt))
                .ToListAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return (IReadOnlyList<ServerQuizResponse>)rows.GroupBy(item => item.QuestionId)
                .Select(group => group.First()).OrderBy(item => item.QuestionId).ToArray();
        }).ConfigureAwait(false);
    }

    public Task<ServerProfileWriteResult> UpdateProfileAsync(
        UpdateServerProfileRequest request,
        CancellationToken cancellationToken) => UpdateAsync(
        request.UserId,
        request.ExpectedVersion,
        ProfileUpdateReason,
        (profile, _) =>
        {
            if (request.Name is not null) profile.Name = request.Name;
            if (request.SetDateOfBirth) profile.DateOfBirth = request.DateOfBirth;
            if (request.Height is not null) profile.Height = request.Height.Value;
            if (request.Weight is not null) profile.Weight = request.Weight.Value;
            if (request.Goal is not null) profile.Goal = request.Goal;
            if (request.WeightGoalPace is not null) profile.WeightGoalPace = request.WeightGoalPace;
            if (request.CycleLength is not null) profile.CycleLength = request.CycleLength.Value;
            if (request.DietaryTags is not null) profile.DietaryTags = request.DietaryTags;
            if (request.EquipmentLevel is not null) profile.EquipmentLevel = request.EquipmentLevel;
            if (request.ReproductiveContextJson is not null)
            {
                var parsed = ReproductiveContextCodec.Parse(request.ReproductiveContextJson);
                profile.ReproductiveContextJson = ReproductiveContextCodec.Serialize(parsed);
            }
            if (request.ProfileImagePath is not null)
                profile.ProfileImagePath = request.ProfileImagePath;
        }, cancellationToken,
        profile => request.TrainingExperienceLevel is null ||
                   string.Equals(request.TrainingExperienceLevel, profile.TrainingExperienceLevel,
                       StringComparison.OrdinalIgnoreCase), request.EquipmentLevel is not null);

    public async Task<ServerProfileWriteResult> CorrectQuizResponseAsync(
        CorrectServerQuizResponseRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidVersionRequest(request.UserId, request.ExpectedVersion) ||
            request.QuestionId < MinimumQuizIdentifier || request.AnswerId < MinimumQuizIdentifier)
            return Invalid();

        return await ExecuteTenantTransactionAsync(request.UserId, cancellationToken, async (transaction, token) =>
        {
            await WorkoutSessionAccountLock.AcquireAsync(dbContext, request.UserId, token).ConfigureAwait(false);
            if (await WorkoutSessionAccountLock.HasActivePreviewAsync(dbContext, request.UserId,
                    timeProvider.GetUtcNow().UtcDateTime, token).ConfigureAwait(false)) return Conflict(request.ExpectedVersion);
            var profile = await dbContext.UserProfiles.SingleOrDefaultAsync(
                item => item.UserId == request.UserId, token).ConfigureAwait(false);
            if (profile is null) return Missing();
            if (profile.ConcurrencyVersion != request.ExpectedVersion) return Conflict(profile.ConcurrencyVersion);

            var answerExists = await dbContext.QuizAnswers.AsNoTracking().AnyAsync(
                answer => answer.Id == request.AnswerId && answer.QuestionId == request.QuestionId, token).ConfigureAwait(false);
            if (!answerExists) return new ServerProfileWriteResult(ServerProfileWriteStatus.InvalidCatalogReference, profile.ConcurrencyVersion);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var snapshot = AddSnapshot(request.UserId, QuizCorrectionReason, profile, now);
            dbContext.UserQuizResponses.Add(new UserQuizResponseRecord
            {
                Id = Guid.CreateVersion7(), UserId = request.UserId, QuizQuestionId = request.QuestionId,
                QuizAnswerId = request.AnswerId, AnsweredAt = now, UserProfileSnapshotId = snapshot.Id
            });
            profile.UpdatedAt = now;
            return await SaveVersionedAsync(transaction, profile, token).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public Task<ServerProfileWriteResult> UpdateNutritionPreferencesAsync(
        UpdateServerNutritionPreferencesRequest request,
        CancellationToken cancellationToken) => UpdateAsync(
        request.UserId,
        request.ExpectedVersion,
        NutritionPreferencesReason,
        (profile, _) =>
        {
            profile.Goal = request.Goal;
            profile.WeightGoalPace = request.WeightGoalPace;
            profile.DietaryTags = request.DietaryTags;
        }, cancellationToken);

    public async Task<ServerProfileWriteResult> UpdateWorkoutPreferencesAsync(
        UpdateServerWorkoutPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        if (!WorkoutPreferencesRule.IsValid(request.ActivityTypes, request.AvailableWeekdays,
                request.ActivitySessionDurationsMinutes, request.RequestedSessionsPerWeek) ||
            !WorkoutPreferencesRule.IsValidEquipmentLevel(request.EquipmentLevel) ||
            !ValidVersionRequest(request.UserId, request.ExpectedVersion))
            return Invalid();

        var serializedActivities = string.Join(',', request.ActivityTypes
            .Select(value => ((WorkoutActivityType)value).ToString())
            .OrderBy(value => value, StringComparer.Ordinal));
        var serializedWeekdays = JsonSerializer.Serialize(request.AvailableWeekdays
            .Select(value => ((DayOfWeek)value).ToString()).OrderBy(value => value, StringComparer.Ordinal));
        var serializedDurations = JsonSerializer.Serialize(request.ActivitySessionDurationsMinutes
            .ToDictionary(pair => ((WorkoutActivityType)pair.Key).ToString(), pair => pair.Value,
                StringComparer.Ordinal));
        var serializedFrequencies = JsonSerializer.Serialize((request.RequestedSessionsPerWeek ??
                new Dictionary<int, int>())
            .ToDictionary(pair => ((WorkoutActivityType)pair.Key).ToString(), pair => pair.Value,
                StringComparer.Ordinal));
        var serializedExcludedExerciseIds = JsonSerializer.Serialize((request.ExcludedExerciseIds ??
                new HashSet<int>()).OrderBy(exerciseId => exerciseId).ToArray());
        return await ExecuteTenantTransactionAsync(request.UserId, cancellationToken, async (transaction, token) =>
        {
            await WorkoutSessionAccountLock.AcquireAsync(dbContext, request.UserId, token).ConfigureAwait(false);
            if (await WorkoutSessionAccountLock.HasActivePreviewAsync(dbContext, request.UserId,
                    timeProvider.GetUtcNow().UtcDateTime, token).ConfigureAwait(false)) return Conflict(request.ExpectedVersion);
            var profile = await dbContext.UserProfiles.SingleOrDefaultAsync(
                item => item.UserId == request.UserId, token).ConfigureAwait(false);
            if (profile is null) return Missing();
            if (profile.ConcurrencyVersion != request.ExpectedVersion) return Conflict(profile.ConcurrencyVersion);

            var now = timeProvider.GetUtcNow().UtcDateTime;
            AddSnapshot(request.UserId, WorkoutPreferencesReason, profile, now);
            profile.PreferredWorkoutActivityTypes = serializedActivities;
            profile.AvailableWorkoutWeekdaysJson = serializedWeekdays;
            profile.ActivitySessionDurationsJson = serializedDurations;
            profile.RequestedActivitySessionsPerWeekJson = serializedFrequencies;
            profile.ExcludedStrengthExerciseIdsJson = serializedExcludedExerciseIds;
            if (request.EquipmentLevel is not null)
                profile.EquipmentLevel = request.EquipmentLevel;
            profile.UpdatedAt = now;
            return await SaveVersionedAsync(transaction, profile, token).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public Task<ServerProfileWriteResult> UpdateNutritionTargetsAsync(
        UpdateServerNutritionTargetsRequest request,
        CancellationToken cancellationToken) => UpdateAsync(
        request.UserId,
        request.ExpectedVersion,
        NutritionTargetsReason,
        (profile, _) => profile.NutritionGoalsJson = MergeTargets(profile.NutritionGoalsJson, request.Targets),
        cancellationToken);

    private async Task<ServerProfileWriteResult> UpdateAsync(
        Guid userId,
        int expectedVersion,
        string reason,
        Action<UserProfileRecord, DateTime> apply,
        CancellationToken cancellationToken,
        Func<UserProfileRecord, bool>? validate = null,
        bool guardPlanning = false)
    {
        if (!ValidVersionRequest(userId, expectedVersion))
            return Invalid();

        return await ExecuteTenantTransactionAsync(userId, cancellationToken, async (transaction, token) =>
        {
            if (guardPlanning)
            {
                await WorkoutSessionAccountLock.AcquireAsync(dbContext, userId, token).ConfigureAwait(false);
                if (await WorkoutSessionAccountLock.HasActivePreviewAsync(dbContext, userId,
                        timeProvider.GetUtcNow().UtcDateTime, token).ConfigureAwait(false)) return Conflict(expectedVersion);
            }
            var profile = await dbContext.UserProfiles.SingleOrDefaultAsync(
                item => item.UserId == userId, token).ConfigureAwait(false);
            if (profile is null) return Missing();
            if (profile.ConcurrencyVersion != expectedVersion) return Conflict(profile.ConcurrencyVersion);
            if (validate is not null && !validate(profile)) return Invalid();
            var now = timeProvider.GetUtcNow().UtcDateTime;
            AddSnapshot(userId, reason, profile, now);
            apply(profile, now);
            profile.UpdatedAt = now;
            return await SaveVersionedAsync(transaction, profile, token).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private async Task<ServerProfileWriteResult> SaveVersionedAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        UserProfileRecord profile,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ServerProfileWriteResult(ServerProfileWriteStatus.Saved, profile.ConcurrencyVersion);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            return Conflict(null);
        }
    }

    private async Task<T> ExecuteTenantTransactionAsync<T>(
        Guid userId,
        CancellationToken cancellationToken,
        Func<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction, CancellationToken, Task<T>> operation)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, token).ConfigureAwait(false);
            try
            {
                return await operation(transaction, token).ConfigureAwait(false);
            }
            catch
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private UserProfileSnapshotRecord AddSnapshot(
        Guid userId,
        string reason,
        UserProfileRecord profile,
        DateTime now)
    {
        var snapshot = new UserProfileSnapshotRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SnapshotReason = reason,
            ProfileJson = JsonSerializer.Serialize(profile),
            CreatedAt = now
        };
        dbContext.UserProfileSnapshots.Add(snapshot);
        return snapshot;
    }

    private static ServerProfileSnapshot ToSnapshot(UserProfileRecord profile, UserRecord? user = null) => new(
        profile.Name,
        profile.DateOfBirth,
        profile.Height,
        profile.Weight,
        profile.Goal,
        profile.WeightGoalPace,
        profile.TrainingExperienceLevel,
        profile.WorkoutDaysPerWeek,
        profile.AvailableWorkoutWeekdaysJson,
        profile.CycleLength,
        profile.DietaryTags,
        profile.ActivitySessionDurationsJson,
        profile.EquipmentLevel,
        profile.PreferredWorkoutActivityTypes,
        profile.UnitSystem,
        profile.BodyWeightUnit,
        profile.FoodMassUnit,
        profile.HeightUnit,
        profile.DistanceUnit,
        profile.EnergyUnit,
        profile.NutritionGoalsJson,
        profile.ConcurrencyVersion,
        user?.Email ?? string.Empty,
        profile.ReproductiveContextJson,
        user?.CreatedAt,
        profile.ProfileImagePath,
        profile.RequestedActivitySessionsPerWeekJson,
        profile.ExcludedStrengthExerciseIdsJson);

    private static string MergeTargets(string currentJson, IReadOnlyDictionary<string, string> updates)
    {
        var targets = JsonNode.Parse(string.IsNullOrWhiteSpace(currentJson) ? EmptyJsonObject : currentJson) as JsonObject
                      ?? new JsonObject();
        foreach (var update in updates)
            targets[update.Key] = JsonNode.Parse(update.Value);
        return targets.ToJsonString();
    }

    private static ServerProfileHistoryEntry SanitizeHistoryEntry(ServerProfileHistoryEntry entry)
    {
        var profile = JsonNode.Parse(entry.ProfileJson) as JsonObject ?? new JsonObject();
        profile.Remove(nameof(UserProfileRecord.ProfileImagePath));
        return entry with { ProfileJson = profile.ToJsonString() };
    }

    private static bool ValidVersionRequest(Guid userId, int expectedVersion) =>
        userId != Guid.Empty && expectedVersion >= MinimumExpectedProfileVersion;

    private static ServerProfileWriteResult Invalid() =>
        new(ServerProfileWriteStatus.InvalidRequest, null);

    private static ServerProfileWriteResult Missing() =>
        new(ServerProfileWriteStatus.ProfileMissing, null);

    private static ServerProfileWriteResult Conflict(int? version) =>
        new(ServerProfileWriteStatus.VersionConflict, version);
}
