using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.ServerProfile;

public sealed class PostgresProfileDataStore(
    PostgresDbContext dbContext,
    TimeProvider timeProvider,
    GenerateWorkoutPlanUseCase workoutPlanner)
    : IServerProfileDataStore
{
    private const string ProfileUpdateReason = "ProfileUpdate";
    private const string NutritionPreferencesReason = "NutritionPreferencesUpdate";
    private const string WorkoutPreferencesReason = "WorkoutPreferencesUpdate";
    private const string NutritionTargetsReason = "NutritionTargetsUpdate";
    private const string EmptyJsonObject = "{}";
    private const string StrengthStylePrefix = "StrengthStyle:";
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
            if (request.TrainingExperienceLevel is not null) profile.TrainingExperienceLevel = request.TrainingExperienceLevel;
            if (request.CycleTrackingMode is not null) profile.CycleTrackingMode = request.CycleTrackingMode;
            if (request.WorkoutDaysPerWeek is not null) profile.WorkoutDaysPerWeek = request.WorkoutDaysPerWeek.Value;
            if (request.CycleLength is not null) profile.CycleLength = request.CycleLength.Value;
            if (request.DietaryTags is not null) profile.DietaryTags = request.DietaryTags;
            if (request.SessionDurationMinutes is not null) profile.SessionDurationMinutes = request.SessionDurationMinutes.Value;
            if (request.EquipmentLevel is not null) profile.EquipmentLevel = request.EquipmentLevel;
            if (request.ReproductiveContextJson is not null)
            {
                var parsed = ReproductiveContextCodec.Parse(request.ReproductiveContextJson);
                profile.ReproductiveContextJson = ReproductiveContextCodec.Serialize(parsed);
            }
            if (request.ProfileImagePath is not null)
                profile.ProfileImagePath = request.ProfileImagePath;
        }, cancellationToken);

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
        if (!WorkoutPreferencesRule.IsValid(request.ActivityTypes, request.StrengthStyle, request.DaysPerWeek) ||
            !WorkoutPreferencesRule.IsValidEquipmentLevel(request.EquipmentLevel) ||
            !ValidVersionRequest(request.UserId, request.ExpectedVersion))
            return Invalid();

        var activities = request.ActivityTypes
            .Select(value => ((WorkoutActivityType)value).ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        var style = ((StrengthTrainingStyle)request.StrengthStyle).ToString();
        activities.Add($"{StrengthStylePrefix}{style}");
        var serialized = string.Join(',', activities);
        return await ExecuteTenantTransactionAsync(request.UserId, cancellationToken, async (transaction, token) =>
        {
            var profile = await dbContext.UserProfiles.SingleOrDefaultAsync(
                item => item.UserId == request.UserId, token).ConfigureAwait(false);
            if (profile is null) return Missing();
            if (profile.ConcurrencyVersion != request.ExpectedVersion) return Conflict(profile.ConcurrencyVersion);

            if (!Enum.TryParse<UserGoal>(profile.Goal, true, out var goal) || !Enum.IsDefined(goal) ||
                !Enum.TryParse<TrainingExperienceLevel>(profile.TrainingExperienceLevel, true, out var experience) ||
                !Enum.IsDefined(experience))
                return Invalid();

            var equipmentLevel = request.EquipmentLevel ?? profile.EquipmentLevel;
            var equipment = Enum.TryParse<Equipment>(equipmentLevel, true, out var selectedEquipment) &&
                            Enum.IsDefined(selectedEquipment)
                ? selectedEquipment : Equipment.FullGym;
            var phase = Enum.TryParse<CyclePhase>(profile.CurrentCyclePhase, true, out var currentPhase) &&
                        Enum.IsDefined(currentPhase) && currentPhase != CyclePhase.Unknown
                ? currentPhase : CyclePhase.Follicular;
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var activeInjuryRows = await dbContext.WorkoutInjuryLogs.AsNoTracking()
                .Where(item => item.UserId == request.UserId && item.Status != ClearedInjuryStatus)
                .ToListAsync(token).ConfigureAwait(false);
            var injuries = activeInjuryRows.Select(item => new Injury(
                (InjuryFlag)item.SiteFlag,
                Enum.TryParse<InjuryStatus>(item.Status, true, out var injuryStatus)
                    ? injuryStatus : InjuryStatus.Acute,
                item.Since)).ToArray();

            GenerateWorkoutPlanResponse generated;
            try
            {
                generated = await workoutPlanner.ExecuteAsync(request.UserId, new GenerateWorkoutPlanRequest(
                    goal, experience, request.DaysPerWeek, profile.SessionDurationMinutes,
                    request.ActivityTypes.Select(value => (WorkoutActivityType)value).ToArray(),
                    (StrengthTrainingStyle)request.StrengthStyle, equipment, phase,
                    ReproductiveContextCodec.Parse(profile.ReproductiveContextJson).AllowsCyclePhaseTrainingAdjustments,
                    now, DateOnly.FromDateTime(now), injuries, DefaultBaselineSteps), token).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                return new ServerProfileWriteResult(ServerProfileWriteStatus.InvalidCatalogReference, profile.ConcurrencyVersion);
            }

            AddSnapshot(request.UserId, WorkoutPreferencesReason, profile, now);
            profile.PreferredWorkoutActivityTypes = serialized;
            profile.WorkoutDaysPerWeek = request.DaysPerWeek;
            if (request.EquipmentLevel is not null)
                profile.EquipmentLevel = equipment.ToString();
            profile.UpdatedAt = now;

            var activePlans = await dbContext.WorkoutPlans
                .Where(item => item.UserId == request.UserId && item.IsActive)
                .ToListAsync(token).ConfigureAwait(false);
            foreach (var activePlan in activePlans)
                activePlan.IsActive = false;

            var planId = Guid.CreateVersion7();
            dbContext.WorkoutPlans.Add(new WorkoutPlanRecord
            {
                Id = planId,
                UserId = request.UserId,
                Name = generated.Name,
                IsActive = true,
                CyclePhaseTarget = generated.CyclePhaseTarget.ToString(),
                CreatedAt = now
            });
            foreach (var day in generated.WorkoutDays)
            {
                var dayId = Guid.CreateVersion7();
                dbContext.WorkoutDays.Add(new WorkoutDayRecord
                {
                    Id = dayId,
                    UserId = request.UserId,
                    WorkoutPlanId = planId,
                    DayOfWeek = day.DayOfWeek,
                    WeekIndex = day.WeekIndex,
                    Date = DateOnly.FromDateTime(day.Date),
                    WorkoutType = day.WorkoutType.ToString(),
                    Name = day.Name
                });
                foreach (var exercise in day.Exercises)
                    dbContext.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord
                    {
                        Id = Guid.CreateVersion7(),
                        UserId = request.UserId,
                        WorkoutDayId = dayId,
                        ExerciseId = exercise.ExerciseId,
                        Sets = exercise.Sets,
                        Reps = exercise.Reps,
                        DurationSeconds = exercise.DurationSeconds
                    });
            }
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
        CancellationToken cancellationToken)
    {
        if (!ValidVersionRequest(userId, expectedVersion))
            return Invalid();

        return await ExecuteTenantTransactionAsync(userId, cancellationToken, async (transaction, token) =>
        {
            var profile = await dbContext.UserProfiles.SingleOrDefaultAsync(
                item => item.UserId == userId, token).ConfigureAwait(false);
            if (profile is null) return Missing();
            if (profile.ConcurrencyVersion != expectedVersion) return Conflict(profile.ConcurrencyVersion);
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
        profile.CycleTrackingMode,
        profile.WorkoutDaysPerWeek,
        profile.CycleLength,
        profile.DietaryTags,
        profile.SessionDurationMinutes,
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
        profile.ProfileImagePath);

    private static string MergeTargets(string currentJson, IReadOnlyDictionary<string, string> updates)
    {
        var targets = JsonNode.Parse(string.IsNullOrWhiteSpace(currentJson) ? EmptyJsonObject : currentJson) as JsonObject
                      ?? new JsonObject();
        foreach (var update in updates)
            targets[update.Key] = JsonNode.Parse(update.Value);
        return targets.ToJsonString();
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
