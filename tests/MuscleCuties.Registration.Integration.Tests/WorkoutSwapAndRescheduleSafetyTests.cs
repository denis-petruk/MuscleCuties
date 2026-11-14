using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

/// <summary>Persistence guards for in-session swaps (G-07) and whole-occurrence rescheduling (G-06).</summary>
public sealed class WorkoutSwapAndRescheduleSafetyTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string SyntheticDatabasePrefix = "mc_";
    private const int LatPulldownId = 142;
    private const int PullUpId = 141;

    // The swap API speaks reviewed definition ids; the day exercise carries the planning id in its
    // ENGINE_<id> catalog code and the store translates it through the persisted source id.
    [PostgresFact]
    public async Task BodyweightProfileSwapNeverOffersOrAcceptsAPullUp()
    {
        await using var database = Open();
        var userId = await SeedUserAsync(database, nameof(Equipment.Bodyweight));
        try
        {
            var dayExerciseId = await SeedStrengthDayAsync(database, userId, LatPulldownId);
            var pullUpDefinitionId = await DefinitionIdAsync(database, PullUpId);
            var store = new PostgresWorkoutPlanAndSessionStore(database);

            var candidates = await store.GetSwapCandidatesAsync(userId, dayExerciseId, CancellationToken.None);
            Xunit.Assert.DoesNotContain(candidates, candidate => candidate.ExerciseId == pullUpDefinitionId);

            var failure = await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => store.SwapAsync(userId,
                new SwapWorkoutExerciseRequest(dayExerciseId, pullUpDefinitionId, true), CancellationToken.None));
            Xunit.Assert.Equal(ExerciseSwapEligibilityRule.NoEligibleReviewedSubstituteCode, failure.Message);
            Xunit.Assert.False(await database.UserExercisePreferences.AsNoTracking()
                .AnyAsync(row => row.UserId == userId));
        }
        finally
        {
            await database.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
        }
    }

    [PostgresFact]
    public async Task MissingEquipmentProfileYieldsNoSwapCandidates()
    {
        await using var database = Open();
        var userId = await SeedUserAsync(database, string.Empty);
        try
        {
            var dayExerciseId = await SeedStrengthDayAsync(database, userId, LatPulldownId);
            var pullUpDefinitionId = await DefinitionIdAsync(database, PullUpId);
            var store = new PostgresWorkoutPlanAndSessionStore(database);

            Xunit.Assert.Empty(await store.GetSwapCandidatesAsync(userId, dayExerciseId, CancellationToken.None));
            var failure = await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => store.SwapAsync(userId,
                new SwapWorkoutExerciseRequest(dayExerciseId, pullUpDefinitionId, false), CancellationToken.None));
            Xunit.Assert.Equal(ExerciseSwapEligibilityRule.EquipmentProfileMissingCode, failure.Message);
        }
        finally
        {
            await database.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
        }
    }

    // The synthetic day carries the reviewed metadata of its ENGINE_<id> exercise, so this asserts the
    // two-session rule rather than the fail-closed metadata guard.
    [PostgresFact]
    public async Task RescheduleMovesTheWholeOccurrenceAndRejectsAThirdActivityOnADay()
    {
        await using var database = Open();
        var userId = await SeedUserAsync(database, nameof(Equipment.FullGym));
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var planId = Guid.CreateVersion7();
            database.WorkoutPlans.Add(new WorkoutPlanRecord
            {
                Id = planId, UserId = userId, Name = "Synthetic plan", IsActive = true, CreatedAt = DateTime.UtcNow
            });
            var strength = Day(userId, planId, today, WorkoutActivityType.GeneralStrength, 1);
            var walking = Day(userId, planId, today.AddDays(1), WorkoutActivityType.Walking, 1);
            var yoga = Day(userId, planId, today.AddDays(2), WorkoutActivityType.Yoga, 1);
            var cycling = Day(userId, planId, today.AddDays(2), WorkoutActivityType.Cycling, 2);
            database.WorkoutDays.AddRange(strength, walking, yoga, cycling);
            var strengthExerciseId = await database.Exercises.AsNoTracking()
                .Where(exercise => exercise.Code == $"ENGINE_{LatPulldownId}")
                .Select(exercise => exercise.Id).SingleAsync();
            // Strength muscles must be readable, or the reschedule fails closed.
            database.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord
            {
                Id = Guid.CreateVersion7(), UserId = userId, WorkoutDayId = strength.Id,
                ExerciseId = strengthExerciseId, Sets = 2, Reps = 10
            });
            await database.SaveChangesAsync();
            var store = new PostgresWorkoutPlanAndSessionStore(database);

            await store.RescheduleAsync(userId, new RescheduleWorkoutDayRequest(
                PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(strength.Id), today.AddDays(1)),
                CancellationToken.None);
            var moved = await database.WorkoutDays.AsNoTracking().SingleAsync(day => day.Id == strength.Id);
            Xunit.Assert.Equal(today.AddDays(1), moved.Date);
            Xunit.Assert.Equal((int)WorkoutActivityType.GeneralStrength, moved.ActivityTypeId);
            // jsonb normalizes whitespace on the round trip; the occurrence must stay semantically identical.
            Xunit.Assert.True(JsonNode.DeepEquals(
                    JsonNode.Parse(strength.PlannedActivityPrescriptionJson!),
                    JsonNode.Parse(moved.PlannedActivityPrescriptionJson!)),
                "The moved occurrence keeps its planned prescription.");
            Xunit.Assert.Equal(WorkoutSessionOrderingRule.FirstSessionOrder, moved.DayOrder);
            var walkingAfter = await database.WorkoutDays.AsNoTracking().SingleAsync(day => day.Id == walking.Id);
            Xunit.Assert.Equal((int)WorkoutActivityType.Walking, walkingAfter.ActivityTypeId);
            Xunit.Assert.Equal(WorkoutSessionOrderingRule.FirstSessionOrder + 1, walkingAfter.DayOrder);

            var rejection = await Xunit.Assert.ThrowsAsync<WorkoutRescheduleRejectedException>(() =>
                store.RescheduleAsync(userId, new RescheduleWorkoutDayRequest(
                    PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(walking.Id), today.AddDays(2)),
                    CancellationToken.None));
            Xunit.Assert.Contains(WorkoutScheduleValidationRule.TooManySessions, rejection.Message);
        }
        finally
        {
            await database.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
        }
    }

    private static WorkoutDayRecord Day(Guid userId, Guid planId, DateOnly date, WorkoutActivityType activity,
        int order) => new()
    {
        Id = Guid.CreateVersion7(), UserId = userId, WorkoutPlanId = planId, DayOfWeek = (int)date.DayOfWeek,
        WeekIndex = 0, Date = date, ActivityTypeId = (int)activity, DayOrder = order,
        WorkoutType = WorkoutPreferencesRule.IsStrengthStyle(activity) ? "Strength" : "Cardio",
        Name = activity.ToString(), PlannedMainDurationMinutes = 30m,
        PlannedActivityPrescriptionJson = $"{{\"synthetic\":\"{activity}\"}}"
    };

    private static async Task<int> DefinitionIdAsync(PostgresDbContext database, int planningId) =>
        await database.WorkoutExerciseDefinitions.AsNoTracking()
            .Where(definition => definition.SourceExerciseId == planningId)
            .Select(definition => definition.Id).SingleAsync();

    private static async Task<int> SeedStrengthDayAsync(PostgresDbContext database, Guid userId, int planningId)
    {
        var exerciseId = await database.Exercises.AsNoTracking()
            .Where(exercise => exercise.Code == $"ENGINE_{planningId}")
            .Select(exercise => exercise.Id).SingleAsync();
        var planId = Guid.CreateVersion7();
        var dayId = Guid.CreateVersion7();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        database.WorkoutPlans.Add(new WorkoutPlanRecord
        {
            Id = planId, UserId = userId, Name = "Synthetic plan", IsActive = true, CreatedAt = DateTime.UtcNow
        });
        database.WorkoutDays.Add(new WorkoutDayRecord
        {
            Id = dayId, UserId = userId, WorkoutPlanId = planId, DayOfWeek = (int)today.DayOfWeek, WeekIndex = 0,
            Date = today, WorkoutType = "Strength", Name = "Synthetic strength",
            ActivityTypeId = (int)WorkoutActivityType.GeneralStrength
        });
        var dayExercise = new WorkoutDayExerciseRecord
        {
            Id = Guid.CreateVersion7(), UserId = userId, WorkoutDayId = dayId, ExerciseId = exerciseId, Sets = 2,
            Reps = 10
        };
        database.WorkoutDayExercises.Add(dayExercise);
        await database.SaveChangesAsync();
        return PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(dayExercise.Id);
    }

    private static async Task<Guid> SeedUserAsync(PostgresDbContext database, string equipment)
    {
        var userId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;
        await SyntheticTenantSession.SetAsync(database, userId);
        database.Users.Add(new UserRecord
        {
            Id = userId, Email = $"swap-{userId:N}@example.invalid", Username = $"swp_{userId.ToString("N")[..10]}",
            CredentialType = "Password", PasswordHash = "synthetic-hash", CreatedAt = now, UpdatedAt = now,
            IsOnboardingComplete = true
        });
        database.UserProfiles.Add(new UserProfileRecord
        {
            Id = Guid.CreateVersion7(), UserId = userId, Name = "Swap Tester", Height = 170m, Weight = 64m,
            Goal = "BuildMuscle", WeightGoalPace = "Maintain", TrainingExperienceLevel = "Beginner",
            WorkoutDaysPerWeek = 3, CycleLength = 28, EquipmentLevel = equipment,
            PreferredWorkoutActivityTypes = nameof(WorkoutActivityType.GeneralStrength), UpdatedAt = now
        });
        database.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
        {
            Id = Guid.NewGuid(), UserId = userId, TimeZoneId = TimeZoneInfo.Utc.Id, TimeZoneVersion = 1,
            BaselineExperience = TrainingExperienceLevel.Beginner, EffectiveExperience = TrainingExperienceLevel.Beginner
        });
        await database.SaveChangesAsync();
        return userId;
    }

    private static PostgresDbContext Open()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Xunit.Assert.Equal(LocalHost, builder.Host);
        Xunit.Assert.StartsWith(SyntheticDatabasePrefix, builder.Database, StringComparison.Ordinal);
        return new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connectionString).Options);
    }
}
