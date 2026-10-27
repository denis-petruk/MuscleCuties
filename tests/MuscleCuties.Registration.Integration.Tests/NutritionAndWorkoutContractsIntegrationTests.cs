using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Overview;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Targets;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Archive;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class NutritionAndWorkoutContractsIntegrationTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string SyntheticDatabasePrefix = "mc_";
    private const int SyntheticStrengthSets = 3;
    private const int SyntheticStrengthRepetitions = 10;
    private const int TestWorkoutDaysPerWeek = 3;
    private const int TestCycleLengthDays = 28;
    private const int TestStrengthSessionDurationMinutes = WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes;

    [Xunit.Fact]
    public async Task GroupedMealsAndCustomFoodsRoundTripAgainstPostgreSql()
    {
        var connectionString = RequireLocalTestConnectionString();
        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connectionString).Options;
        await using var database = new PostgresDbContext(options);

        var userId = Guid.CreateVersion7();
        var nowUtc = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(nowUtc);
        await SyntheticTenantSession.SetAsync(database, userId);

        database.Users.Add(new UserRecord
        {
            Id = userId,
            Email = $"nutrition-{userId:N}@example.invalid",
            Username = $"nut_{userId.ToString("N")[..10]}",
            CredentialType = "Password",
            PasswordHash = "synthetic-hash",
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            IsOnboardingComplete = true
        });
        database.UserProfiles.Add(new UserProfileRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = "Nutrition Tester",
            Height = 168m,
            Weight = 62m,
            Goal = "Tone",
            WeightGoalPace = "Maintain",
            TrainingExperienceLevel = "Intermediate",
            WorkoutDaysPerWeek = TestWorkoutDaysPerWeek,
            AvailableWorkoutWeekdaysJson = JsonSerializer.Serialize(new[]
                { nameof(DayOfWeek.Monday), nameof(DayOfWeek.Wednesday), nameof(DayOfWeek.Friday) }),
            CycleLength = TestCycleLengthDays,
            ActivitySessionDurationsJson = JsonSerializer.Serialize(new Dictionary<string, int>
            {
                [nameof(WorkoutActivityType.HighVolumeStrength)] = TestStrengthSessionDurationMinutes
            }),
            EquipmentLevel = "FullGym",
            UpdatedAt = nowUtc
        });
        await database.SaveChangesAsync();

        try
        {
            var targetStore = new PostgresNutritionTargetProjectionStore(database);
            var overviewStore = new PostgresNutritionOverviewStore(database, targetStore);

            var customFood = await overviewStore.CreateCustomFoodAsync(
                userId,
                new CreateCustomFoodRequest("Skyr Berry Bowl", 100f, "g", 120f, 15f, 10f, 2f),
                CancellationToken.None);

            Xunit.Assert.NotEqual(0, customFood.FoodItemId);
            Xunit.Assert.Equal("Skyr Berry Bowl", customFood.Name);

            await overviewStore.LogMealAsync(
                userId,
                new LogMealRequest(
                    today,
                    "Breakfast",
                    TimeSpan.FromHours(8),
                    [new MealIngredientInputDto(customFood.FoodItemId, "Skyr Berry Bowl", 200f, 200f, "g", 120f, 15f, 10f, 2f, "Custom")],
                    "Skyr Concept",
                    "Follicular"),
                CancellationToken.None);

            var overview = await overviewStore.GetOverviewAsync(
                userId,
                today,
                "Sweet",
                CancellationToken.None);

            var loggedMeal = Xunit.Assert.Single(overview.Meals, m => m.LoggedMealId > 0);
            Xunit.Assert.Equal("Breakfast", loggedMeal.MealType);
            Xunit.Assert.InRange(loggedMeal.Calories, 239f, 241f);

            var ingredients = await overviewStore.GetMealIngredientsAsync(
                userId,
                loggedMeal.LoggedMealId,
                CancellationToken.None);

            var singleIngredient = Xunit.Assert.Single(ingredients);
            Xunit.Assert.Equal(customFood.FoodItemId, singleIngredient.FoodItemId);
            Xunit.Assert.Equal(200f, singleIngredient.Grams);

            await overviewStore.UpdateMealAsync(
                userId,
                new UpdateMealRequest(
                    loggedMeal.LoggedMealId,
                    today,
                    "Lunch",
                    TimeSpan.FromHours(13),
                    [new MealIngredientInputDto(customFood.FoodItemId, "Skyr Berry Bowl", 150f, 150f, "g", 120f, 15f, 10f, 2f, "Custom")]),
                CancellationToken.None);

            var updatedIngredients = await overviewStore.GetMealIngredientsAsync(
                userId,
                loggedMeal.LoggedMealId,
                CancellationToken.None);
            var updatedSingle = Xunit.Assert.Single(updatedIngredients);
            Xunit.Assert.Equal(150f, updatedSingle.Grams);
        }
        finally
        {
            await database.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
        }
    }

    [Xunit.Fact]
    public async Task WorkoutOverviewAndSessionActionsRoundTripAgainstPostgreSql()
    {
        var connectionString = RequireLocalTestConnectionString();
        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connectionString).Options;
        await using var database = new PostgresDbContext(options);

        var userId = Guid.CreateVersion7();
        var nowUtc = DateTime.UtcNow;
        await SyntheticTenantSession.SetAsync(database, userId);
        var today = DateOnly.FromDateTime(nowUtc);

        database.Users.Add(new UserRecord
        {
            Id = userId,
            Email = $"workout-{userId:N}@example.invalid",
            Username = $"wrk_{userId.ToString("N")[..10]}",
            CredentialType = "Password",
            PasswordHash = "synthetic-hash",
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            IsOnboardingComplete = true
        });
        database.UserProfiles.Add(new UserProfileRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = "Workout Tester",
            Height = 170m,
            Weight = 64m,
            Goal = "BuildMuscle",
            WeightGoalPace = "Maintain",
            TrainingExperienceLevel = "Intermediate",
            WorkoutDaysPerWeek = TestWorkoutDaysPerWeek,
            AvailableWorkoutWeekdaysJson = JsonSerializer.Serialize(new[]
                { nameof(DayOfWeek.Monday), nameof(DayOfWeek.Wednesday), nameof(DayOfWeek.Friday) }),
            CycleLength = TestCycleLengthDays,
            ActivitySessionDurationsJson = JsonSerializer.Serialize(new Dictionary<string, int>
            {
                [nameof(WorkoutActivityType.HighVolumeStrength)] = TestStrengthSessionDurationMinutes
            }),
            EquipmentLevel = "FullGym",
            PreferredWorkoutActivityTypes = nameof(WorkoutActivityType.HighVolumeStrength),
            UpdatedAt = nowUtc
        });
        await database.SaveChangesAsync();

        try
        {
            var workoutStore = new PostgresWorkoutPlanAndSessionStore(database);
            var archiveStore = new PostgresWorkoutArchiveStore(database);
            database.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
            {
                Id = Guid.NewGuid(), UserId = userId, TimeZoneId = TimeZoneInfo.Utc.Id,
                TimeZoneVersion = 1, BaselineExperience = TrainingExperienceLevel.Intermediate,
                EffectiveExperience = TrainingExperienceLevel.Intermediate
            });
            await database.SaveChangesAsync();

            var emptyOverview = await workoutStore.LoadOverviewAsync(
                userId,
                new WorkoutOverviewRequest(today),
                CancellationToken.None);
            Xunit.Assert.False(await database.WorkoutPlans.AsNoTracking()
                .AnyAsync(plan => plan.UserId == userId));
            Xunit.Assert.DoesNotContain(emptyOverview.Workouts, workout => workout.WorkoutDayId > 0);

            var exerciseId = await database.Exercises.AsNoTracking()
                .OrderBy(exercise => exercise.Id)
                .Select(exercise => exercise.Id)
                .FirstAsync();
            var existingPlanId = Guid.CreateVersion7();
            var existingDayId = Guid.CreateVersion7();
            database.WorkoutPlans.Add(new WorkoutPlanRecord
            {
                Id = existingPlanId, UserId = userId, Name = "Synthetic existing plan",
                IsActive = true, CyclePhaseTarget = "Follicular", CreatedAt = nowUtc
            });
            database.WorkoutDays.Add(new WorkoutDayRecord
            {
                Id = existingDayId, UserId = userId, WorkoutPlanId = existingPlanId,
                DayOfWeek = (int)today.DayOfWeek, WeekIndex = 0, Date = today,
                WorkoutType = "Strength", Name = "Synthetic strength"
            });
            database.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord
            {
                Id = Guid.CreateVersion7(), UserId = userId, WorkoutDayId = existingDayId,
                ExerciseId = exerciseId, Sets = SyntheticStrengthSets,
                Reps = SyntheticStrengthRepetitions
            });
            await database.SaveChangesAsync();

            var overview = await workoutStore.LoadOverviewAsync(
                userId, new WorkoutOverviewRequest(today), CancellationToken.None);

            Xunit.Assert.False(string.IsNullOrWhiteSpace(overview.PlanName));
            var activeWorkout = overview.Workouts.First(w => !w.IsRestDay && w.WorkoutDayId > 0);

            var detail = await workoutStore.GetDetailAsync(userId, activeWorkout.WorkoutDayId, CancellationToken.None);
            Xunit.Assert.False(detail.IsRestDay);
            Xunit.Assert.NotEmpty(detail.Exercises);

            var firstExercise = detail.Exercises[0];
            await workoutStore.SaveMovementNoteAsync(
                userId,
                new SaveWorkoutMovementNoteRequest(firstExercise.WorkoutDayExerciseId, "Keep ribcage stacked."),
                CancellationToken.None);

            var note = await workoutStore.GetMovementNoteAsync(
                userId,
                firstExercise.WorkoutDayExerciseId,
                CancellationToken.None);
            Xunit.Assert.Equal("Keep ribcage stacked.", note.Notes);

            var swapCandidates = await workoutStore.GetSwapCandidatesAsync(
                userId,
                firstExercise.WorkoutDayExerciseId,
                CancellationToken.None);
            if (swapCandidates.Count > 0)
            {
                await workoutStore.SwapAsync(
                    userId,
                    new SwapWorkoutExerciseRequest(
                        firstExercise.WorkoutDayExerciseId,
                        swapCandidates[0].ExerciseId,
                        true),
                    CancellationToken.None);
            }

            await workoutStore.LogAsync(
                userId,
                new LogWorkoutSessionRequest(
                    activeWorkout.WorkoutDayId,
                    today,
                    detail.Exercises.Select(ex => new WorkoutExerciseLogDto(
                        ex.WorkoutDayExerciseId,
                        ex.ExerciseId,
                        3,
                        10,
                        40f)).ToArray()),
                CancellationToken.None);

            var archiveHistory = await archiveStore.GetWorkoutHistoryAsync(userId, 10, 0, CancellationToken.None);
            Xunit.Assert.NotEmpty(archiveHistory);
            var earnedCredit = await database.WorkoutCompletionDayCredits.AsNoTracking()
                .SingleAsync(credit => credit.UserId == userId && credit.LocalDate == today);
            Xunit.Assert.Equal(TimeZoneInfo.Utc.Id, earnedCredit.TimeZoneId);
            await database.WorkoutLogs.Where(log => log.UserId == userId).ExecuteDeleteAsync();
            Xunit.Assert.True(await database.WorkoutCompletionDayCredits.AsNoTracking()
                .AnyAsync(credit => credit.UserId == userId && credit.Id == earnedCredit.Id));
        }
        finally
        {
            await database.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
        }
    }

    [Xunit.Fact]
    public async Task UnsafeAnswerSuppressesUnfinishedHundredPercentLogButKeepsFinishedSession()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(RequireLocalTestConnectionString()).Options;
        await using var database = new PostgresDbContext(options);
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        await SyntheticTenantSession.SetAsync(database, userId);
        database.Users.Add(new UserRecord { Id = userId, Email = $"safety-{userId:N}@example.invalid",
            Username = $"saf_{userId.ToString("N")[..10]}", CredentialType = "Password", PasswordHash = "synthetic-hash",
            CreatedAt = now, UpdatedAt = now, IsOnboardingComplete = true });
        var planId = Guid.NewGuid();
        var unfinishedDayId = Guid.NewGuid();
        var completedDayId = Guid.NewGuid();
        var logId = Guid.NewGuid();
        var finishedSessionId = Guid.NewGuid();
        var unfinishedSessionId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        database.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
        {
            Id = Guid.NewGuid(), UserId = userId, TimeZoneId = TimeZoneInfo.Utc.Id,
            BaselineExperience = TrainingExperienceLevel.Beginner, EffectiveExperience = TrainingExperienceLevel.Beginner
        });
        database.WorkoutPlans.Add(new WorkoutPlanRecord { Id = planId, UserId = userId, Name = "Synthetic plan",
            IsActive = true, CyclePhaseTarget = "Follicular", CreatedAt = now });
        foreach (var dayId in new[] { unfinishedDayId, completedDayId })
            database.WorkoutDays.Add(new WorkoutDayRecord { Id = dayId, UserId = userId, WorkoutPlanId = planId,
                DayOfWeek = (int)today.DayOfWeek, WeekIndex = 0, Date = today, WorkoutType = "Cardio",
                ActivityTypeId = (int)WorkoutActivityType.Swimming, Name = "Synthetic swimming" });
        database.WorkoutLogs.Add(new WorkoutLogRecord { Id = logId, UserId = userId, WorkoutDayId = unfinishedDayId,
            Date = today, CompletionPercent = 100, CreatedAt = now });
        database.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord { Id = finishedSessionId, UserId = userId,
            PlannedWorkoutDayId = completedDayId, WorkoutName = "Synthetic finished", StartedAt = now,
            CompletedAt = now, CreatedAt = now, UpdatedAt = now });
        database.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord { Id = unfinishedSessionId, UserId = userId,
            PlannedWorkoutDayId = unfinishedDayId, WorkoutName = "Synthetic unfinished", StartedAt = now,
            CreatedAt = now, UpdatedAt = now });
        database.ExerciseSetLogs.Add(new ExerciseSetLogRecord { Id = setId, UserId = userId,
            WorkoutSessionLogId = unfinishedSessionId, ExerciseName = "Synthetic set", SetNumber = 1,
            RepsCompleted = SyntheticStrengthRepetitions, CreatedAt = now, UpdatedAt = now });
        await database.SaveChangesAsync();
        try
        {
            var store = new PostgresStrengthPlanProposalStoreV020(database);
            var unsafeAnswer = new SaveActivityAnswersRequest(0,
                [new(MuscleCuties.UseCases.Features.Workout.Contracts.WorkoutActivityDto.Swimming,
                    HasSwimmingFacilityAccess: false)], null);
            var saved = await store.SaveAnswersAsync(userId, unsafeAnswer, CancellationToken.None);
            var retried = await store.SaveAnswersAsync(userId, unsafeAnswer, CancellationToken.None);
            Xunit.Assert.True(saved.SafetyPlanChanged);
            Xunit.Assert.True(retried.SafetyPlanChanged);
            Xunit.Assert.Equal(saved.Version, retried.Version);
            Xunit.Assert.False((await store.ReadAnswersAsync(userId, CancellationToken.None)).SafetyPlanChanged);
            await Xunit.Assert.ThrowsAsync<WorkoutPlanningInputsChangedException>(() => store.SaveAnswersAsync(userId,
                unsafeAnswer with { ActivityEligibilityEvidence = [] }, CancellationToken.None));
            var days = await database.WorkoutDays.AsNoTracking().Where(day => day.UserId == userId)
                .ToDictionaryAsync(day => day.Id);
            Xunit.Assert.True(days[unfinishedDayId].IsRemovedBySafety);
            Xunit.Assert.False(days[completedDayId].IsRemovedBySafety);
            Xunit.Assert.True(await database.WorkoutLogs.AsNoTracking().AnyAsync(log => log.Id == logId));
            Xunit.Assert.True(await database.ExerciseSetLogs.AsNoTracking().AnyAsync(set => set.Id == setId));

            var safeSwimming = new MuscleCuties.UseCases.Features.Workout.Planning.ActivityEligibilityInputV020(
                MuscleCuties.UseCases.Features.Workout.Contracts.WorkoutActivityDto.Swimming,
                HasSwimmingFacilityAccess: true, CanSwimIndependently: true);
            await store.SaveAnswersAsync(userId, new SaveActivityAnswersRequest(1, [safeSwimming], null), CancellationToken.None);
            // Model a separately applied safe plan before withdrawing the safety evidence.
            await database.WorkoutDays.Where(day => day.Id == unfinishedDayId)
                .ExecuteUpdateAsync(update => update.SetProperty(day => day.IsRemovedBySafety, false));
            await store.SaveAnswersAsync(userId, new SaveActivityAnswersRequest(2,
                [safeSwimming with { CanSwimIndependently = null }], null), CancellationToken.None);
            Xunit.Assert.True(await database.WorkoutDays.AsNoTracking().Where(day => day.Id == unfinishedDayId)
                .Select(day => day.IsRemovedBySafety).SingleAsync());

            await store.SaveAnswersAsync(userId, new SaveActivityAnswersRequest(3, [safeSwimming], null), CancellationToken.None);
            await database.WorkoutDays.Where(day => day.Id == unfinishedDayId)
                .ExecuteUpdateAsync(update => update.SetProperty(day => day.IsRemovedBySafety, false));
            await store.SaveAnswersAsync(userId, new SaveActivityAnswersRequest(4, [], null), CancellationToken.None);
            Xunit.Assert.True(await database.WorkoutDays.AsNoTracking().Where(day => day.Id == unfinishedDayId)
                .Select(day => day.IsRemovedBySafety).SingleAsync());

            var hiitDayId = Guid.NewGuid();
            database.WorkoutDays.Add(new WorkoutDayRecord { Id = hiitDayId, UserId = userId, WorkoutPlanId = planId,
                DayOfWeek = (int)today.DayOfWeek, WeekIndex = 0, Date = today, WorkoutType = "Cardio",
                ActivityTypeId = (int)WorkoutActivityType.Hiit, Name = "Synthetic HIIT" });
            await database.SaveChangesAsync();
            var choice = new SaveActivityAnswersRequest(5, [], null)
                { HiitUnderlyingCardioActivity = MuscleCuties.UseCases.Features.Workout.Contracts.WorkoutActivityDto.Cycling };
            await store.SaveAnswersAsync(userId, choice, CancellationToken.None);
            Xunit.Assert.True(await database.WorkoutDays.AsNoTracking().Where(day => day.Id == hiitDayId)
                .Select(day => day.IsRemovedBySafety).SingleAsync());
            Xunit.Assert.Equal(choice.HiitUnderlyingCardioActivity,
                (await store.ReadAnswersAsync(userId, CancellationToken.None)).HiitUnderlyingCardioActivity);
        }
        finally
        {
            await database.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
        }
    }

    private static string RequireLocalTestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Xunit.Assert.Equal(LocalHost, builder.Host);
        Xunit.Assert.StartsWith(SyntheticDatabasePrefix, builder.Database, StringComparison.Ordinal);
        return connectionString!;
    }
}
